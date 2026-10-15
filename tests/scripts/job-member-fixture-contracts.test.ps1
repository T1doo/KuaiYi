Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$out=Join-Path $repo ('artifacts/job-member-fixture-offline/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $out)
$fakePath=Join-Path $out 'FakeFixtureApi.cs'
@'
using System;using System.Collections.Generic;
public sealed class JobMemberFixtureFake : KuaiYiJobMemberWindowsFixture.IFixtureApi
{
 public string Scenario="Good",ThrowAt="";public bool Platform=true,Cleaning;
 public int NewJobs,Created,Resumed,Closed,CleanupTicks,ClockCalls;
 public readonly List<string> Events=new List<string>();
 public readonly List<IntPtr> Owned=new List<IntPtr>();
 public readonly Dictionary<IntPtr,int> CloseAttempts=new Dictionary<IntPtr,int>();
 private readonly Dictionary<IntPtr,int> binding=new Dictionary<IntPtr,int>();
 private readonly HashSet<IntPtr> exited=new HashSet<IntPtr>();
 public bool Supported=>Platform;
 private void Event(string value){Events.Add(value);if(ThrowAt==value)throw new Exception("PRIVATE_SYNTHETIC_SENTINEL");}
 public long Elapsed(){ClockCalls++;return Scenario=="BudgetAtStart"||Scenario=="BudgetAfterObserve"&&Events.Contains("Observe")?29750:0;}
 public IntPtr NewJob(){NewJobs++;Event("NewJob"+NewJobs);if(Scenario=="SecondJobMissing"&&NewJobs==2)return IntPtr.Zero;IntPtr h=new IntPtr(NewJobs*100);Owned.Add(h);return h;}
 public bool LimitJob(IntPtr job){Event("Limit"+job.ToInt64()/100);return Scenario!="LimitFailure";}
 public bool CreateSuspended(string executable,out KuaiYiJobMemberWindowsFixture.Child child)
 {
  Event("Create"+(Created+1));child=new KuaiYiJobMemberWindowsFixture.Child();
  if(Scenario=="CreateFailure"&&Created==3)return false;
  Created++;child=new KuaiYiJobMemberWindowsFixture.Child{Process=new IntPtr(1000+Created),Thread=new IntPtr(2000+Created),Id=(uint)(3000+Created)};
  Owned.Add(child.Process);Owned.Add(child.Thread);if(Scenario=="InvalidPid")child.Id=0;return true;
 }
 public bool Bind(IntPtr job,IntPtr process){Event("Bind"+(process.ToInt64()-1000));if(Scenario=="BindFailure")return false;binding[process]=(int)(job.ToInt64()/100);return true;}
 public bool Member(IntPtr job,IntPtr process){Event("Member"+(process.ToInt64()-1000));return Scenario!="MemberFailure";}
 public bool Resume(IntPtr thread){Event("Resume"+(thread.ToInt64()-2000));Resumed++;return Scenario!="ResumeFailure";}
 public uint Active(IntPtr job)
 {
  Event((Cleaning?"CleanActive":"Active")+job.ToInt64()/100);
  if(Cleaning)return Scenario=="JobUnsettled"?1u:0u;
  return job.ToInt64()==100?(Scenario=="WrongCount"?7u:8u):1u;
 }
 public bool LimitsIntact(IntPtr job){Event("LimitsIntact"+job.ToInt64()/100);return Scenario!="ChangedLimits";}
 public KuaiYiJobMemberDiagnostic.Observation Observe(IntPtr job)
 {
  Event("Observe");if(Scenario=="NullObservation")return null;
  return new KuaiYiJobMemberDiagnostic.Observation{State=Scenario=="ProbeFailure"?KuaiYiJobMemberDiagnostic.Status.ReaderFailure:KuaiYiJobMemberDiagnostic.Status.Observed,Listed=8,Other=Scenario=="ProbeWrongCount"?7:8,DiagnosticHandlesClosed=Scenario!="ProbeCloseUnknown"};
 }
 public KuaiYiJobMemberWindowsFixture.Rejection RejectOwnedNonMember(IntPtr job,uint id)
 {
  Event("Reject");if(id!=3009)throw new Exception();if(Scenario=="NullRejection")return null;
  var r=new KuaiYiJobMemberWindowsFixture.Rejection{Lists=1,Opens=1,MembershipChecks=1,Closes=1,HandlesClosed=Scenario!="RejectCloseUnknown",Observation=new KuaiYiJobMemberDiagnostic.Observation{State=KuaiYiJobMemberDiagnostic.Status.Partial,Listed=1,MembershipUnconfirmed=1}};
  if(Scenario=="RejectionAccepted")r.Observation.MembershipUnconfirmed=0;
  if(Scenario=="UnexpectedClass")r.Observation.MSBuild=1;
  if(Scenario=="UnexpectedImage")r.ImageReads=1;
  if(Scenario=="UnexpectedWait")r.RunningChecks=1;
  if(Scenario=="NullRejectionObservation")r.Observation=null;
  return r;
 }
 public bool TerminateJob(IntPtr job)
 {
  Event("TerminateJob"+job.ToInt64()/100);if(Scenario=="TerminateFailure")return false;
  foreach(var pair in binding)if(pair.Value==job.ToInt64()/100)exited.Add(pair.Key);return true;
 }
 public bool TerminateRoot(IntPtr process){Event("TerminateRoot");exited.Add(process);return true;}
 public bool RootRunning(IntPtr process){Event("LiveRoot");return KuaiYiJobMemberWindowsFixture.IsRunningWait(Scenario=="UnknownWait"?uint.MaxValue:Scenario=="EarlyExit"?0u:258u);}
 public bool RootExited(IntPtr process){Event(Cleaning?"CleanRoot":"LiveRoot");return Cleaning?Scenario!="RootUnsettled"&&exited.Contains(process):Scenario=="EarlyExit";}
 public bool Close(IntPtr handle)
 {
  Event("Close");if(!Owned.Contains(handle))throw new Exception("Unowned close");
  CloseAttempts[handle]=CloseAttempts.ContainsKey(handle)?CloseAttempts[handle]+1:1;Closed++;
  if(Scenario=="CloseThrows")throw new Exception("PRIVATE_SYNTHETIC_SENTINEL");
  return Scenario!="CloseFalse";
 }
 public void BeginCleanup(){Event("BeginCleanup");Cleaning=true;}
 public bool CleanupExpired=>!Cleaning||Scenario=="CloseCrossesDeadline"&&Closed>0||CleanupTicks>=5;
 public void Pause(){Event("Pause");CleanupTicks++;}
 public static int NativeSize(Type type){return System.Runtime.InteropServices.Marshal.SizeOf(type);}
}
'@ | Set-Content -LiteralPath $fakePath -Encoding utf8NoBOM
Add-Type -Path @((Join-Path $repo 'tools/ci/JobMemberDiagnostic.cs'),(Join-Path $PSScriptRoot 'JobMemberWindowsFixture.cs'),$fakePath)
$checks=0;$cases=0
function Check([bool]$Condition){if(-not $Condition){throw "Job member fixture contract failed $script:checks"};$script:checks++}
function Execute($api,[string]$Expected){
    $script:cases++
    $r=[KuaiYiJobMemberWindowsFixture]::Run($api,'OWNED_SYNTHETIC_EXECUTABLE',[Func[long]]{$api.Elapsed()})
    Check ($r.Status -ceq $Expected)
    $json=$r.PublicJson();Check ($json.Length -le 4096 -and $json -notmatch 'PRIVATE|OWNED_SYNTHETIC_EXECUTABLE|3009|Exception|[\\/]')
    $public=$json|ConvertFrom-Json -AsHashtable
    Check ($public.CMake -ceq 'NotRun' -and $public.FourCases -ceq 'NotRun' -and $public.ModelCalls -eq 0 -and $public.PidReuseScope -ceq 'SubstitutedOwnedNonMember')
    foreach($h in $api.Owned){Check ($api.CloseAttempts.ContainsKey($h) -and $api.CloseAttempts[$h] -eq 1)}
    if($Expected -cne 'Pass'){Check ($null -eq $public.NativeObservation -and $null -eq $public.NonMemberObservation -and $null -eq $public.NonMemberCounters)}
    return $r
}
$f=[JobMemberFixtureFake]::new();$r=Execute $f 'Pass'
Check ($r.ExitCode -eq 0 -and $r.CleanupConfirmed -and -not $r.CleanupFailed -and $r.Created -eq 9 -and $r.Checks.Count -eq 11)
Check ($f.NewJobs -eq 2 -and $f.Created -eq 9 -and $f.Resumed -eq 9 -and $f.Closed -eq 20)
$events=$f.Events -join ','
foreach($i in 1..9){Check ($events.IndexOf('Create'+$i) -lt $events.IndexOf('Bind'+$i));Check ($events.IndexOf('Bind'+$i) -lt $events.IndexOf('Member'+$i));Check ($events.IndexOf('Member'+$i) -lt $events.IndexOf('Resume'+$i))}
Check ($events.IndexOf('Observe') -lt $events.IndexOf('Reject') -and $events.IndexOf('Reject') -lt $events.IndexOf('BeginCleanup'))
Check (($r.PublicJson()|ConvertFrom-Json).NativeObservation.Other -eq 8)
$f=[JobMemberFixtureFake]::new();$f.Platform=$false;$r=Execute $f 'Blocked';Check ($r.ExitCode -eq 2 -and $f.Owned.Count -eq 0 -and $f.Events.Count -eq 0)
foreach($scenario in @('BudgetAtStart','BudgetAfterObserve','SecondJobMissing','LimitFailure','CreateFailure','InvalidPid','BindFailure','MemberFailure','ResumeFailure','WrongCount','ChangedLimits','EarlyExit','UnknownWait','NullObservation','ProbeFailure','ProbeWrongCount','ProbeCloseUnknown','NullRejection','NullRejectionObservation','RejectionAccepted','UnexpectedClass','UnexpectedImage','UnexpectedWait','RejectCloseUnknown','TerminateFailure','RootUnsettled','JobUnsettled','CloseFalse','CloseThrows','CloseCrossesDeadline')){
    $f=[JobMemberFixtureFake]::new();$f.Scenario=$scenario;$r=Execute $f 'Fail';Check ($r.ExitCode -eq 3)
    if($scenario -ceq 'BudgetAtStart'){Check ($f.Owned.Count -eq 0 -and $r.BudgetExpired)}
    if($scenario -ceq 'BudgetAfterObserve'){Check ($r.BudgetExpired -and -not $f.Events.Contains('Reject'))}
    if($scenario -ceq 'BindFailure'){Check ($r.Created -eq 1 -and $f.Events.Contains('TerminateRoot') -and -not $f.Events.Contains('Resume1'))}
    if($scenario -ceq 'UnknownWait'){Check (-not $f.Events.Contains('Observe'))}
    if($scenario -cin @('RootUnsettled','JobUnsettled','CloseCrossesDeadline')){Check ($r.CleanupBudgetExpired -and -not $r.CleanupConfirmed)}
    if($scenario -cin @('ProbeCloseUnknown','RejectCloseUnknown','CloseFalse','CloseThrows')){Check (-not $r.CleanupConfirmed -and $r.CleanupFailed)}
}
foreach($throwAt in @('NewJob1','NewJob2','Limit1','Create1','Create4','Bind1','Member1','Resume1','Active1','LimitsIntact1','LiveRoot','Observe','Reject','BeginCleanup','TerminateJob1','CleanRoot','CleanActive1')){
    $f=[JobMemberFixtureFake]::new();$f.ThrowAt=$throwAt;$r=Execute $f 'Fail';Check ($r.ExitCode -eq 3)
}
$prior=$env:KUAIYI_FIXTURE_SYNTHETIC_SECRET
try {$env:KUAIYI_FIXTURE_SYNTHETIC_SECRET='PRIVATE_SYNTHETIC_SENTINEL';$environment=[KuaiYiJobMemberWindowsFixture]::ChildEnvironment();Check ($environment -notmatch 'KUAIYI_FIXTURE_SYNTHETIC_SECRET|PRIVATE_SYNTHETIC_SENTINEL|GITHUB_TOKEN');Check ($environment.Contains('POWERSHELL_TELEMETRY_OPTOUT=1') -and $environment.EndsWith("`0`0"))} finally {$env:KUAIYI_FIXTURE_SYNTHETIC_SECRET=$prior}
Check ([KuaiYiJobMemberWindowsFixture]::Quote('C:\Program Files\pwsh.exe') -ceq '"C:\Program Files\pwsh.exe"')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'JobMemberWindowsFixture.cs'))
Check ($source.Contains('false,0x08000404,environment') -and $source.Contains('ResumeThread(thread)==1') -and $source.Contains('limits.Basic.Flags==0x2000'))
Check ($source -notmatch 'BREAKAWAY|AdjustTokenPrivileges|GetProcesses|Toolhelp|OpenJobObject|DuplicateHandle|KuaiYiLifecycleOperation|Process.Start')
if([IntPtr]::Size -eq 8){
    foreach($pair in @(@('BasicLimits',64),@('ExtendedLimits',144),@('Accounting',48),@('StartupInfo',104),@('ProcessInfo',24))){
        $layout=[KuaiYiJobMemberWindowsFixture].GetNestedType($pair[0],[Reflection.BindingFlags]::NonPublic)
        Check ([JobMemberFixtureFake]::NativeSize($layout) -eq $pair[1])
    }
}
Check ([KuaiYiJobMemberWindowsFixture]::IsRunningWait(258))
Check (-not [KuaiYiJobMemberWindowsFixture]::IsRunningWait(0))
Check (-not [KuaiYiJobMemberWindowsFixture]::IsRunningWait([uint32]::MaxValue))
Check (-not [KuaiYiJobMemberWindowsFixture]::IsRunningWait(1))
if(-not $IsWindows){
    $r=[KuaiYiJobMemberWindowsFixture]::RunWindows('MUST_NOT_RUN');Check ($r.Status -ceq 'Blocked' -and $r.Created -eq 0)
    $lines=@(& (Join-Path $PSHOME 'pwsh') -NoProfile -File (Join-Path $PSScriptRoot 'run-job-member-windows.ps1') *>&1)
    Check ($LASTEXITCODE -eq 2 -and $lines.Count -eq 1)
    $entry=([string]$lines[0]).Substring('KUAIYI_JOB_MEMBER_WINDOWS '.Length)|ConvertFrom-Json
    Check ($entry.Status -ceq 'Blocked' -and $entry.ExitCode -eq 2 -and $null -eq $entry.Fixture)
}
Write-Host "JOB_MEMBER_FIXTURE_CONTRACTS passed=$checks scenarios=$cases failed=0; injected fixture API only; native Windows/CMake/FourCases NotRun"
