Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$locked='3ae69419a1152ea9694a3a9cf11130fbae645fa3'
$summary=[pscustomobject][ordered]@{Kind='NonTargetLifecycleCiSummary';SourceCommit=$null;LockedSource=$locked;LockedTree='870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b';SourceEquivalent=$false;Scope='FourFixedNonTargetCasesOnly';Status='Fail';Reason='RunnerFailure';Operations=@();CompilerFaults=@();Cases=@();Runtime='NotRun';UiaText='NotRun';Match='NotRun';Performance='NotRun';ProductionReuse='Off';ModelCalls=0;ExitCode=3}
$output=$null;$runtimeLog=$null;$allLogs=[Collections.Generic.List[string]]::new();$operations=[Collections.Generic.List[object]]::new()
function Invoke-BoundedLifecycleOperation([string]$Kind,[string]$Executable,[string[]]$Arguments) {
    $log=Join-Path $script:output ($Kind+'.local.log');$script:allLogs.Add($log);if($Kind -ceq 'LifecycleRuntime'){$script:runtimeLog=$log}
    $receipt=[KuaiYiLifecycleOperation]::Run($Executable,$Arguments,$script:repo,$log)
    $script:operations.Add([pscustomobject][ordered]@{Kind=$Kind;Status=$receipt.Status;Exit=$receipt.Exit;RootExitConfirmed=$receipt.RootExitConfirmed;JobEmpty=$receipt.JobEmpty;RescueUsed=$receipt.RescueUsed;CleanupConfirmed=$receipt.CleanupConfirmed})
    if($receipt.Status -cne 'Completed' -or $receipt.RescueUsed -or -not $receipt.CleanupConfirmed){$script:summary.Reason='OperationTimeoutOrCleanupFailure';throw 'Operation failed'}
    return [pscustomobject]@{Exit=$receipt.Exit;Log=$log}
}
try {
    if(-not $IsWindows -or $env:CI -cne 'true'){$summary.Status='Blocked';$summary.Reason='HostedWindowsRequired';$summary.ExitCode=2}
    else {
        if($env:GITHUB_SHA -cnotmatch '^[0-9a-f]{40}$'){throw 'Invalid source'}
        if([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -cne 'X64'){throw 'Invalid architecture'}
        $output=Join-Path $repo ('artifacts/lifecycle-ci/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $output)
        Add-Type -Path (Join-Path $repo 'tools/ci/LifecycleOperation.cs') 2>$null 3>$null 4>$null 5>$null 6>$null
        $git=(Get-Command git -CommandType Application).Source
        $head=Invoke-BoundedLifecycleOperation 'SourceHead' $git @('-C',$repo,'rev-parse','HEAD')
        $commit=(Get-Content -LiteralPath $head.Log -Raw).Trim()
        if($head.Exit -cne '0' -or $commit -cnotmatch '^[0-9a-f]{40}$' -or $commit -cne $env:GITHUB_SHA){throw 'Invalid source'};$summary.SourceCommit=$commit
        $state=Invoke-BoundedLifecycleOperation 'SourceStatus' $git @('-C',$repo,'status','--porcelain','--untracked-files=no')
        if($state.Exit -cne '0' -or (Get-Item -LiteralPath $state.Log).Length -ne 0){throw 'Invalid source'}
        # Exact locked source subtrees and shared inputs remain unchanged, despite the workflow commit.
        $compare=Invoke-BoundedLifecycleOperation 'SourceCompare' $git @('-C',$repo,'diff','--quiet',$locked,'HEAD','--','tools/KuaiYi.ReuseLifecycle','tools/KuaiYi.ReuseLifecycleNative','tools/KuaiYi.SelectionReuseDiagnostic/ExclusiveKillJob.cs','tools/KuaiYi.SelectionReuseNative/RequestLifetime.h','src/KuaiYi.Windows/app.manifest','global.json','Directory.Build.props','Directory.Build.targets')
        if($compare.Exit -cne '0') {throw 'Source changed'};$summary.SourceEquivalent=$true
        $cmake=(Get-Command cmake -CommandType Application).Source;$dotnet=(Get-Command dotnet -CommandType Application).Source
        $version=Invoke-BoundedLifecycleOperation 'CMakeVersion' $cmake @('--version')
        $line=Get-Content -LiteralPath $version.Log -TotalCount 1
        if($version.Exit -cne '0' -or $line -cnotmatch '^cmake version (\d+\.\d+\.\d+)$' -or [version]$Matches[1] -lt [version]'3.28.0'){throw 'Missing prerequisite'}
        $sdk=Invoke-BoundedLifecycleOperation 'DotnetVersion' $dotnet @('--version')
        if($sdk.Exit -cne '0' -or (Get-Content -LiteralPath $sdk.Log -Raw).Trim() -cne '10.0.401'){throw 'Missing prerequisite'}
        $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $vsResult=Invoke-BoundedLifecycleOperation 'VisualStudioInventory' $vswhere @('-products','*','-version','[17.0,18.0)','-requires','Microsoft.VisualStudio.Component.VC.Tools.x86.x64','-format','json','-utf8')
        if($vsResult.Exit -cne '0' -or (Get-Item -LiteralPath $vsResult.Log).Length -gt 1048576){throw 'Missing prerequisite'}
        $instances=@(Get-Content -LiteralPath $vsResult.Log -Raw | ConvertFrom-Json);if($instances.Count -ne 1){throw 'Missing prerequisite'}
        $vs=$instances[0].installationPath;$toolset=(Get-Content -LiteralPath (Join-Path $vs 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt') -Raw).Trim()
        if($toolset -cnotmatch '^14\.44\.\d+$' -or -not (Test-Path -LiteralPath (Join-Path $vs "VC/Tools/MSVC/$toolset/bin/Hostx64/x64/cl.exe") -PathType Leaf)){throw 'Missing prerequisite'}
        $sdkRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
        foreach($file in @('Include/10.0.26100.0/um/Windows.h','Include/10.0.26100.0/um/UIAutomation.h','Lib/10.0.26100.0/um/x64/ole32.lib')){if(-not (Test-Path -LiteralPath (Join-Path $sdkRoot $file) -PathType Leaf)){throw 'Missing prerequisite'}}
        $native=Join-Path $output 'native';$managed=Join-Path $output 'managed';$empty=Join-Path $output 'empty';[void](New-Item -ItemType Directory -Path $empty)
        $configure=Invoke-BoundedLifecycleOperation 'NativeConfigure' $cmake @('-S',(Join-Path $repo 'tools/KuaiYi.ReuseLifecycleNative'),'-B',$native,'-G','Visual Studio 17 2022','-A','x64','-T',"v143,version=$toolset","-DCMAKE_GENERATOR_INSTANCE=$vs",'-DCMAKE_SYSTEM_VERSION=10.0.26100.0')
        if($configure.Exit -cne '0'){$summary.Reason='NativeConfigureFailed';throw 'Build failed'}
        $build=Invoke-BoundedLifecycleOperation 'NativeBuild' $cmake @('--build',$native,'--config','Release','--parallel','2','--','/nodeReuse:false')
        if($build.Exit -cne '0'){$summary.Reason='NativeBuildFailed';throw 'Build failed'}
        $exe=Join-Path $native 'Release/KuaiYi.ReuseLifecycleNative.exe';if(-not (Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Missing binary'}
        $project=Join-Path $repo 'tools/KuaiYi.ReuseLifecycle/KuaiYi.ReuseLifecycle.csproj'
        $restore=Invoke-BoundedLifecycleOperation 'ManagedRestore' $dotnet @('restore',$project,'--locked-mode','--source',$empty,'-p:NuGetAudit=false','-m:1','-nr:false','-v','quiet')
        if($restore.Exit -cne '0'){$summary.Reason='ManagedRestoreFailed';throw 'Build failed'}
        $managedBuild=Invoke-BoundedLifecycleOperation 'ManagedBuild' $dotnet @('build',$project,'--no-restore','-c','Release','-o',$managed,'-m:1','-nr:false','-p:UseSharedCompilation=false','-v','quiet')
        if($managedBuild.Exit -cne '0'){$summary.Reason='ManagedBuildFailed';throw 'Build failed'}
        $dll=Join-Path $managed 'KuaiYi.ReuseLifecycle.dll';if(-not (Test-Path -LiteralPath $dll -PathType Leaf)){throw 'Missing binary'}
        $summary.Runtime='InProgress'
        $runtime=Invoke-BoundedLifecycleOperation 'LifecycleRuntime' $dotnet @($dll,'--supervise',$exe)
        if((Get-Item -LiteralPath $runtime.Log).Length -gt 4194304){throw 'Invalid receipt'}
        $receipt=Read-LifecyclePublicCases (Get-Content -LiteralPath $runtime.Log -Raw);$summary.Cases=$receipt.Cases;$summary.Runtime='Fail'
        if($runtime.Exit -cne '0' -or $receipt.Unsettled -or $receipt.Cases.Count -ne 4 -or @($receipt.Cases | Where-Object {-not (Test-LifecycleCasePass $_)}).Count -ne 0){$summary.Reason='LifecycleFailedOrReceiptMissing';throw 'Lifecycle failed'}
        $summary.Runtime='Pass';$summary.Status='Pass';$summary.Reason='Completed';$summary.ExitCode=0
    }
} catch { # No exception, original compiler/dotnet text or paths cross public streams.
} finally {
    if($summary.Runtime -ceq 'InProgress'){$summary.Runtime='Fail'}
    if($runtimeLog -and (Test-Path -LiteralPath $runtimeLog -PathType Leaf) -and (Get-Item -LiteralPath $runtimeLog).Length -le 4194304){try{$partial=Read-LifecyclePublicCases (Get-Content -LiteralPath $runtimeLog -Raw);$summary.Cases=$partial.Cases}catch{}}
    $summary.Operations=$operations.ToArray()
    try{$faults=[Collections.Generic.List[object]]::new();foreach($log in $allLogs){if((Test-Path -LiteralPath $log -PathType Leaf) -and (Get-Item -LiteralPath $log).Length -le 4194304){foreach($row in @(Get-LifecycleBuildEvidence (Get-Content -LiteralPath $log -Raw))){if($faults.Count -lt 16){$faults.Add($row)}}}};$summary.CompilerFaults=$faults.ToArray()}catch{$summary.Status='Fail';$summary.Reason='EvidenceFailure';$summary.ExitCode=3}
}
Write-Host ('KUAIYI_LIFECYCLE_CI_SUMMARY '+($summary | ConvertTo-Json -Depth 8 -Compress))
exit $summary.ExitCode
