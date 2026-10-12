Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/common.ps1"

function Get-CiDesktopBlockers {
    param($Facts)
    $reasons = [Collections.Generic.List[string]]::new()
    if (-not $Facts.IsWindows -or $Facts.Architecture -cne 'X64') { $reasons.Add('WindowsX64Required') }
    if (-not $Facts.UserInteractive -or $Facts.SessionId -eq 0) { $reasons.Add('InteractiveSessionRequired') }
    if ($Facts.WindowStation -cne 'WinSta0' -or $Facts.InputDesktop -cne 'Default') { $reasons.Add('UnlockedInputDesktopRequired') }
    if ($Facts.Monitors.Count -eq 0 -or @($Facts.Monitors | Where-Object { $_.WorkArea.Right -le $_.WorkArea.Left -or $_.WorkArea.Bottom -le $_.WorkArea.Top }).Count -gt 0) { $reasons.Add('UsableDisplayRequired') }
    if (-not $Facts.HasShell) { $reasons.Add('ShellRequiredForProductionTray') }
    if (-not $Facts.DwmEnabled) { $reasons.Add('DwmUnavailable') }
    return $reasons.ToArray()
}

function Get-CiEnvironment {
    $facts = [ordered]@{
        IsWindows = $IsWindows; Architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        UserInteractive = [Environment]::UserInteractive; SessionId = [Diagnostics.Process]::GetCurrentProcess().SessionId
        Os = $null; Administrator = $null; IntegrityLevel = $null; EnableLua = $null; WindowStation = 'NotRun'; InputDesktop = 'NotRun'; DesktopProbeError = $null
        Monitors = @(); ProbeDpiAwareness = $null; HasShell = $false; DwmEnabled = $false
        PowerShell = $PSVersionTable.PSVersion.ToString(); ImageOS = $env:ImageOS; ImageVersion = $env:ImageVersion
        Sdk = $null; DesktopRuntime = $false; CMake = $null; CTest = $null; VisualStudio = $null; Toolset = $null; WindowsSdk = '10.0.26100.0'
        NativeBlockers = @(); DesktopBlockers = @(); Scope = 'HostedWindowsContractsOnly'; Windows11Acceptance = 'NotRun'
    }
    if (-not $IsWindows) { $facts.NativeBlockers = @('WindowsRequired'); $facts.DesktopBlockers = @('WindowsRequired'); return [pscustomobject]$facts }
    if (-not ('KuaiYiCiWindowsFacts' -as [type])) { Add-Type -Path (Join-Path $script:RepositoryRoot 'tools/ci/WindowsFacts.cs') }
    $facts.Os = [KuaiYiCiWindowsFacts]::ReadOs()
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        $facts.Administrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        $facts.IntegrityLevel = [KuaiYiCiWindowsFacts]::IntegrityLevel($identity.Token)
    } finally { $identity.Dispose() }
    $facts.EnableLua = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name EnableLUA).EnableLUA
    try {
        $facts.WindowStation = [KuaiYiCiWindowsFacts]::WindowStation(); $facts.InputDesktop = [KuaiYiCiWindowsFacts]::InputDesktop()
        $facts.Monitors = @([KuaiYiCiWindowsFacts]::Monitors()); $facts.ProbeDpiAwareness = [KuaiYiCiWindowsFacts]::DpiAwareness()
        $facts.HasShell = [KuaiYiCiWindowsFacts]::HasShell(); $facts.DwmEnabled = [KuaiYiCiWindowsFacts]::DwmEnabled()
    } catch { $facts.DesktopProbeError = $_.Exception.GetType().Name }
    $facts.DesktopBlockers = @(Get-CiDesktopBlockers ([pscustomobject]$facts))
    if ($facts.DesktopProbeError) { $facts.DesktopBlockers += 'DesktopInventoryFailed' }
    $missing = [Collections.Generic.List[string]]::new()
    if ($facts.Architecture -cne 'X64') { $missing.Add('X64Required') }
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $version = @(& dotnet --version); if ($LASTEXITCODE -eq 0) { $facts.Sdk = ($version -join '').Trim() }
        $runtimes = @(& dotnet --list-runtimes); if ($LASTEXITCODE -eq 0) { $facts.DesktopRuntime = @($runtimes | Where-Object { $_ -match '^Microsoft.WindowsDesktop.App 10\.0\.12 ' }).Count -eq 1 }
    }
    if (Get-Command cmake -ErrorAction SilentlyContinue) {
        $version = @(& cmake --version); if ($LASTEXITCODE -eq 0 -and $version[0] -match '^cmake version (\d+\.\d+\.\d+)') { $facts.CMake = $Matches[1] }
    }
    if (-not $facts.CMake -or [version]$facts.CMake -lt [version]'3.28.0') { $missing.Add('CMake328Required') }
    if (Get-Command ctest -ErrorAction SilentlyContinue) {
        $version = @(& ctest --version); if ($LASTEXITCODE -eq 0 -and $version[0] -match '^ctest version (\d+\.\d+\.\d+)') { $facts.CTest = $Matches[1] }
    }
    if (-not $facts.CTest -or $facts.CTest -cne $facts.CMake) { $missing.Add('MatchingPreinstalledCTestRequired') }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $instances = @(& $vswhere -products '*' -version '[17.0,18.0)' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json -utf8 | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0 -or $instances.Count -ne 1) { $missing.Add('ExactlyOnePreinstalledVS2022Required') }
        else {
            $instance = $instances[0]; $facts.VisualStudio = @{ Path = $instance.installationPath; Version = $instance.installationVersion }
            $toolsetFile = Join-Path $instance.installationPath 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt'
            if (Test-Path -LiteralPath $toolsetFile) { $facts.Toolset = (Get-Content -LiteralPath $toolsetFile -Raw).Trim() }
            if (-not $facts.Toolset -or $facts.Toolset -notmatch '^14\.44\.\d+$' -or -not (Test-Path -LiteralPath (Join-Path $instance.installationPath "VC/Tools/MSVC/$($facts.Toolset)/bin/Hostx64/x64/cl.exe"))) { $missing.Add('PreinstalledMSVC1444Required') }
        }
    } else { $missing.Add('VSWhereMissing') }
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
    foreach ($relative in @('Include/10.0.26100.0/um/Windows.h','Include/10.0.26100.0/cppwinrt/winrt/Windows.Foundation.h','Lib/10.0.26100.0/um/x64/windowsapp.lib')) {
        if (-not (Test-Path -LiteralPath (Join-Path $sdkRoot $relative))) { $missing.Add("SdkFileMissing:$relative") }
    }
    $facts.NativeBlockers = $missing.ToArray()
    return [pscustomobject]$facts
}

function Assert-CiNativePe {
    param([string]$Path)
    $stream = [IO.File]::OpenRead($Path); $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($stream.Length -lt 88 -or $reader.ReadUInt16() -ne 0x5A4D) { throw 'Invalid DOS header' }
        $stream.Position = 0x3C; $offset = $reader.ReadUInt32()
        if ($offset -lt 64 -or $offset -gt $stream.Length - 26) { throw 'Invalid PE offset' }
        $stream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne 0x8664) { throw 'Native x64 PE required' }
        $stream.Position = $offset + 20; $size = $reader.ReadUInt16(); $characteristics = $reader.ReadUInt16()
        if ($size -lt 2 -or $offset + 24 + $size -gt $stream.Length -or ($characteristics -band 0x2000) -eq 0 -or $reader.ReadUInt16() -ne 0x20B) { throw 'PE32+ DLL required' }
    } finally { $reader.Dispose() }
}

function Assert-CiCtestResults {
    param([string]$Path, [string]$ExpectedName)
    [xml]$result = Get-Content -LiteralPath $Path -Raw
    $cases = @($result.SelectNodes('//testcase'))
    if ($cases.Count -ne 1 -or $cases[0].GetAttribute('name') -cne $ExpectedName -or $cases[0].GetAttribute('status') -cne 'run' -or $cases[0].SelectNodes('failure|error|skipped').Count -ne 0) { throw "Expected one executed passing CTest: $ExpectedName" }
    return 1
}

function Assert-CiOverlayReport {
    param($Report)
    if ($Report.Kind -cne 'ActualProductionOverlayPublicProbe' -or $Report.Source -cne 'PublicFixtureOnly' -or $Report.ModelCalls -ne 0 -or $Report.DesktopAcceptance -cne 'NotEvaluated' -or $Report.Mode -cne 'BoundedCi' -or $Report.ExitCode -ne 0) { throw 'Invalid bounded public overlay receipt' }
    if ($Report.RetainedFrames -lt 10 -or $Report.DiscardedFrames -ne 0 -or @($Report.Errors).Count -ne 0 -or $Report.StreamCompleted -isnot [bool] -or -not $Report.StreamCompleted) { throw 'Incomplete overlay observation' }
    $names = @('AnchorContained','WorkContained','NativeMatches','ActualAnchorContained','ActualWorkContained','ForegroundUnchanged','ActualButtonMatchesAnchor','AnchorStable','Grew')
    if (@($Report.Checks.PSObject.Properties).Count -ne $names.Count) { throw 'Unexpected overlay checks' }
    foreach ($name in $names) { if ($Report.Checks.$name -isnot [bool] -or -not $Report.Checks.$name) { throw "Overlay contract failed: $name" } }
    return $names.Count
}
