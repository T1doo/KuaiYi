Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/sdk-compile-evidence.ps1"
$root=Split-Path $PSScriptRoot -Parent
$summary=[pscustomobject][ordered]@{Kind='HostedSdkCompileSummary';SourceCommit=$null;Scope='NativeSdkBuildOnly';Status='Fail';Reason='RunnerFailure';ConfigureStatus='NotRun';BuildStatus='NotRun';ConfigureExit=$null;BuildExit=$null;CompilerFaults=@();Uia='NotRun';ModelCalls=0;ExitCode=3}
$log=$null
try {
    if(-not $IsWindows -or $env:CI -cne 'true'){$summary.Status='Blocked';$summary.Reason='HostedWindowsRequired';$summary.ExitCode=2}
    else {
        $commit=(& git -C $root rev-parse HEAD 2>$null).Trim()
        if($LASTEXITCODE -ne 0 -or $commit -cnotmatch '^[0-9a-f]{40}$' -or $env:GITHUB_SHA -cne $commit -or @(& git -C $root status --porcelain --untracked-files=no 2>$null).Count -ne 0){throw 'Invalid source'}
        $summary.SourceCommit=$commit
        . "$PSScriptRoot/windows-ci-common.ps1"
        $facts=Get-CiEnvironment 2>$null 3>$null 4>$null 5>$null 6>$null
        if(@($facts.NativeBlockers).Count -ne 0){$summary.Status='Blocked';$summary.Reason='SdkPrerequisiteUnavailable';$summary.ExitCode=2}
        else {
            $output=Join-Path $root ('artifacts/windows-selection/'+[guid]::NewGuid().ToString('N'))
            [void](New-Item -ItemType Directory -Path $output)
            $native=Join-Path $output 'sdk-com';$log=Join-Path $output 'sdk-build.local.log'
            & cmake -S (Join-Path $root 'tools/KuaiYi.ComCaptureProbe') -B $native -G 'Visual Studio 17 2022' -A x64 -T "v143,version=$($facts.Toolset)" "-DCMAKE_GENERATOR_INSTANCE=$($facts.VisualStudio.Path)" "-DCMAKE_SYSTEM_VERSION=$($facts.WindowsSdk)" *> $log
            $summary.ConfigureExit=$LASTEXITCODE
            if($summary.ConfigureExit -ne 0){$summary.ConfigureStatus='Fail';$summary.Reason='SdkConfigureFailed'}
            else {
                $summary.ConfigureStatus='Pass'
                & cmake --build $native --config Release --parallel 2 *> $log
                $summary.BuildExit=$LASTEXITCODE
                if($summary.BuildExit -ne 0){$summary.BuildStatus='Fail';$summary.Reason='SdkBuildFailed'}
                elseif(-not (Test-Path -LiteralPath (Join-Path $native 'Release/KuaiYi.ComCaptureProbe.exe') -PathType Leaf)){$summary.BuildStatus='Fail';$summary.Reason='SdkExecutableUnavailable'}
                else {$summary.BuildStatus='Pass';$summary.Status='Pass';$summary.Reason='Completed';$summary.ExitCode=0}
            }
            if((Get-Item -LiteralPath $log).Length -le 4194304){$summary.CompilerFaults=@(Get-SdkCompilerEvidence (Get-Content -LiteralPath $log -Raw))}
        }
    }
} catch { # No arbitrary exception, command, environment, source text or path crosses the public boundary.
}
Write-Host ('KUAIYI_SDK_COMPILE_SUMMARY '+($summary | ConvertTo-Json -Depth 5 -Compress))
exit $summary.ExitCode
