Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-runner-contracts.ps1"
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
Add-Type -Path "$PSScriptRoot/../../tools/ci/LifecycleOperation.cs"
$checks=0;$actualLookups=0;$missingLookups=0
function Check([bool]$Condition){if(-not $Condition){throw ('Full runner contract failed '+$script:checks)};$script:checks++}
function Reject([scriptblock]$Operation){$failed=$false;try{& $Operation | Out-Null}catch{$failed=$true};Check $failed}
$private='PRIVATE_SENTINEL_SYNTHETIC_sk';$base='b39a9076e69bec3c277e5664428ff142eaaef9f5';$locked='3ae69419a1152ea9694a3a9cf11130fbae645fa3';$tree='870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b'
function Candidate([string]$Path){return [pscustomobject]@{CommandType=[Management.Automation.CommandTypes]::Application;Source=$Path}}
foreach($name in @('git','cmake','dotnet')){
    $first=Candidate ('/fixture/'+$name+'-first');$second=Candidate ('/fixture/'+$name+'-second')
    $result=Select-LifecycleApplicationPath @($first,$second)
    Check ($result -is [string] -and $result -ceq $first.Source)
    Check ([KuaiYiLifecycleOperation]::Quote($result) -cne [KuaiYiLifecycleOperation]::Quote(@($first.Source,$second.Source)))
    $installed=@(Get-Command $name -CommandType Application -ErrorAction SilentlyContinue)
    if($installed.Count -gt 0){$actual=Get-LifecycleApplicationPath $name;Check ($actual -is [string] -and $actual -ceq $installed[0].Source);$actualLookups++}else{Reject {Get-LifecycleApplicationPath $name};$missingLookups++}
}
Reject {Select-LifecycleApplicationPath @()};Reject {Select-LifecycleApplicationPath @($null)}
foreach($bad in @([pscustomobject]@{CommandType='Alias';Source=$private},[pscustomobject]@{CommandType='Application';Source=@('a','b')},[pscustomobject]@{CommandType='Application';Source=''})){Reject {Select-LifecycleApplicationPath @($bad,(Candidate '/fixture/valid'))}}
Reject {Get-LifecycleApplicationPath $private}
foreach($end in @('',"`n","`r`n")){Check ((Read-LifecycleExactSha ($base+$end)) -ceq $base)}
foreach($bad in @($private,(" $base"),$base.ToUpperInvariant(),($base+"`n`n"),($base+"`r"),($base+'extra'),('x'*129),'')){Reject {Read-LifecycleExactSha $bad}}
foreach($end in @('',"`n","`r`n")){Check (Test-LifecycleSingleParent ($base+' '+$locked+$end) $base $locked)}
foreach($bad in @($base,($base+' '+$locked+' '+$tree),($base+' '+$tree),(' '+$base+' '+$locked),($base+' '+$locked+"`n`n"),$private)){Check (-not (Test-LifecycleSingleParent $bad $base $locked))}
Check (-not (Test-LifecycleSingleParent ($base+' '+$locked) $private $locked))
$runner=Get-Content -LiteralPath "$PSScriptRoot/run-lifecycle-windows-ci.ps1" -Raw
Check ($runner -notmatch '\(Get-Command (git|cmake|dotnet) -CommandType Application\)\.Source')
foreach($name in @('git','cmake','dotnet')){Check ($runner.Contains("Get-LifecycleApplicationPath '$name'"))}
foreach($kind in @('SourceParent','SourceAncestry','SourceLockedTree')){
    $receipt=[KuaiYiLifecycleOperation+Receipt]::new();$row=Get-LifecycleOperationEvidence $kind $receipt;Check ($row.Kind -ceq $kind)
    Check ($runner.Contains("'$kind'"))
}
Check ($runner.Contains("'LifecycleRuntime' "+'$dotnet @($dll,'+"'--supervise',"+'$exe)'))
Check ($runner -notmatch 'Install-|upload-artifact|KuaiYi\.ReuseLifecycle\.exe')
$workflow=Get-Content -LiteralPath "$PSScriptRoot/../../.github/workflows/windows-lifecycle-validation.yml" -Raw
Check ($workflow -match 'fetch-depth: 5' -and $workflow.Contains('ref: ${{ github.sha }}') -and $workflow -match 'timeout-minutes: 10' -and $workflow -match 'timeout-minutes: 8' -and $workflow -match 'runs-on: windows-2022')
Check ($workflow -match 'run-lifecycle-windows-ci.ps1' -and $workflow -notmatch 'run-source-head-windows-ci.ps1|rerun|upload-artifact')
Check ($workflow -match 'paths: \[.github/workflows/windows-lifecycle-validation.yml\]')
if(-not $IsWindows){
    $output=@(& (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/run-lifecycle-windows-ci.ps1" *>&1);Check ($LASTEXITCODE -eq 2 -and $output.Count -eq 1)
    $summary=([string]$output[0]).Substring('KUAIYI_LIFECYCLE_CI_SUMMARY '.Length) | ConvertFrom-Json
    Check ($summary.Status -ceq 'Blocked' -and $summary.Runtime -ceq 'NotRun' -and $summary.Operations.Count -eq 0 -and -not $summary.ParentVerified -and -not $summary.AncestorVerified -and -not $summary.LockedTreeVerified)
}
# Real local Git history and mutation rejection; all objects/files are in a new owned fixture.
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$temp=Join-Path $repo ('artifacts/full-runner-tests/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $temp)
$git=Get-LifecycleApplicationPath 'git';$log=Join-Path $temp 'git.local.log'
function GitCode([string[]]$Arguments){& $script:git @Arguments *> $script:log;return $LASTEXITCODE}
$bare=Join-Path $temp 'history.git';Check ((GitCode @('clone','--bare','--shared',$repo,$bare)) -eq 0)
Check ((GitCode @('-C',$bare,'-c','user.name=KuaiYiSynthetic','-c','user.email=fixture@example.invalid','commit-tree','2bcfa3dd1a23c0da085f9d5303d491354e668686','-p',$base,'-m','Full lifecycle checkout fixture')) -eq 0)
$future=(Get-Content -LiteralPath $log -Raw).Trim();Check ($future -cmatch '^[0-9a-f]{40}$');Check ((GitCode @('-C',$bare,'update-ref','refs/heads/future',$future)) -eq 0)
$uri=[Uri]::new($bare+[IO.Path]::DirectorySeparatorChar).AbsoluteUri
foreach($depth in @(4,5)){
    $clone=Join-Path $temp ('depth'+$depth);Check ((GitCode @('clone','--no-checkout','--depth',[string]$depth,'--single-branch','--branch','future',$uri,$clone)) -eq 0)
    Check ((GitCode @('-C',$clone,'rev-list','--parents','-n','1','HEAD')) -eq 0);Check (Test-LifecycleSingleParent (Get-Content -LiteralPath $log -Raw) $future $base)
    $code=GitCode @('-C',$clone,'merge-base','--is-ancestor',$locked,'HEAD')
    if($depth -eq 4){Check ($code -ne 0)}else{
        Check ($code -eq 0);Check ((GitCode @('-C',$clone,'rev-parse',($locked+'^{tree}'))) -eq 0);Check ((Read-LifecycleExactSha (Get-Content -LiteralPath $log -Raw)) -ceq $tree)
        Check ((GitCode (@('-C',$clone,'diff','--quiet',$locked,'HEAD','--')+(Get-LifecycleRuntimePaths))) -eq 0)
        Check ((GitCode @('-C',$clone,'checkout','--quiet','HEAD')) -eq 0)
        foreach($path in @('global.json','src/KuaiYi.Windows/app.manifest')){
            $file=Join-Path $clone $path;$bytes=[IO.File]::ReadAllBytes($file);[IO.File]::AppendAllText($file,"`nfixture mutation`n")
            Check ((GitCode (@('-C',$clone,'diff','--quiet',$locked,'--')+(Get-LifecycleRuntimePaths))) -eq 1)
            [IO.File]::WriteAllBytes($file,$bytes)
        }
        Check ((GitCode (@('-C',$clone,'diff','--quiet',$locked,'--')+(Get-LifecycleRuntimePaths))) -eq 0)
    }
}
Write-Host "FULL_LIFECYCLE_RUNNER_CONTRACTS passed=$checks failed=0; actualLookups=$actualLookups missingRejected=$missingLookups; local Git/pure checks only; Windows NotRun"
