Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/../../scripts/sdk-compile-evidence.ps1"
. "$PSScriptRoot/native-reuse-source-gate.ps1"
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$summary=[pscustomobject][ordered]@{Kind='NativeReuseSdkCompileSummary';SourceCommit=$null;Scope='ConfigureReleaseX64LinkOnly';Status='Fail';Reason='RunnerFailure';ConfigureStatus='NotRun';BuildStatus='NotRun';ConfigureExit=$null;BuildExit=$null;ExecutableExists=$false;CompilerFaults=@();Worker='NotRun';Uia='NotRun';Com='NotRun';Job='NotRun';Match='NotRun';ModelCalls=0;ExitCode=3}
$logs=@()
function ConvertTo-FixedCompilerExit([int]$Value){if($Value -in @(0,1,2,3)){return $Value};return 'Other'}
try {
    if(-not $IsWindows -or $env:CI -cne 'true'){$summary.Status='Blocked';$summary.Reason='HostedWindowsRequired';$summary.ExitCode=2}
    else {
        $commit=Get-NativeReuseSourceCommit $repo $env:GITHUB_SHA
        $summary.SourceCommit=$commit
        if([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -cne 'X64'){throw 'Invalid architecture'}
        $output=Join-Path $repo ('artifacts/native-reuse-build/'+[guid]::NewGuid().ToString('N'))
        [void](New-Item -ItemType Directory -Path $output)
        $inventory=Join-Path $output 'inventory.local.log'
        & cmake --version *> $inventory
        if($LASTEXITCODE -ne 0){throw 'Missing compiler prerequisite'}
        $version=Get-Content -LiteralPath $inventory -TotalCount 1
        if($version -cnotmatch '^cmake version (\d+\.\d+\.\d+)$' -or [version]$Matches[1] -lt [version]'3.28.0'){throw 'Missing compiler prerequisite'}
        $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        if(-not (Test-Path -LiteralPath $vswhere -PathType Leaf)){throw 'Missing compiler prerequisite'}
        & $vswhere -products '*' -version '[17.0,18.0)' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json -utf8 *> $inventory
        if($LASTEXITCODE -ne 0 -or (Get-Item -LiteralPath $inventory).Length -gt 1048576){throw 'Missing compiler prerequisite'}
        $instances=@(Get-Content -LiteralPath $inventory -Raw | ConvertFrom-Json)
        if($instances.Count -ne 1){throw 'Missing compiler prerequisite'}
        $vs=$instances[0].installationPath
        $toolset=(Get-Content -LiteralPath (Join-Path $vs 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt') -Raw).Trim()
        if($toolset -cnotmatch '^14\.44\.\d+$' -or -not (Test-Path -LiteralPath (Join-Path $vs "VC/Tools/MSVC/$toolset/bin/Hostx64/x64/cl.exe") -PathType Leaf)){throw 'Missing compiler prerequisite'}
        $sdk=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
        foreach($relative in @('Include/10.0.26100.0/um/Windows.h','Include/10.0.26100.0/um/UIAutomation.h','Lib/10.0.26100.0/um/x64/ole32.lib')){if(-not (Test-Path -LiteralPath (Join-Path $sdk $relative) -PathType Leaf)){throw 'Missing compiler prerequisite'}}
        $native=Join-Path $output 'native';$configure=Join-Path $output 'configure.local.log';$build=Join-Path $output 'build.local.log';$logs=@($configure,$build)
        & cmake -S (Join-Path $repo 'tools/KuaiYi.SelectionReuseNative') -B $native -G 'Visual Studio 17 2022' -A x64 -T "v143,version=$toolset" "-DCMAKE_GENERATOR_INSTANCE=$vs" '-DCMAKE_SYSTEM_VERSION=10.0.26100.0' *> $configure
        $actual=$LASTEXITCODE;$summary.ConfigureExit=ConvertTo-FixedCompilerExit $actual
        if($actual -ne 0){$summary.ConfigureStatus='Fail';$summary.Reason='SdkConfigureFailed'}
        else {
            $summary.ConfigureStatus='Pass'
            & cmake --build $native --config Release --parallel 2 *> $build
            $actual=$LASTEXITCODE;$summary.BuildExit=ConvertTo-FixedCompilerExit $actual
            if($actual -ne 0){$summary.BuildStatus='Fail';$summary.Reason='SdkBuildFailed'}
            else {
                $summary.ExecutableExists=Test-Path -LiteralPath (Join-Path $native 'Release/KuaiYi.SelectionReuseNative.exe') -PathType Leaf
                if(-not $summary.ExecutableExists){$summary.BuildStatus='Fail';$summary.Reason='SdkExecutableUnavailable'}
                else {$summary.BuildStatus='Pass';$summary.Status='Pass';$summary.Reason='Completed';$summary.ExitCode=0}
            }
        }
    }
} catch { # Raw commands, exceptions, paths and compiler text never reach public streams.
} finally {
    try {
    $safe=[Collections.Generic.List[object]]::new()
    foreach($log in $logs){if((Test-Path -LiteralPath $log -PathType Leaf) -and (Get-Item -LiteralPath $log).Length -le 4194304){foreach($row in @(Get-SdkCompilerEvidence (Get-Content -LiteralPath $log -Raw) -Profile NativeReuse)){if($safe.Count -lt 16){$safe.Add($row)}}}}
    $summary.CompilerFaults=$safe.ToArray()
    } catch {$summary.Status='Fail';$summary.Reason='EvidenceFailure';$summary.ExitCode=3;$summary.CompilerFaults=@()}
}
Write-Host ('KUAIYI_NATIVE_REUSE_COMPILE_SUMMARY '+($summary | ConvertTo-Json -Depth 5 -Compress))
exit $summary.ExitCode
