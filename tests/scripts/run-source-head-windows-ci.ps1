Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/source-head-ci-evidence.ps1"
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$summary=[pscustomobject][ordered]@{Kind='SourceHeadCiSummary';Scope='SourceHeadOnly';ExpectedSource=$null;SourceCommit=$null;ReviewedParent='4bc4ee3ea471ed1dc51f83c8d04827ad02f92ed2';LockedAncestor='3ae69419a1152ea9694a3a9cf11130fbae645fa3';Status='Fail';Reason='PreparationFailure';BootstrapStage='HostGuard';PreparationError='None';Operations=@();AncestorVerification='NotRun';NativeCompile='NotRun';ManagedCompile='NotRun';LifecycleCases='NotRun';UiaText='NotRun';Match='NotRun';Performance='NotRun';ProductionReuse='Off';ModelCalls=0;ExitCode=3}
try {
    if(-not $IsWindows -or $env:CI -cne 'true'){$summary.Status='Blocked';$summary.Reason='HostedWindowsRequired';$summary.ExitCode=2}
    else {
        $summary.BootstrapStage='EnvironmentCheck'
        if($env:GITHUB_SHA -cnotmatch '^[0-9a-f]{40}$' -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -cne 'X64'){$summary.Reason='InvalidEnvironment';throw 'Invalid environment'}
        $summary.ExpectedSource=$env:GITHUB_SHA
        $summary.BootstrapStage='OutputDirectory'
        $output=Join-Path $repo ('artifacts/source-head-ci/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $output)
        $summary.BootstrapStage='InteropLoad'
        # Only the existing in-memory PowerShell interop definition. No product/project build or restore.
        Add-Type -Path (Join-Path $repo 'tools/ci/LifecycleOperation.cs') 2>$null 3>$null 4>$null 5>$null 6>$null
        $summary.BootstrapStage='CommandLookup'
        $git=(Get-Command git -CommandType Application | Select-Object -First 1).Source
        $summary.BootstrapStage='SourceHead'
        $log=Join-Path $output 'SourceHead.local.log'
        # Exactly one external diagnostic operation; no retry or subsequent Git/build/runtime commands.
        $receipt=[KuaiYiLifecycleOperation]::Run($git,@('-C',$repo,'rev-parse','HEAD'),$repo,$log)
        $summary.Operations=@(Get-LifecycleOperationEvidence 'SourceHead' $receipt)
        $reason=[KuaiYiLifecycleOperation]::GetReason($receipt)
        if($reason -ne [KuaiYiLifecycleOperation+OperationReason]::Completed){$summary.Reason=$reason.ToString();throw 'Operation failed'}
        $summary.BootstrapStage='SourceRead'
        if((Get-Item -LiteralPath $log).Length -gt 4096){$summary.Reason='InvalidGitOutput';throw 'Invalid output'}
        $outcome=Get-SourceHeadOutcome $summary.Operations[0] (Get-Content -LiteralPath $log -Raw) $summary.ExpectedSource
        $summary.Reason=$outcome
        if($outcome -cne 'SourceHeadSucceeded'){throw 'Source verification failed'}
        $summary.SourceCommit=$summary.ExpectedSource;$summary.BootstrapStage='Completed';$summary.Status='Pass';$summary.ExitCode=0
    }
} catch {
    # Script exception messages, paths and identities never enter public streams.
    if($summary.Reason -ceq 'PreparationFailure'){$summary.PreparationError=Get-SourceHeadPreparationError $_.Exception}
}
Write-Host ('KUAIYI_SOURCE_HEAD_CI_SUMMARY '+($summary | ConvertTo-Json -Depth 6 -Compress))
exit $summary.ExitCode
