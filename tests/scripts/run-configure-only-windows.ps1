Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
. "$PSScriptRoot/lifecycle-runner-contracts.ps1"
$configureDiagnosticReady=$false
try {. "$PSScriptRoot/lifecycle-configure-diagnostics.ps1";$configureDiagnosticReady=$true} catch {$configureDiagnosticReady=$false}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$locked='3ae69419a1152ea9694a3a9cf11130fbae645fa3'
$summary=[pscustomobject][ordered]@{Kind='ConfigureOnlyCiSummary';SourceCommit=$null;ReviewedParent='9d75368aa246b40e2bfc9517eb390384c6faf470';ParentVerified=$false;AncestorVerified=$false;LockedSource=$locked;LockedTree='870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b';LockedTreeVerified=$false;SourceEquivalent=$false;Scope='NativeConfigureOnly';Status='Fail';Reason='RunnerFailure';Operations=@();CollectorSettlement='NotStarted';NativeBuild='NotRun';ManagedRestore='NotRun';ManagedBuild='NotRun';FourCases='NotRun';UiaText='NotRun';Match='NotRun';Performance='NotRun';ProductionReuse='Off';ModelCalls=0;ExitCode=3}
$output=$null;$operations=[Collections.Generic.List[object]]::new()
$configureLease=$null;$configureDiagnostic=$null;$configureAccountingReceipt=$null;$configureAccountingOutputStatus='NotAttempted'
function Invoke-ConfigureOnlyOperation([string]$Kind,[string]$Executable,[string[]]$Arguments) {
    $log=Join-Path $script:output ($Kind+'.local.log')
    $receipt=[KuaiYiLifecycleOperation]::Run($Executable,$Arguments,$script:repo,$log)
    $script:operations.Add((Get-LifecycleOperationEvidence $Kind $receipt))
    # Original operation has returned through its unchanged rescue/cleanup envelope.
    if($Kind -ceq 'NativeConfigure'){
        $script:configureAccountingReceipt=$receipt
        if(-not $script:configureDiagnosticReady){$script:configureDiagnostic=[pscustomobject]@{DiagnosticStatus='ReaderUnavailable';Files=@()}}
        else {$script:configureDiagnostic=Complete-LifecycleConfigureDiagnostic $receipt $script:configureLease {
            param($lease)
            $python=@(Get-Command python -CommandType Application -ErrorAction Stop)[0].Source
            $collector=Join-Path $script:repo 'tools/ci/lifecycle_configure_collect.py'
            $diagnosticLog=Join-Path $script:output 'ConfigureDiagnostic.local.json'
            $arguments=@('-I','-B',$collector,'confirmed',[string]$lease.SourceProcess,[string]$lease.Handle,[string]$lease.Volume,[string]$lease.Index)
            # Unknown until the separate collector receipt confirms cleanup.
            $script:summary.CollectorSettlement='Unknown'
            $diagnosticReceipt=[KuaiYiLifecycleOperation]::Run($python,$arguments,$script:repo,$diagnosticLog)
            if($diagnosticReceipt.CleanupConfirmed -is [bool] -and $diagnosticReceipt.CleanupConfirmed){$script:summary.CollectorSettlement='CleanupConfirmed'}
            if([KuaiYiLifecycleOperation]::GetReason($diagnosticReceipt) -ne [KuaiYiLifecycleOperation+OperationReason]::Completed -or $diagnosticReceipt.Exit -cne '0' -or (Get-Item -LiteralPath $diagnosticLog).Length -gt 8192){throw 'Diagnostic unavailable'}
            return (Get-Content -LiteralPath $diagnosticLog -Raw)
        }}
        $script:configureLease=$null
    }
    $reason=[KuaiYiLifecycleOperation]::GetReason($receipt)
    if($reason -ne [KuaiYiLifecycleOperation+OperationReason]::Completed){$script:summary.Reason=$reason.ToString();throw 'Operation failed'}
    return [pscustomobject]@{Exit=$receipt.Exit;Log=$log}
}
try {
    if(-not $IsWindows -or $env:CI -cne 'true'){$summary.Status='Blocked';$summary.Reason='HostedWindowsRequired';$summary.ExitCode=2}
    else {
        if($env:GITHUB_SHA -cnotmatch '^[0-9a-f]{40}$'){throw 'Invalid source'}
        if([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -cne 'X64'){throw 'Invalid architecture'}
        $output=Join-Path $repo ('artifacts/configure-only-ci/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $output)
        Add-Type -Path (Join-Path $repo 'tools/ci/LifecycleOperation.cs') 2>$null 3>$null 4>$null 5>$null 6>$null
        # Root identity is held before any configure. Failure disables only diagnostics.
        if($configureDiagnosticReady){try {Add-Type -Path (Join-Path $repo 'tools/ci/ConfigureRootLease.cs') 2>$null 3>$null 4>$null 5>$null 6>$null;$configureLease=[KuaiYiConfigureRootLease]::Open($output)} catch {$configureLease=$null}}
        $git=Get-LifecycleApplicationPath 'git'
        $head=Invoke-ConfigureOnlyOperation 'SourceHead' $git @('-C',$repo,'rev-parse','HEAD')
        $commit=Read-LifecycleExactSha (Get-Content -LiteralPath $head.Log -Raw)
        if($head.Exit -cne '0' -or $commit -cnotmatch '^[0-9a-f]{40}$' -or $commit -cne $env:GITHUB_SHA){throw 'Invalid source'};$summary.SourceCommit=$commit
        $parent=Invoke-ConfigureOnlyOperation 'SourceParent' $git @('-C',$repo,'rev-list','--parents','-n','1','HEAD')
        if($parent.Exit -cne '0' -or -not (Test-LifecycleSingleParent (Get-Content -LiteralPath $parent.Log -Raw) $summary.SourceCommit $summary.ReviewedParent)){$summary.Reason='ReviewedParentMismatch';throw 'Invalid parent'};$summary.ParentVerified=$true
        $ancestry=Invoke-ConfigureOnlyOperation 'SourceAncestry' $git @('-C',$repo,'merge-base','--is-ancestor',$locked,'HEAD')
        if($ancestry.Exit -cne '0'){$summary.Reason='LockedAncestorUnavailable';throw 'Invalid ancestry'};$summary.AncestorVerified=$true
        $lockedTree=Invoke-ConfigureOnlyOperation 'SourceLockedTree' $git @('-C',$repo,'rev-parse',($locked+'^{tree}'))
        if($lockedTree.Exit -cne '0' -or (Read-LifecycleExactSha (Get-Content -LiteralPath $lockedTree.Log -Raw)) -cne $summary.LockedTree){$summary.Reason='LockedTreeMismatch';throw 'Invalid locked tree'};$summary.LockedTreeVerified=$true
        $state=Invoke-ConfigureOnlyOperation 'SourceStatus' $git @('-C',$repo,'status','--porcelain','--untracked-files=no')
        if($state.Exit -cne '0' -or (Get-Item -LiteralPath $state.Log).Length -ne 0){throw 'Invalid source'}
        # Exact locked source subtrees and shared inputs remain unchanged, despite the workflow commit.
        $compare=Invoke-ConfigureOnlyOperation 'SourceCompare' $git (@('-C',$repo,'diff','--quiet',$locked,'HEAD','--')+(Get-LifecycleRuntimePaths))
        if($compare.Exit -cne '0') {throw 'Source changed'};$summary.SourceEquivalent=$true
        $cmake=Get-LifecycleApplicationPath 'cmake';$dotnet=Get-LifecycleApplicationPath 'dotnet'
        $version=Invoke-ConfigureOnlyOperation 'CMakeVersion' $cmake @('--version')
        $line=Get-Content -LiteralPath $version.Log -TotalCount 1
        if($version.Exit -cne '0' -or $line -cnotmatch '^cmake version (\d+\.\d+\.\d+)$' -or [version]$Matches[1] -lt [version]'3.28.0'){throw 'Missing prerequisite'}
        $sdk=Invoke-ConfigureOnlyOperation 'DotnetVersion' $dotnet @('--version')
        if($sdk.Exit -cne '0' -or (Get-Content -LiteralPath $sdk.Log -Raw).Trim() -cne '10.0.401'){throw 'Missing prerequisite'}
        $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $vsResult=Invoke-ConfigureOnlyOperation 'VisualStudioInventory' $vswhere @('-products','*','-version','[17.0,18.0)','-requires','Microsoft.VisualStudio.Component.VC.Tools.x86.x64','-format','json','-utf8')
        if($vsResult.Exit -cne '0' -or (Get-Item -LiteralPath $vsResult.Log).Length -gt 1048576){throw 'Missing prerequisite'}
        $instances=@(Get-Content -LiteralPath $vsResult.Log -Raw | ConvertFrom-Json);if($instances.Count -ne 1){throw 'Missing prerequisite'}
        $vs=$instances[0].installationPath;$toolset=(Get-Content -LiteralPath (Join-Path $vs 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt') -Raw).Trim()
        if($toolset -cnotmatch '^14\.44\.\d+$' -or -not (Test-Path -LiteralPath (Join-Path $vs "VC/Tools/MSVC/$toolset/bin/Hostx64/x64/cl.exe") -PathType Leaf)){throw 'Missing prerequisite'}
        $sdkRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
        foreach($file in @('Include/10.0.26100.0/um/Windows.h','Include/10.0.26100.0/um/UIAutomation.h','Lib/10.0.26100.0/um/x64/ole32.lib')){if(-not (Test-Path -LiteralPath (Join-Path $sdkRoot $file) -PathType Leaf)){throw 'Missing prerequisite'}}
        $native=Join-Path $output 'native'
        $configure=Invoke-ConfigureOnlyOperation 'NativeConfigure' $cmake @('-S',(Join-Path $repo 'tools/KuaiYi.ReuseLifecycleNative'),'-B',$native,'-G','Visual Studio 17 2022','-A','x64','-T',"v143,version=$toolset","-DCMAKE_GENERATOR_INSTANCE=$vs",'-DCMAKE_SYSTEM_VERSION=10.0.26100.0')
        if($configure.Exit -cne '0'){$summary.Reason='NativeConfigureFailed';throw 'Build failed'}
        # Terminal operation: successful configure never enters build or runtime.
        $summary.Status='Pass';$summary.Reason='Completed';$summary.ExitCode=0
    }
} catch { # No exception, original compiler/dotnet text or paths cross public streams.
} finally {
    if($null -ne $configureLease){try {$configureLease.Dispose()} catch {$configureDiagnostic=New-LifecycleConfigureDiagnostic 'LeaseCloseFailed'};$configureLease=$null}
    $summary.Operations=$operations.ToArray()
}
if($null -ne $configureAccountingReceipt){$configureAccountingOutputStatus=Write-LifecycleAccountingEvidence $configureAccountingReceipt}
if($null -ne $configureDiagnostic){try {Write-Host ('KUAIYI_CONFIGURE_DIAGNOSTIC '+($configureDiagnostic | ConvertTo-Json -Depth 8 -Compress))} catch {Write-Host 'KUAIYI_CONFIGURE_DIAGNOSTIC {"DiagnosticStatus":"ProjectionError","Files":[]}'}}
Write-Host ('KUAIYI_CONFIGURE_ONLY_SUMMARY '+($summary | ConvertTo-Json -Depth 8 -Compress))
exit $summary.ExitCode
