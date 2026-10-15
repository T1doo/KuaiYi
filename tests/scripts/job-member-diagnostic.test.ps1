Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$helper=Join-Path $repo 'tools/ci/JobMemberDiagnostic.cs'
$fake=@'
public sealed class JobMemberProbeFixture : KuaiYiJobMemberDiagnostic.IProbe
{
 public readonly List<string> Events=new List<string>();
 public ulong[] Ids={987654321};public uint Assigned=1,Count=1,Written=16;
 public int BufferSize=72,Error;public bool Success=true,NullList,NullBuffer,FailClose,ThrowClose;
 public string ImagePath="C:\\PRIVATE_SYNTHETIC_SENTINEL\\MSBuild.exe",ThrowAt="";
 public long OpenValue=long.MinValue;public long[] Budget={1000};public int BudgetCalls,ClockThrowsAt;
 public KuaiYiJobMemberDiagnostic.Verification MemberFirst=KuaiYiJobMemberDiagnostic.Verification.Yes,MemberSecond=KuaiYiJobMemberDiagnostic.Verification.Yes;
 public KuaiYiJobMemberDiagnostic.Verification RunningFirst=KuaiYiJobMemberDiagnostic.Verification.Yes,RunningSecond=KuaiYiJobMemberDiagnostic.Verification.Yes;
 private readonly Dictionary<IntPtr,int> members=new Dictionary<IntPtr,int>(),runs=new Dictionary<IntPtr,int>();
 public long Remaining(){BudgetCalls++;if(BudgetCalls==ClockThrowsAt)throw new Exception("PRIVATE_SYNTHETIC_SENTINEL");return Budget[Math.Min(BudgetCalls-1,Budget.Length-1)];}
 private void Event(string name){Events.Add(name);if(ThrowAt==name)throw new Exception("PRIVATE_SYNTHETIC_SENTINEL");}
 public KuaiYiJobMemberDiagnostic.MemberList ReadList(IntPtr job)
 {
  Event("List");if(job!=new IntPtr(1234))throw new Exception();if(NullList)return null;
  byte[] bytes=NullBuffer?null:new byte[BufferSize];
  if(bytes!=null&&bytes.Length>=8){Array.Copy(BitConverter.GetBytes(Assigned),0,bytes,0,4);Array.Copy(BitConverter.GetBytes(Count),0,bytes,4,4);for(int i=0;i<Ids.Length&&8+i*8+8<=bytes.Length;i++)Array.Copy(BitConverter.GetBytes(Ids[i]),0,bytes,8+i*8,8);}
  return new KuaiYiJobMemberDiagnostic.MemberList{Success=Success,Error=Error,WrittenBytes=Written,Buffer=bytes};
 }
 public IntPtr Open(uint id){Event("Open");if(Array.IndexOf(Ids,(ulong)id)<0)throw new Exception();return new IntPtr(OpenValue==long.MinValue?10000+(long)id:OpenValue);}
 public KuaiYiJobMemberDiagnostic.Verification IsMember(IntPtr process,IntPtr job)
 {int n=members.ContainsKey(process)?members[process]+1:1;members[process]=n;Event("Member"+n);if(job!=new IntPtr(1234))throw new Exception();return n==1?MemberFirst:MemberSecond;}
 public KuaiYiJobMemberDiagnostic.Verification IsRunning(IntPtr process)
 {int n=runs.ContainsKey(process)?runs[process]+1:1;runs[process]=n;Event("Running"+n);return n==1?RunningFirst:RunningSecond;}
 public string Image(IntPtr process){Event("Image");return ImagePath;}
 public bool Close(IntPtr handle){Events.Add("Close");if(handle==new IntPtr(1234))throw new Exception("Owner job closed");if(ThrowClose)throw new Exception("PRIVATE_SYNTHETIC_SENTINEL");return !FailClose;}
}
'@
Add-Type -TypeDefinition (([IO.File]::ReadAllText($helper))+[Environment]::NewLine+$fake)
Add-Type -Path (Join-Path $repo 'tools/ci/LifecycleOperation.cs')
$checks=0;$scenarios=0
function Check([bool]$Value){if(-not $Value){throw "Job member diagnostic contract failed $script:checks"};$script:checks++}
$original=[KuaiYiLifecycleOperation+Receipt]::new()
$original.Status='Timeout';$original.Exit='0';$original.Stage=[KuaiYiLifecycleOperation+OperationStage]::WaitJobEmpty
$original.Failure=[KuaiYiLifecycleOperation+FailureKind]::Timeout;$original.RootExitConfirmed=$true;$original.JobEmpty=$true
$original.RescueUsed=$true;$original.CleanupConfirmed=$true;$original.PreRescueJobState=[KuaiYiLifecycleOperation+JobState]::NonEmpty;$original.BudgetExpired=$true
$before=$original|ConvertTo-Json -Compress
$keys=@('Status','Listed','MSBuild','PdbServer','Compiler','Linker','CMake','Other','OpenUnavailable','MembershipUnconfirmed','RunningUnconfirmed','ImageUnavailable','DiagnosticHandlesClosed')
function Capture($Fake,[string]$Expected,[IntPtr]$Job=[IntPtr]1234){
    $script:scenarios++
    $row=[KuaiYiJobMemberDiagnostic]::Capture($Job,[Func[long]]{$Fake.Remaining()},$Fake)
    Check ($row.State.ToString() -ceq $Expected)
    $json=$row.PublicJson();$public=$json|ConvertFrom-Json -AsHashtable
    Check ($public.Count -eq $keys.Count -and @(Compare-Object @($public.Keys) $keys).Count -eq 0)
    Check ($json.Length -lt 512 -and $json -notmatch 'PRIVATE|987654321|Exception|[\\/]')
    Check (($original|ConvertTo-Json -Compress) -ceq $before)
    Check ([KuaiYiLifecycleOperation]::GetReason($original).ToString() -ceq 'OperationTimeout')
    return $row
}
foreach($pair in @(@('MSBuild.exe','MSBuild'),@('mspdbsrv.exe','PdbServer'),@('cl.exe','Compiler'),@('link.exe','Linker'),@('cmake.exe','CMake'),@('dotnet.exe','Other'),@('unrecognized.exe','Other'))){
    $f=[JobMemberProbeFixture]::new();$f.ImagePath='C:\PRIVATE_SYNTHETIC_SENTINEL\'+$pair[0]
    $r=Capture $f 'Observed';Check ($r.($pair[1]) -eq 1 -and $r.Listed -eq 1 -and $r.DiagnosticHandlesClosed)
    Check (($f.Events -join ',') -ceq 'List,Open,Member1,Running1,Image,Member2,Running2,Close')
}
foreach($path in @('msbuild.exe','C:\private\',"C:\private\MSBuild.exe`0",'C:\private\"MSBuild.exe',('C:\'+('x'*1024)))){
    $f=[JobMemberProbeFixture]::new();$f.ImagePath=$path;$r=Capture $f 'Partial'
    Check ($r.ImageUnavailable -eq 1 -and $r.MSBuild -eq 0 -and @($f.Events|Where-Object {$_ -ceq 'Close'}).Count -eq 1)
}
$f=[JobMemberProbeFixture]::new();$r=Capture $f 'NotAttempted' ([IntPtr]::Zero);Check ($f.Events.Count -eq 0 -and $f.BudgetCalls -eq 0)
$f=[JobMemberProbeFixture]::new();$r=Capture $f 'NotAttempted' ([IntPtr](-1));Check ($f.Events.Count -eq 0)
foreach($remaining in @(-1,0,250)){$f=[JobMemberProbeFixture]::new();$f.Budget=@($remaining);$r=Capture $f 'SkippedBudget';Check ($f.Events.Count -eq 0)}
foreach($scenario in @('ApiFailure','CapacityFailure','NullList','NullBuffer','ShortBuffer','LongBuffer','HeaderShort','WrittenLong','EntriesShort','AssignedLess','AssignedMore','OverCapacity','ZeroPid','LargePid','DuplicatePid')){
    $f=[JobMemberProbeFixture]::new();$expected='InvalidList'
    switch($scenario){
        ApiFailure {$f.Success=$false;$f.Error=5;$expected='ListUnavailable'}
        CapacityFailure {$f.Success=$false;$f.Error=234;$expected='CapacityExceeded'}
        NullList {$f.NullList=$true;$expected='ListUnavailable'}
        NullBuffer {$f.NullBuffer=$true}
        ShortBuffer {$f.BufferSize=71}
        LongBuffer {$f.BufferSize=73}
        HeaderShort {$f.Written=7}
        WrittenLong {$f.Written=73}
        EntriesShort {$f.Written=15}
        AssignedLess {$f.Count=2}
        AssignedMore {$f.Assigned=2;$expected='CapacityExceeded'}
        OverCapacity {$f.Assigned=9;$f.Count=9;$expected='CapacityExceeded'}
        ZeroPid {$f.Ids=@(0)}
        LargePid {$f.Ids=@([ulong]4294967296)}
        DuplicatePid {$f.Assigned=2;$f.Count=2;$f.Written=24;$f.Ids=@(42,42)}
    }
    $r=Capture $f $expected;Check (($f.Events -join ',') -ceq 'List' -and $r.Listed -eq -1)
}
foreach($open in @(0,-1)){$f=[JobMemberProbeFixture]::new();$f.OpenValue=$open;$r=Capture $f 'Partial';Check ($r.OpenUnavailable -eq 1 -and ($f.Events -join ',') -ceq 'List,Open')}
$f=[JobMemberProbeFixture]::new();$f.OpenValue=1234;$r=Capture $f 'ReaderFailure';Check (($f.Events -join ',') -ceq 'List,Open')
foreach($state in @('No','Unknown')){
    $f=[JobMemberProbeFixture]::new();$f.MemberFirst=[Enum]::Parse([KuaiYiJobMemberDiagnostic+Verification],$state)
    $r=Capture $f 'Partial';Check ($r.MembershipUnconfirmed -eq 1 -and ($f.Events -join ',') -ceq 'List,Open,Member1,Close')
    $f=[JobMemberProbeFixture]::new();$f.RunningFirst=[Enum]::Parse([KuaiYiJobMemberDiagnostic+Verification],$state)
    $r=Capture $f 'Partial';Check ($r.RunningUnconfirmed -eq 1 -and ($f.Events -join ',') -ceq 'List,Open,Member1,Running1,Close')
    $f=[JobMemberProbeFixture]::new();$f.MemberSecond=[Enum]::Parse([KuaiYiJobMemberDiagnostic+Verification],$state)
    $r=Capture $f 'Partial';Check ($r.MembershipUnconfirmed -eq 1 -and $r.MSBuild -eq 0 -and ($f.Events -join ',').EndsWith('Image,Member2,Close'))
    $f=[JobMemberProbeFixture]::new();$f.RunningSecond=[Enum]::Parse([KuaiYiJobMemberDiagnostic+Verification],$state)
    $r=Capture $f 'Partial';Check ($r.RunningUnconfirmed -eq 1 -and $r.MSBuild -eq 0 -and ($f.Events -join ',').EndsWith('Member2,Running2,Close'))
}
foreach($stage in @('List','Open','Member1','Running1','Image','Member2','Running2')){
    $f=[JobMemberProbeFixture]::new();$f.ThrowAt=$stage;$r=Capture $f 'ReaderFailure'
    Check ($r.Listed -eq -1 -and $r.MSBuild -eq 0)
    Check (@($f.Events|Where-Object {$_ -ceq 'Close'}).Count -eq $(if($stage -cin @('List','Open')){0}else{1}))
}
foreach($scenario in @('CloseFalse','CloseThrows','RejectedThenCloseFalse')){
    $f=[JobMemberProbeFixture]::new();$f.Ids=@(42,43);$f.Assigned=2;$f.Count=2;$f.Written=24
    if($scenario -ceq 'CloseThrows'){$f.ThrowClose=$true}else{$f.FailClose=$true}
    if($scenario -ceq 'RejectedThenCloseFalse'){$f.MemberFirst=[KuaiYiJobMemberDiagnostic+Verification]::No}
    $r=Capture $f 'HandleCloseUnknown';Check (-not $r.DiagnosticHandlesClosed -and $r.Listed -eq -1 -and $r.MSBuild -eq 0)
    Check (@($f.Events|Where-Object {$_ -ceq 'Open'}).Count -eq 1 -and @($f.Events|Where-Object {$_ -ceq 'Close'}).Count -eq 1)
}
foreach($point in @(2,3,6,8)){
    $f=[JobMemberProbeFixture]::new();$f.Budget=[long[]](@(1000)*($point-1)+@(250))
    $r=Capture $f 'SkippedBudget';Check ($r.Listed -eq -1 -and $r.MSBuild -eq 0)
    Check (@($f.Events|Where-Object {$_ -ceq 'Close'}).Count -eq $(if($point -eq 2){0}else{1}))
    if($point -eq 6){Check (($f.Events -join ',') -ceq 'List,Open,Member1,Running1,Image,Close')}
}
$f=[JobMemberProbeFixture]::new();$f.ClockThrowsAt=3;$r=Capture $f 'ReaderFailure';Check (($f.Events -join ',') -ceq 'List,Open,Close')
$f=[JobMemberProbeFixture]::new();$f.Ids=@();$f.Count=0;$f.Assigned=0;$f.Written=8;$r=Capture $f 'Observed';Check ($r.Listed -eq 0 -and ($f.Events -join ',') -ceq 'List')
$f=[JobMemberProbeFixture]::new();$f.Ids=@(1..8);$f.Count=8;$f.Assigned=8;$f.Written=72;$r=Capture $f 'Observed';Check ($r.MSBuild -eq 8 -and $f.Events.Count -eq 57 -and $f.BudgetCalls -eq 50)
$examples=@()
foreach($pair in @(@('MSBuildImage','MSBuild.exe'),@('PdbServerImage','mspdbsrv.exe'),@('OtherImage','dotnet.exe'))){
    $f=[JobMemberProbeFixture]::new();$f.ImagePath='C:\PRIVATE_SYNTHETIC_SENTINEL\'+$pair[1]
    $r=Capture $f 'Observed'
    $examples+=[pscustomobject]@{Case=$pair[0];Scope='SyntheticInjectedOnly';OriginalAccountingActive=1;OriginalReason='OperationTimeout';Observation=($r.PublicJson()|ConvertFrom-Json)}
}
Check ($examples[0].Observation.MSBuild -eq 1 -and $examples[1].Observation.PdbServer -eq 1 -and $examples[2].Observation.Other -eq 1)
Check (@($examples|Where-Object {$_.OriginalAccountingActive -eq 1 -and $_.OriginalReason -ceq 'OperationTimeout'}).Count -eq 3)
foreach($example in $examples){Write-Host ('JOB_MEMBER_COUNTEREXAMPLE '+($example|ConvertTo-Json -Depth 4 -Compress))}
Check ([KuaiYiJobMemberDiagnostic]::Classify('\\?\C:\private\mSpDbSrV.ExE').ToString() -ceq 'PdbServer')
Check ([KuaiYiJobMemberDiagnostic]::Classify('C:\private\dotnet.exe').ToString() -ceq 'Other')
$x64=[Runtime.InteropServices.Architecture]::X64;$x86=[Runtime.InteropServices.Architecture]::X86;$arm=[Runtime.InteropServices.Architecture]::Arm64
Check ([KuaiYiJobMemberDiagnostic]::SupportsNativeLayout($true,8,$x64,$x64))
Check (-not [KuaiYiJobMemberDiagnostic]::SupportsNativeLayout($false,8,$x64,$x64))
Check (-not [KuaiYiJobMemberDiagnostic]::SupportsNativeLayout($true,4,$x86,$x64))
Check (-not [KuaiYiJobMemberDiagnostic]::SupportsNativeLayout($true,4,$x64,$x64))
Check (-not [KuaiYiJobMemberDiagnostic]::SupportsNativeLayout($true,8,$arm,$x64))
Check (-not [KuaiYiJobMemberDiagnostic]::SupportsNativeLayout($true,8,$x64,$arm))
if(-not $IsWindows){$r=[KuaiYiJobMemberDiagnostic]::CaptureWindows([IntPtr]1234,[Diagnostics.Stopwatch]::StartNew());Check ($r.State.ToString() -ceq 'Unsupported' -and $r.Listed -eq -1)}
$source=[IO.File]::ReadAllText($helper)
Check ($source -notmatch 'DuplicateHandle|Terminate|SetInformationJobObject|OpenJobObject|CreateJobObject|GetProcesses|Toolhelp|AdjustTokenPrivileges|ProcessStart|Thread.Sleep')
Check ($source.Contains('OpenProcess(0x101000,false,id)') -and $source.Contains('WaitForSingleObject(process,0)') -and $source.Contains('QueryInformationJobObject(queryJob,3,bytes,ListBytes'))
Check ($source.Contains('Guard(remaining);MemberList list=api.ReadList(ownedJob)'))
Write-Host "JOB_MEMBER_DIAGNOSTIC passed=$checks scenarios=$scenarios failed=0; injected job/process API only; Windows/CMake/FourCases NotRun"
