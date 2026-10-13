param([switch]$ProbeOnly,[switch]$OleOnly,[switch]$OleMarshalOnly,[string]$OutputRoot)
. "$PSScriptRoot/windows-ci-common.ps1"
if ($OleOnly -and $OleMarshalOnly) { throw 'Select one OLE scope' }
$exitCode = 0
$root = if ($OutputRoot) { [IO.Path]::GetFullPath($OutputRoot) } else { Join-Path $script:RepositoryRoot ('artifacts/windows-ci/' + [guid]::NewGuid().ToString('N')) }
[void][IO.Directory]::CreateDirectory($root)
$report = [ordered]@{
    Kind = 'HostedWindowsContracts'; Scope = 'ServerOrClientFixtureContractsOnly'; Windows11Acceptance = 'NotRun'
    SourceCommit = (& git -C $script:RepositoryRoot rev-parse HEAD).Trim(); RunUrl = $null
    SourceDirty = @(& git -C $script:RepositoryRoot status --porcelain).Count -gt 0
    SpecificationBytes = @()
    Environment = $null; NativeStatus = 'NotRun'; NativeDlls = 0; CTestsPassed = 0; PeChecksPassed = 0; DllLoadsPassed = 0; ExportsPassed = 0
    NativeArtifacts = @(); OverlayStatus = 'NotRun'; OverlayChecksPassed = 0; OverlayReport = $null; OleStatus = 'NotRun'; OleSyntheticPointerStatus = 'NotRun'; E06PhysicalAcceptance = 'NotRun'; OleReport = $null; Errors = @(); ExitCode = 0
}
if ($env:GITHUB_SERVER_URL -and $env:GITHUB_REPOSITORY -and $env:GITHUB_RUN_ID) { $report.RunUrl = "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID" }
if ($OleOnly) { $report.Scope = 'HostedOleFixtureContractsOnly' }
if ($OleMarshalOnly) { $report.Scope = 'OwnedNativeMarshalOnly'; $report['MarshalStatus'] = 'NotRun'; $report['MarshalReport'] = $null }
Push-Location $script:RepositoryRoot
try {
    $facts = $null
    try { $facts = Get-CiEnvironment; $report.Environment = $facts }
    catch { $report.Errors += 'EnvironmentInventoryFailed:' + $_.Exception.GetType().Name }
    Write-Host 'KUAIYI_ENVIRONMENT_BEGIN'
    Write-Host ($facts | ConvertTo-Json -Depth 10 -Compress)
    Write-Host 'KUAIYI_ENVIRONMENT_END'
    foreach ($name in @('SPEC.md','快译_产品与技术开发规格_v1.0.md')) {
        $bytes = [IO.File]::ReadAllBytes((Join-Path $script:RepositoryRoot $name))
        $lf = 0; $crlf = 0
        for ($i = 0; $i -lt $bytes.Length; $i++) { if ($bytes[$i] -eq 10) { $lf++; if ($i -gt 0 -and $bytes[$i - 1] -eq 13) { $crlf++ } } }
        $report.SpecificationBytes += @{ Name = $name; Bytes = $bytes.Length; Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)); LF = $lf; CRLF = $crlf }
    }
    Write-Host ('KUAIYI_SPECIFICATION_BYTES ' + ($report.SpecificationBytes | ConvertTo-Json -Compress))
    Assert-SpecIntegrity # The exact original byte hash and all original assertions are unchanged.
    if ($null -eq $facts) {
        $report.NativeStatus = 'Blocked'; $report.OverlayStatus = 'Blocked'; $report.OleStatus = 'Blocked'; if ($OleMarshalOnly) { $report.MarshalStatus = 'Blocked' }; $exitCode = 2
    } elseif ($ProbeOnly) {
        $report.NativeStatus = 'NotRun'; $report.OverlayStatus = 'NotRun'
    } elseif ($facts.NativeBlockers.Count -gt 0) {
        $report.NativeStatus = 'Blocked'; $report.OverlayStatus = 'Blocked'; $report.OleStatus = 'Blocked'; if ($OleMarshalOnly) { $report.MarshalStatus = 'Blocked' }; $exitCode = 2
    } else {
        $report.NativeStatus = 'InProgress'
        $exports = [ordered]@{
            'KuaiYi.OcrNative' = @('KY_OcrDiagnose','KY_OcrRecognize','KY_OcrFree')
            'KuaiYi.ShellExtension' = @('DllGetClassObject','DllCanUnloadNow')
            'KuaiYi.CaptureNative' = @('KY_CaptureMonitor','KY_CaptureGdi')
            'KuaiYi.OleNativeBridge' = @('KY_CreateDropTarget')
        }
        $components = if ($OleOnly -or $OleMarshalOnly) { @('KuaiYi.OleNativeBridge') } else { @($exports.Keys) }
        foreach ($component in $components) {
            $build = Join-Path $root $component
            # Exact installed patch selected once, explicitly supplied to CMake and recorded above.
            Invoke-Checked cmake @('-S',"src/$component",'-B',$build,'-G','Visual Studio 17 2022','-A','x64','-T',"v143,version=$($facts.Toolset)","-DCMAKE_GENERATOR_INSTANCE=$($facts.VisualStudio.Path)","-DCMAKE_SYSTEM_VERSION=$($facts.WindowsSdk)")
            Invoke-Checked cmake @('--build',$build,'--config','Release','--parallel','2')
            $dll = Join-Path $build "Release/$component.dll"
            Assert-CiNativePe $dll; $report.PeChecksPassed++
            $handle = [Runtime.InteropServices.NativeLibrary]::Load($dll)
            try {
                $report.DllLoadsPassed++
                foreach ($symbol in $exports[$component]) {
                    if ([Runtime.InteropServices.NativeLibrary]::GetExport($handle,$symbol) -eq [IntPtr]::Zero) { throw "Missing export: $component/$symbol" }
                    $report.ExportsPassed++
                }
            } finally { [Runtime.InteropServices.NativeLibrary]::Free($handle) }
            $report.NativeDlls++
            $report.NativeArtifacts += @{ Name = "$component.dll"; Bytes = (Get-Item -LiteralPath $dll).Length; Sha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash; Exports = $exports[$component] }
        }
        $tests = if ($OleOnly -or $OleMarshalOnly) { @() } else { @(@{ Component = 'KuaiYi.ShellExtension'; Name = 'CommandPolicy' },@{ Component = 'KuaiYi.CaptureNative'; Name = 'GdiReadOrderingContract' }) }
        foreach ($test in $tests) {
            $junit = Join-Path $root ($test.Name + '.xml')
            Invoke-Checked ctest @('--test-dir',(Join-Path $root $test.Component),'-C','Release','--output-on-failure','--output-junit',$junit)
            $report.CTestsPassed += Assert-CiCtestResults $junit $test.Name
            Write-Host (Get-Content -LiteralPath $junit -Raw)
        }
        $report.NativeStatus = 'Pass'
        if ($facts.DesktopBlockers.Count -gt 0 -or $facts.Sdk -cne '10.0.401' -or -not $facts.DesktopRuntime) {
            $report.OverlayStatus = 'Blocked'
            $report.OleStatus = 'Blocked'
            if ($OleMarshalOnly) { $report.MarshalStatus = 'Blocked' }
            if ($facts.Sdk -cne '10.0.401') { $report.Errors += 'OverlayRequiredSdkMissing' }
            if (-not $facts.DesktopRuntime) { $report.Errors += 'OverlayRequiredDesktopRuntimeMissing' }
        } else {
            if (-not $OleOnly -and -not $OleMarshalOnly) {
            $report.OverlayStatus = 'InProgress'
            # Existing locked dependencies only; no setup-dotnet, WinAppCLI, installer or SDK installation.
            Invoke-Checked dotnet @('restore','tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj','--locked-mode')
            Invoke-Checked dotnet @('build','tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj','--configuration','Release','--no-restore')
            $executable = Join-Path $script:RepositoryRoot 'tools/KuaiYi.GrowthHarness/bin/Release/net10.0-windows/win-x64/KuaiYi.GrowthHarness.exe'
            $csv = Join-Path $root 'overlay.csv'
            $info = [Diagnostics.ProcessStartInfo]::new($executable); $info.UseShellExecute = $false
            $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
            [void]$info.ArgumentList.Add('--ci-actual-overlay'); [void]$info.ArgumentList.Add($csv)
            $child = [Diagnostics.Process]::Start($info)
            $stdout = $child.StandardOutput.ReadToEndAsync(); $stderr = $child.StandardError.ReadToEndAsync()
            try {
                if (-not $child.WaitForExit(35000)) {
                    $child.Kill($true)
                    if (-not $child.WaitForExit(5000)) { throw 'Owned overlay exit unconfirmed' }
                    throw 'Owned overlay deadline exceeded'
                }
                if (Test-Path -LiteralPath ($csv + '.summary.json')) { $report.OverlayReport = Get-Content -LiteralPath ($csv + '.summary.json') -Raw | ConvertFrom-Json }
                if (Test-Path -LiteralPath $csv) {
                    Write-Host 'KUAIYI_OVERLAY_FRAMES_BEGIN'
                    Get-Content -LiteralPath $csv | ForEach-Object { Write-Host $_ }
                    Write-Host 'KUAIYI_OVERLAY_FRAMES_END'
                }
                if ($child.ExitCode -ne 0) { throw "Owned overlay failed: exit $($child.ExitCode)" }
                if ($null -eq $report.OverlayReport) { throw 'Missing actual overlay receipt' }
                $report.OverlayChecksPassed = Assert-CiOverlayReport $report.OverlayReport
                $report.OverlayStatus = 'Pass'
            } finally {
                foreach ($drain in @($stdout,$stderr)) {
                    if ($drain.IsCompletedSuccessfully) { Write-Host $drain.GetAwaiter().GetResult() }
                    else { Write-Host 'Owned probe output drain incomplete; no unbounded wait.' }
                }
                if (Test-Path -LiteralPath ($csv + '.phases.log')) { Get-Content -LiteralPath ($csv + '.phases.log') | ForEach-Object { Write-Host ('PUBLIC_OVERLAY_PHASE ' + $_) } }
                $child.Dispose()
            }
            }
            if ($OleMarshalOnly) {
            $report.MarshalStatus = 'InProgress'
            $report.MarshalReport = & "$PSScriptRoot/run-windows-ole-marshal-ci.ps1" -NativeRoot $root -OutputRoot (Join-Path $root 'marshal-ci')
            $report.MarshalStatus = $report.MarshalReport.Status
            }
            if ($OleOnly) {
            $report.OleStatus = 'InProgress'
            $report.OleReport = & "$PSScriptRoot/run-windows-ole-ci.ps1" -NativeRoot $root -OutputRoot (Join-Path $root 'ole-ci')
            $report.OleStatus = $report.OleReport.IsolationStatus
            $report.OleSyntheticPointerStatus = $report.OleReport.SyntheticPointerStatus
            }
        }
    }
} catch {
    $report.Errors += $_.Exception.Message
    if ($OleMarshalOnly -and $report.MarshalStatus -eq 'InProgress') { $report.MarshalStatus = 'Fail' }
    if ($report.NativeStatus -eq 'InProgress') { $report.NativeStatus = 'Fail' }
    if ($report.OverlayStatus -eq 'InProgress') { $report.OverlayStatus = 'Fail' }
    if ($report.OleStatus -eq 'InProgress') { $report.OleStatus = 'Fail' }
    $exitCode = 3
} finally {
    Pop-Location
    $report.ExitCode = $exitCode
    $json = $report | ConvertTo-Json -Depth 12 -Compress
    [IO.File]::WriteAllText((Join-Path $root 'report.json'),$json)
    Write-Host 'KUAIYI_CONTRACT_REPORT_BEGIN'; Write-Host $json; Write-Host 'KUAIYI_CONTRACT_REPORT_END'
    if ($env:GITHUB_STEP_SUMMARY) {
        $text = "Hosted Windows fixture contracts; Windows 11 ordinary-user acceptance: **NotRun**.`n`nNative: $($report.NativeStatus); DLLs: $($report.NativeDlls); CTests: $($report.CTestsPassed); PE checks: $($report.PeChecksPassed); loads: $($report.DllLoadsPassed); exports: $($report.ExportsPassed).`n`nOverlay: $($report.OverlayStatus); checks: $($report.OverlayChecksPassed). Exit: $exitCode.`n`nFull environment/receipt/CSV evidence is in the job log; no artifact upload.`n"
        if ($OleOnly) { $text += "`nOLE isolation/cancel/close/recovery: $($report.OleStatus); synthetic pointer: $($report.OleSyntheticPointerStatus); physical E06: **NotRun**. Earlier accepted CTests/HWND contracts were not rerun.`n" }
        if ($OleMarshalOnly) { $text += "`nMinimal actual native export/import: $($report.MarshalStatus); actual Drop/GetData/close/recovery/physical E06: **NotRun**.`n" }
        [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY,$text)
    }
}
exit $exitCode
