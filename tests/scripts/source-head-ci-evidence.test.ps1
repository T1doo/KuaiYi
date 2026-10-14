Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/source-head-ci-evidence.ps1"
# Compile the existing interop definition only; never call Run or any native import.
Add-Type -Path "$PSScriptRoot/../../tools/ci/LifecycleOperation.cs"
$checks=0
function Check([bool]$Condition){if(-not $Condition){throw ('SourceHead contract failed '+$script:checks)};$script:checks++}
$sha='4bc4ee3ea471ed1dc51f83c8d04827ad02f92ed2';$private='PRIVATE_SENTINEL_SYNTHETIC_sk'
function ValidOperation {
    $r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Status='Completed';$r.Exit='0';$r.Stage=[KuaiYiLifecycleOperation+OperationStage]::Completed;$r.ProcessCreated=$true;$r.Resumed=$true;$r.RootExitConfirmed=$true;$r.JobEmpty=$true;$r.CleanupConfirmed=$true
    return (Get-LifecycleOperationEvidence 'SourceHead' $r)
}
$op=ValidOperation
foreach($ending in @('',"`n","`r`n")){Check ((Get-SourceHeadOutcome $op ($sha+$ending) $sha) -ceq 'SourceHeadSucceeded')}
foreach($bad in @($private,(" $sha"),$sha.ToUpperInvariant(),("$sha`n$sha"),($sha+"`r"),($sha+"`n`n"),('x'*4097),'')){Check ((Get-SourceHeadOutcome $op $bad $sha) -ceq 'InvalidGitOutput')}
Check ((Get-SourceHeadOutcome $op ('0'*40) $sha) -ceq 'SourceMismatch')
Check ((Get-SourceHeadOutcome $op $sha $private) -ceq 'InvalidExpectedSource')
foreach($field in @('ProcessCreated','Resumed','RootExitConfirmed','JobEmpty','CleanupConfirmed')){$bad=ValidOperation;$bad.$field=$false;Check ((Get-SourceHeadOutcome $bad $sha $sha) -ceq 'InvalidOperationReceipt')}
$bad=ValidOperation;$bad.RescueUsed=$true;Check ((Get-SourceHeadOutcome $bad $sha $sha) -ceq 'InvalidOperationReceipt')
foreach($field in @('Kind','Status','Stage','FailureKind','ErrorCode')){$bad=ValidOperation;$bad.$field=$private;Check ((Get-SourceHeadOutcome $bad $sha $sha) -ceq 'InvalidOperationReceipt')}
foreach($exit in @('1','2','3','Other','Unknown')){$bad=ValidOperation;$bad.Exit=$exit;Check ((Get-SourceHeadOutcome $bad $sha $sha) -ceq 'CommandExitNonzero')}
foreach($pair in @(@([Management.Automation.CommandNotFoundException]::new($private),'CommandMissing'),@([IO.IOException]::new($private),'Io'),@([ArgumentException]::new($private),'Argument'),@([DllNotFoundException]::new($private),'LibraryMissing'),@([EntryPointNotFoundException]::new($private),'EntryMissing'),@([InvalidOperationException]::new($private),'Other'))){Check ((Get-SourceHeadPreparationError $pair[0]) -ceq $pair[1])}
$script=Get-Content -LiteralPath "$PSScriptRoot/run-source-head-windows-ci.ps1" -Raw
$tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseInput($script,[ref]$tokens,[ref]$errors);Check ($errors.Count -eq 0)
$runs=@($ast.FindAll({param($node)$node -is [Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Member.Value -ceq 'Run'},$true));Check ($runs.Count -eq 1)
Check ($script -notmatch 'SourceCompare|dotnet|cmake|--supervise|Start-Process|Install-|upload-artifact')
Check ($script.Contains("@('-C',"+'$repo,'+"'rev-parse','HEAD')"))
Check ($script.Contains('Get-Command git -CommandType Application | Select-Object -First 1'))
# Real binder reproduction of the old ambiguous lookup without executing a native operation.
Check ([KuaiYiLifecycleOperation]::Quote(@('fixture-a','fixture-b')) -ceq '"fixture-a fixture-b"')
$workflow=Get-Content -LiteralPath "$PSScriptRoot/../../.github/workflows/windows-contracts.yml" -Raw
Check ($workflow -match 'fetch-depth: 4' -and $workflow.Contains('ref: ${{ github.sha }}') -and $workflow -match 'timeout-minutes: 10' -and $workflow -match 'runs-on: windows-2022')
Check ($workflow -match 'run-source-head-windows-ci.ps1' -and $workflow -notmatch 'run-lifecycle-windows-ci.ps1|rerun|upload-artifact')
if(-not $IsWindows){
    $output=@(& (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/run-source-head-windows-ci.ps1" *>&1)
    Check ($LASTEXITCODE -eq 2 -and $output.Count -eq 1)
    $summary=([string]$output[0]).Substring('KUAIYI_SOURCE_HEAD_CI_SUMMARY '.Length) | ConvertFrom-Json
    Check ($summary.Status -ceq 'Blocked' -and $summary.Reason -ceq 'HostedWindowsRequired' -and $summary.Operations.Count -eq 0 -and $summary.SourceCommit -eq $null)
    Check ($summary.NativeCompile -ceq 'NotRun' -and $summary.ManagedCompile -ceq 'NotRun' -and $summary.LifecycleCases -ceq 'NotRun' -and $summary.ModelCalls -eq 0)
}
# Real local Git shallow-checkout experiment; fixed synthetic commit is confined to a new bare fixture.
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$temp=Join-Path $repo ('artifacts/source-head-tests/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $temp)
$git=(Get-Command git -CommandType Application | Select-Object -First 1).Source;$log=Join-Path $temp 'git.local.log'
function GitCode([string[]]$Arguments){& $script:git @Arguments *> $script:log;return $LASTEXITCODE}
$bare=Join-Path $temp 'history.git';Check ((GitCode @('clone','--bare','--shared',$repo,$bare)) -eq 0)
Check ((GitCode @('-C',$bare,'-c','user.name=KuaiYiSynthetic','-c','user.email=fixture@example.invalid','commit-tree','ac8a07e77e1d74b314b3091028ec6fab61ba3b61','-p',$sha,'-m','SourceHead checkout fixture')) -eq 0)
$future=(Get-Content -LiteralPath $log -Raw).Trim();Check ($future -cmatch '^[0-9a-f]{40}$')
Check ((GitCode @('-C',$bare,'update-ref','refs/heads/future',$future)) -eq 0)
$uri=[Uri]::new($bare+[IO.Path]::DirectorySeparatorChar).AbsoluteUri
foreach($depth in @(3,4)){
    $clone=Join-Path $temp ('depth'+$depth)
    Check ((GitCode @('clone','--no-checkout','--depth',[string]$depth,'--single-branch','--branch','future',$uri,$clone)) -eq 0)
    Check ((GitCode @('-C',$clone,'rev-parse','HEAD')) -eq 0);Check ((Get-Content -LiteralPath $log -Raw).Trim() -ceq $future)
    $code=GitCode @('-C',$clone,'cat-file','-e','3ae69419a1152ea9694a3a9cf11130fbae645fa3^{commit}')
    if($depth -eq 3){Check ($code -ne 0)}else{Check ($code -eq 0);Check ((GitCode @('-C',$clone,'diff','--quiet','3ae69419a1152ea9694a3a9cf11130fbae645fa3','HEAD','--','tools/KuaiYi.ReuseLifecycle','tools/KuaiYi.ReuseLifecycleNative')) -eq 0)}
}
Write-Host "SOURCE_HEAD_CONTRACTS passed=$checks failed=0; local Git + pure classification; Windows NotRun"
