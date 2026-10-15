Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$base='118981aead5dc230351efb8ff210963d87d5522c'
$old=(& git -C $repo show ($base+':tools/ci/LifecycleOperation.cs')) -join "`n"
$new=[IO.File]::ReadAllText((Join-Path $repo 'tools/ci/LifecycleOperation.cs'))
$out=Join-Path $repo ('artifacts/accounting-contracts/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $out)
$checks=0;$cases=0
function Check([bool]$Value){if(-not $Value){throw "Accounting projection contract failed $script:checks"};$script:checks++}
function Run-Source([string]$Text){$a=$Text.IndexOf('    public static Receipt Run(');$b=$Text.IndexOf('    private static bool Empty(',$a);return $Text.Substring($a,$b-$a).Trim()}
Check ((Run-Source $old) -ceq (Run-Source $new)) # Entire Run, short circuit, clock reads, rescue/finally unchanged.
Check ((([regex]::Matches($old,'\[DllImport[^\r\n]+')|ForEach-Object Value)-join "`n") -ceq (([regex]::Matches($new,'\[DllImport[^\r\n]+')|ForEach-Object Value)-join "`n"))
Check (([regex]::Matches($new,'QueryInformationJobObject\(')).Count -eq ([regex]::Matches($old,'QueryInformationJobObject\(')).Count)
function Prepare([string]$Text,[string]$TypeName){
    $Text=$Text.Replace('KuaiYiLifecycleOperation',$TypeName)
    $wait=[regex]::Match($Text,'r.Stage=OperationStage.WaitJobEmpty;[^\r\n]+\r?\n\s*bool empty=ObserveJobEmpty[^\r\n]+\r?\n\s*if\(CompleteJobWait[^\r\n]+').Value
    Check ($wait.Length -gt 0)
    $wait=$wait.Replace('Thread.Sleep(10)','Trace.Add("Sleep10")') # Virtual wait, no sleeping or original Run.
    $query=@'
    private static bool QueryInformationJobObject(IntPtr job,int kind,ref Accounting data,uint size,IntPtr returned) {
        string row=Rows.Dequeue();Trace.Add("Query:"+row);
        if(row=="T")throw new IOException("PRIVATE_SENTINEL");
        if(row=="F"){data.Active=999;data.Total=999;Marshal.SetLastPInvokeError(5);return false;}
        string[] parts=row.Split(':');data.Active=uint.Parse(parts[0]);data.Total=uint.Parse(parts[1]);return true;
    }
'@
    $pattern='(?m)^\s*\[DllImport\("kernel32.dll",SetLastError=true\)\] private static extern bool QueryInformationJobObject[^\r\n]+;'
    Check (([regex]::Matches($Text,$pattern)).Count -eq 1)
    $Text=[regex]::Replace($Text,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($m)$query})
    $testing=@'
    public static readonly List<string> Trace=new List<string>();
    private static Queue<string> Rows;private static long ClockValue;
    private sealed class TestClock {public long ElapsedMilliseconds{get{Trace.Add("Clock");return ClockValue;}}}
    public static void Reset(string rows,long elapsed){Rows=new Queue<string>(rows.Split(','));ClockValue=elapsed;Trace.Clear();}
    public static bool TestObserve(Receipt receipt){return ObserveJobEmpty(new IntPtr(1),receipt);}
    public static bool TestCleanup(){return Empty(new IntPtr(1));}
    public static Receipt TestWait() {
        var r=new Receipt{ProcessCreated=true,Resumed=true,RootExitConfirmed=true,Exit="0"};
        bool created=true;var clock=new TestClock();var job=new IntPtr(1);
        try { WAIT_BODY }
        catch(Exception error){RecordFailure(r,error);r.Status=r.Failure==FailureKind.Timeout?"Timeout":"Failure";r.RescueUsed=created;}
        return r;
    }
'@
    $testing=$testing.Replace('WAIT_BODY',$wait)
    $index=$Text.LastIndexOf('}');return $Text.Insert($index,$testing)
}
$oldFile=Join-Path $out 'Baseline.cs';$newFile=Join-Path $out 'Candidate.cs'
Prepare $old 'BaselineAccountingOperation'|Set-Content -LiteralPath $oldFile -Encoding utf8NoBOM
Prepare $new 'KuaiYiLifecycleOperation'|Set-Content -LiteralPath $newFile -Encoding utf8NoBOM
Add-Type -Path @($oldFile,$newFile)
$priorEvidence=(& git -C $repo show ($base+':tests/scripts/lifecycle-ci-evidence.ps1')) -join "`n"
$parseErrors=$null;$parseTokens=$null
$priorAst=[Management.Automation.Language.Parser]::ParseInput($priorEvidence,[ref]$parseTokens,[ref]$parseErrors)
$priorFunction=$priorAst.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Get-LifecycleOperationEvidence'},$true)
$currentEvidence=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'lifecycle-ci-evidence.ps1'))
$currentAst=[Management.Automation.Language.Parser]::ParseInput($currentEvidence,[ref]$parseTokens,[ref]$parseErrors)
$currentFunction=$currentAst.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Get-LifecycleOperationEvidence'},$true)
Check ($priorFunction.Extent.Text -ceq $currentFunction.Extent.Text)
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
$baselineProjection=${function:Get-LifecycleOperationEvidence}.ToString().Replace('KuaiYiLifecycleOperation','BaselineAccountingOperation')
Set-Item Function:Get-BaselineEvidence ([scriptblock]::Create($baselineProjection))
$unknown=[KuaiYiLifecycleOperation+Receipt]::new();$projection=Get-LifecycleAccountingEvidence $unknown
Check ($projection.FirstWait.State -ceq 'Unknown' -and $null -eq $projection.FirstWait.ActiveProcesses -and $null -eq $projection.LastPreRescue.TotalProcesses)
$traces=@()
foreach($case in @(
    @{Name='AlreadyEmpty';Rows='0:5,0:5';Clock=0;First=0;Last=0;Known=$true},
    @{Name='BecomesEmpty';Rows='1:5,0:6,0:6';Clock=0;First=1;Last=0;Known=$true},
    @{Name='Timeout';Rows='1:5,1:6';Clock=30000;First=1;Last=1;Known=$true},
    @{Name='FirstFails';Rows='F';Clock=0;First=$null;Last=$null;Known=$false},
    @{Name='LaterFails';Rows='1:5,F';Clock=0;First=1;Last=$null;Known=$false},
    @{Name='FinalFails';Rows='1:5,0:6,F';Clock=0;First=1;Last=$null;Known=$false},
    @{Name='QueryThrows';Rows='T';Clock=0;First=$null;Last=$null;Known=$false},
    @{Name='Uint32Counts';Rows='4294967295:4294967295,0:4294967295,0:4294967295';Clock=0;First=4294967295;Last=0;Known=$true}
)){
    $cases++
    [BaselineAccountingOperation]::Reset($case.Rows,$case.Clock);[KuaiYiLifecycleOperation]::Reset($case.Rows,$case.Clock)
    $a=[BaselineAccountingOperation]::TestWait();$b=[KuaiYiLifecycleOperation]::TestWait()
    $oldTrace=[BaselineAccountingOperation]::Trace -join ',';$newTrace=[KuaiYiLifecycleOperation]::Trace -join ','
    Check ($oldTrace -ceq $newTrace)
    Check ((Get-BaselineEvidence 'NativeConfigure' $a|ConvertTo-Json -Compress) -ceq (Get-LifecycleOperationEvidence 'NativeConfigure' $b|ConvertTo-Json -Compress))
    Check ([BaselineAccountingOperation]::GetReason($a).ToString() -ceq [KuaiYiLifecycleOperation]::GetReason($b).ToString())
    # Inject identical successful settlement to exercise meaningful old reasons, not only default cleanup failure.
    $a.JobEmpty=$true;$b.JobEmpty=$true;$a.CleanupConfirmed=$true;$b.CleanupConfirmed=$true
    $expected=if($b.Status -ceq 'Completed'){'Completed'}elseif($b.Status -ceq 'Timeout'){'OperationTimeout'}else{'OperationFailure'}
    Check ([BaselineAccountingOperation]::GetReason($a).ToString() -ceq $expected -and [KuaiYiLifecycleOperation]::GetReason($b).ToString() -ceq $expected)
    Check ((Get-BaselineEvidence 'NativeConfigure' $a|ConvertTo-Json -Compress) -ceq (Get-LifecycleOperationEvidence 'NativeConfigure' $b|ConvertTo-Json -Compress))
    $row=Get-LifecycleAccountingEvidence $b
    Check ($row.FirstWait.ActiveProcesses -eq $case.First -and $row.LastPreRescue.ActiveProcesses -eq $case.Last)
    Check (($row.LastPreRescue.State -ceq 'Known') -eq $case.Known)
    if($null -ne $case.First){Check ($row.FirstWait.TotalProcesses -eq $(if($case.Name -ceq 'Uint32Counts'){4294967295}else{5}))}else{Check ($row.FirstWait.State -ceq 'Unknown' -and $null -eq $row.FirstWait.TotalProcesses)}
    if($case.Name -cin @('FirstFails','LaterFails','FinalFails')){Check ($b.Failure.ToString() -ceq 'Win32' -and $b.Error.ToString() -ceq 'AccessDenied')}
    if($case.Name -ceq 'BecomesEmpty'){Check ($row.LastPreRescue.TotalProcesses -eq 6)}
    $saved=$row|ConvertTo-Json -Depth 4 -Compress
    Check ($saved.Length -lt 512 -and $saved -notmatch '999|PRIVATE|Exception')
    foreach($cleanup in @('0:99','7:99','F','T')){
        [BaselineAccountingOperation]::Reset($cleanup,0);[KuaiYiLifecycleOperation]::Reset($cleanup,0)
        $ra=$null;$rb=$null;$ea=$null;$eb=$null
        try{$ra=[BaselineAccountingOperation]::TestCleanup()}catch{$ea=$_.Exception.InnerException.GetType().FullName}
        try{$rb=[KuaiYiLifecycleOperation]::TestCleanup()}catch{$eb=$_.Exception.InnerException.GetType().FullName}
        Check ($ra -eq $rb -and $ea -ceq $eb -and ([BaselineAccountingOperation]::Trace -join ',') -ceq ([KuaiYiLifecycleOperation]::Trace -join ','))
        Check ((Get-LifecycleAccountingEvidence $b|ConvertTo-Json -Depth 4 -Compress) -ceq $saved)
    }
    $traces+=@{Case=$case.Name;Baseline=$oldTrace;Candidate=$newTrace;FirstWait=$row.FirstWait;LastPreRescue=$row.LastPreRescue}
}
# Exercise the actual runner output tail, replacing only process exit with an observed value.
$runner=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'run-configure-only-windows.ps1'))
$tail=$runner.Substring($runner.IndexOf('if($null -ne $configureAccountingReceipt){')).Replace('exit $summary.ExitCode','$script:observedExit=$summary.ExitCode')
Check ($tail.Contains('KUAIYI_CONFIGURE_ONLY_SUMMARY') -and $tail.Contains('Write-LifecycleAccountingEvidence'))
$hostRows=[Collections.Generic.List[string]]::new();$prefixCalls=0;$mode='Normal'
function Write-Host {param([string]$Object) if($Object.StartsWith('KUAIYI_JOB_ACCOUNTING ')){$script:prefixCalls++;if($script:mode -ceq 'PrefixThrows'){throw 'PRIVATE_SENTINEL'}};$script:hostRows.Add($Object)}
foreach($mode in @('Normal','PrefixThrows','InvalidReceipt')){
    $cases++;$hostRows.Clear();$prefixCalls=0;$configureAccountingReceipt=if($mode -ceq 'InvalidReceipt'){'PRIVATE_SENTINEL'}else{$unknown}
    $configureDiagnostic=$null;$summary=[pscustomobject]@{Status='Fail';Reason='OperationTimeout';ExitCode=3};$observedExit=$null
    Invoke-Expression $tail
    Check ($observedExit -eq 3 -and $summary.Reason -ceq 'OperationTimeout' -and $hostRows[$hostRows.Count-1].StartsWith('KUAIYI_CONFIGURE_ONLY_SUMMARY '))
    Check (($hostRows -join ',') -notmatch 'PRIVATE_SENTINEL|Exception')
    Check ($configureAccountingOutputStatus -ceq $(if($mode -ceq 'Normal'){'Projected'}else{'Unavailable'}))
    Check ($prefixCalls -eq $(if($mode -ceq 'InvalidReceipt'){0}else{1}))
}
$traces|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $out 'traces.json') -Encoding utf8NoBOM
[Console]::WriteLine("ACCOUNTING_PROJECTION passed=$checks scenarios=$cases failed=0; baseline/candidate existing-query wait traces only; original Run/native/Windows/CMake/FourCases NotRun")
