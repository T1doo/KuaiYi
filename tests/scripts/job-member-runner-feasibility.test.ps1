Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# Proposed placement simulation only. Compile the unchanged REAL helper/decision functions;
# no Run invocation, Windows APIs, processes, jobs, model calls or background Job handles.
$source=@'
using System;using System.Collections.Generic;
public sealed class JobMemberPlacementSimulation : KuaiYiJobMemberDiagnostic.IProbe {
 public long Now=29700,EmptyAt=29900;public string Mode="Normal";
 public bool Attempted,FinallyReached;public int Calls,Opens,Closes;
 public KuaiYiJobMemberDiagnostic.Observation Diagnostic;
 public readonly List<string> Events=new List<string>();
 public KuaiYiLifecycleOperation.Receipt RunProposal(bool enabled) {
  var r=new KuaiYiLifecycleOperation.Receipt{RootExitConfirmed=true,ProcessCreated=true,Resumed=true,Exit="0",Stage=KuaiYiLifecycleOperation.OperationStage.WaitJobEmpty};
  try {
   while(!(Now>=EmptyAt)&&Now<30000) {
    if(enabled&&!Attempted) {Attempted=true;if(30000-Now>250) {try{Diagnostic=KuaiYiJobMemberDiagnostic.Capture(new IntPtr(100),()=>30000-Now,this);}catch{Events.Add("ReaderException");}}}
    Now+=10;
   }
   bool empty=Now>=EmptyAt;
   if(KuaiYiLifecycleOperation.CompleteJobWait(r,empty,Now>=30000)){r.Status="Timeout";r.Failure=KuaiYiLifecycleOperation.FailureKind.Timeout;r.RescueUsed=true;}else r.Status="Completed";
  } finally {
   FinallyReached=true;
   if(r.RescueUsed){Events.Add("Rescue");r.RootExitConfirmed=true;r.JobEmpty=true;}
   r.CleanupConfirmed=true;Events.Add("CloseJob");
  }
  return r;
 }
 public KuaiYiJobMemberDiagnostic.MemberList ReadList(IntPtr job) {
  if(job!=new IntPtr(100))throw new Exception();Calls++;Events.Add("ReadList");
  if(Mode=="Throw")throw new Exception("PRIVATE_SENTINEL");
  if(Mode=="Cancel")throw new OperationCanceledException("PRIVATE_SENTINEL");
  if(Mode=="Late")Now=30100;
  var b=new byte[72];Array.Copy(BitConverter.GetBytes(1u),0,b,0,4);Array.Copy(BitConverter.GetBytes(1u),0,b,4,4);Array.Copy(BitConverter.GetBytes(7ul),0,b,8,8);
  return new KuaiYiJobMemberDiagnostic.MemberList{Success=true,WrittenBytes=16,Buffer=b};
 }
 public IntPtr Open(uint id){Opens++;return new IntPtr(200);}
 public KuaiYiJobMemberDiagnostic.Verification IsMember(IntPtr process,IntPtr job){return KuaiYiJobMemberDiagnostic.Verification.Yes;}
 public KuaiYiJobMemberDiagnostic.Verification IsRunning(IntPtr process){return KuaiYiJobMemberDiagnostic.Verification.Yes;}
 public string Image(IntPtr process){return @"C:\SYNTHETIC\MSBuild.exe";}
 public bool Close(IntPtr process){Closes++;return Mode!="CloseUnknown";}
}
'@
$out=Join-Path $repo ('artifacts/job-member-placement-tests/'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $out)
$fake=Join-Path $out 'PlacementSimulation.cs';$source|Set-Content -LiteralPath $fake -Encoding utf8NoBOM
Add-Type -Path @((Join-Path $repo 'tools/ci/LifecycleOperation.cs'),(Join-Path $repo 'tools/ci/JobMemberDiagnostic.cs'),$fake)
$checks=0;$cases=0
function Check([bool]$Value){if(-not $Value){throw "Runner placement feasibility contract failed $script:checks"};$script:checks++}
$baseline=[JobMemberPlacementSimulation]::new();$before=$baseline.RunProposal($false);$cases++
Check ($before.Status -ceq 'Completed' -and -not $before.RescueUsed -and $before.CleanupConfirmed)
Check ($baseline.Calls -eq 0 -and $baseline.FinallyReached)
foreach($mode in @('Normal','Throw','Cancel','CloseUnknown','Late')){
 $s=[JobMemberPlacementSimulation]::new();$s.Mode=$mode;$r=$s.RunProposal($true);$cases++
 Check ($s.Calls -eq 1 -and $s.Attempted -and $s.FinallyReached -and $r.CleanupConfirmed)
 Check ($r.Exit -ceq '0' -and $r.RootExitConfirmed -and $s.Events[$s.Events.Count-1] -ceq 'CloseJob')
 Check ($s.Diagnostic.PublicJson() -notmatch 'PRIVATE|SYNTHETIC|Exception|[\\/]')
 switch($mode){
  Normal {Check ($s.Diagnostic.State.ToString() -ceq 'Observed' -and $s.Opens -eq 1 -and $s.Closes -eq 1)}
  {$_ -cin @('Throw','Cancel')} {Check ($s.Diagnostic.State.ToString() -ceq 'ReaderFailure' -and $s.Diagnostic.Listed -eq -1)}
  CloseUnknown {Check ($s.Diagnostic.State.ToString() -ceq 'HandleCloseUnknown' -and -not $s.Diagnostic.DiagnosticHandlesClosed -and $s.Diagnostic.Listed -eq -1)}
  Late {
   Check ($s.Diagnostic.State.ToString() -ceq 'SkippedBudget' -and $s.Diagnostic.Listed -eq -1 -and $s.Opens -eq 0)
   Check ($r.Status -ceq 'Timeout' -and $r.RescueUsed -and $r.BudgetExpired -and $s.Events.Contains('Rescue'))
   Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'OperationTimeout')
   Check ($before.Status -cne $r.Status -and $before.RescueUsed -ne $r.RescueUsed)
  }
 }
 if($mode -cne 'Late'){Check ($r.Status -ceq $before.Status -and $r.RescueUsed -eq $before.RescueUsed -and [KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'Completed')}
}
foreach($start in @(29750,29999,30000)){
 $s=[JobMemberPlacementSimulation]::new();$s.Now=$start;$s.EmptyAt=30001;$r=$s.RunProposal($true);$cases++
 Check ($s.Calls -eq 0 -and $null -eq $s.Diagnostic -and $s.FinallyReached -and $r.RescueUsed)
}
$s=[JobMemberPlacementSimulation]::new();$s.EmptyAt=0;$r=$s.RunProposal($true);$cases++
Check ($s.Calls -eq 0 -and -not $s.Attempted -and $r.Status -ceq 'Completed')
# Returning diagnostic failures do not suppress rescue when the original Job stays nonempty.
foreach($mode in @('Throw','Cancel','CloseUnknown')){
 $s=[JobMemberPlacementSimulation]::new();$s.Mode=$mode;$s.EmptyAt=[long]::MaxValue;$r=$s.RunProposal($true);$cases++
 Check ($s.Calls -eq 1 -and $s.FinallyReached -and $r.Status -ceq 'Timeout' -and $r.RescueUsed -and $r.CleanupConfirmed)
 Check ($s.Events.IndexOf('ReadList') -lt $s.Events.IndexOf('Rescue') -and $s.Events.IndexOf('Rescue') -lt $s.Events.IndexOf('CloseJob'))
}
# No production hook was introduced to make the simulated proposal look safe.
foreach($name in @('tools/ci/LifecycleOperation.cs','tools/ci/JobMemberDiagnostic.cs','tests/scripts/run-configure-only-windows.ps1')){
 $base=& git -C $repo rev-parse ('1dfafc36da5c393b511c2143ac3894fac3f90e9a:'+$name)
 $current=& git -C $repo hash-object (Join-Path $repo $name)
 Check ($LASTEXITCODE -eq 0 -and $base -ceq $current)
}
Write-Host "JOB_MEMBER_RUNNER_FEASIBILITY passed=$checks scenarios=$cases failed=0; placement proposal HOLD; deterministic virtual clock only; actual runner/Windows/CMake/FourCases NotRun"
