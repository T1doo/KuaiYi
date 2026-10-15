Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-configure-diagnostics.ps1"
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"

$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$summary=[pscustomobject][ordered]@{Kind='ConfigureBridgeWindowsFixture';SourceCommit=$null;Scope='SyntheticOwnedFilesOnly';Status='Fail';Stage='Initialize';Checks=@();CleanupConfirmed=$false;CleanupFailed=$false;OriginalResultPreserved=$false;CMake='NotRun';FourCases='NotRun';ModelCalls=0;ExitCode=3}
$checks=[Collections.Generic.List[string]]::new();$leases=[Collections.Generic.List[object]]::new()
$container=$null;$python=$null
function Check([string]$Name,[bool]$Condition) {
    $script:summary.Stage=$Name
    if(-not $Condition){throw 'Fixture check failed'}
    $script:checks.Add($Name)
}
function Owned-Root([string]$Name) {
    $path=Join-Path $script:container $Name
    [void](New-Item -ItemType Directory -Path $path)
    $lease=[KuaiYiConfigureRootLease]::Open($path)
    $script:leases.Add($lease)
    return $lease
}
function Write-Owned([string]$Root,[string]$Relative,[byte[]]$Bytes) {
    $path=Join-Path $Root $Relative
    [void](New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent))
    [IO.File]::WriteAllBytes($path,$Bytes)
}
function Read-ActualProbe($Lease,[string]$Case,[long]$Handle,[uint64]$Index) {
    $log=Join-Path $script:container ($Case+'.local.json')
    $probe=Join-Path $script:repo 'tests/scripts/configure-bridge-owned-probe.py'
    $args=@('-I','-B',$probe,'confirmed',[string]$Lease.SourceProcess,[string]$Handle,[string]$Lease.Volume,[string]$Index)
    $receipt=[KuaiYiLifecycleOperation]::Run($script:python,$args,$script:repo,$log)
    if([KuaiYiLifecycleOperation]::GetReason($receipt) -ne [KuaiYiLifecycleOperation+OperationReason]::Completed -or $receipt.Exit -cne '0' -or (Get-Item -LiteralPath $log).Length -gt 8192){throw 'Probe unavailable'}
    $raw=Get-Content -LiteralPath $log -Raw
    $row=ConvertFrom-Json -InputObject $raw -AsHashtable -Depth 8
    Assert-ConfigureFields $row @('Diagnostic','BridgeReceives','BridgeCloses','ReadCalls','ReadBytes','AdapterHandlesOpen')
    foreach($key in @('BridgeReceives','BridgeCloses','ReadCalls','ReadBytes','AdapterHandlesOpen')){
        if($row[$key] -isnot [int] -and $row[$key] -isnot [long]){throw 'Invalid counter'}
        if($row[$key] -lt 0 -or $row[$key] -gt 262144){throw 'Invalid counter'}
    }
    $diagnostic=Read-LifecycleConfigureDiagnostic ($row.Diagnostic|ConvertTo-Json -Depth 8 -Compress)
    return [pscustomobject]@{Diagnostic=$diagnostic;BridgeReceives=$row.BridgeReceives;BridgeCloses=$row.BridgeCloses;ReadCalls=$row.ReadCalls;ReadBytes=$row.ReadBytes;AdapterHandlesOpen=$row.AdapterHandlesOpen}
}
try {
    $summary.Stage='Preflight'
    if(-not $IsWindows -or $env:CI -cne 'true' -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -cne 'X64'){
        $summary.Status='Blocked';$summary.Stage='HostedWindowsRequired';$summary.ExitCode=2
    } else {
        if($env:GITHUB_SHA -cnotmatch '^[0-9a-f]{40}$'){throw 'Invalid source'}
        $summary.SourceCommit=$env:GITHUB_SHA
        $python=@(Get-Command python -CommandType Application -ErrorAction Stop)[0].Source
        $container=Join-Path $repo ('artifacts/configure-bridge-ci/'+[guid]::NewGuid().ToString('N'))
        [void](New-Item -ItemType Directory -Force -Path $container)
        $summary.Stage='LoadInterop'
        Add-Type -Path (Join-Path $repo 'tools/ci/LifecycleOperation.cs') 2>$null 3>$null 4>$null 5>$null 6>$null
        Add-Type -Path (Join-Path $repo 'tools/ci/ConfigureRootLease.cs') 2>$null 3>$null 4>$null 5>$null 6>$null
        Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class ConfigureFixtureHandleType {
    [DllImport("kernel32.dll")] private static extern uint GetFileType(IntPtr handle);
    public static bool IsDisk(long handle) => GetFileType(new IntPtr(handle))==1;
}
'@ 2>$null 3>$null 4>$null 5>$null 6>$null

        $summary.Stage='CleanupGate'
        $gate=Owned-Root 'gate'
        $gateHandle=$gate.Handle
        $gateCalled=$false
        $unclean=[KuaiYiLifecycleOperation+Receipt]::new();$unclean.CleanupConfirmed=$false
        $skipped=Complete-LifecycleConfigureDiagnostic $unclean $gate {param($owned)$script:gateCalled=$true;throw 'must not run'}
        Check 'CleanupGate' ($skipped.DiagnosticStatus -ceq 'SkippedCleanupUnconfirmed' -and -not $gateCalled -and -not [ConfigureFixtureHandleType]::IsDisk($gateHandle))

        # The lease precedes every synthetic writer, matching the real runner order.
        $summary.Stage='SyntheticFiles'
        $positive=Owned-Root 'positive';$positiveHandle=$positive.Handle
        Check 'RootLeaseBeforeSynthetic' ([ConfigureFixtureHandleType]::IsDisk($positiveHandle))
        $positivePath=Join-Path $container 'positive';$utf8=[Text.Encoding]::UTF8
        Write-Owned $positivePath 'CMakeVersion.local.log' ($utf8.GetBytes('cmake version 3.31.6'))
        Write-Owned $positivePath 'NativeConfigure.local.log' ($utf8.GetBytes("-- Configuring done`n-- Generating done`n-- The CXX compiler identification is MSVC 19.44`nPRIVATE_SYNTHETIC_SENTINEL --key=fixture"))
        Write-Owned $positivePath 'native/CMakeCache.txt' ($utf8.GetBytes('CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022'))
        Write-Owned $positivePath 'native/CMakeFiles/CMakeConfigureLog.yaml' ($utf8.GetBytes("  -`n    kind: `"try_compile-v1`""))
        $original=[KuaiYiLifecycleOperation+Receipt]::new();$original.Status='Timeout';$original.Exit='0';$original.Failure=[KuaiYiLifecycleOperation+FailureKind]::Timeout
        $original.Stage=[KuaiYiLifecycleOperation+OperationStage]::WaitJobEmpty;$original.ProcessCreated=$true;$original.Resumed=$true;$original.RescueUsed=$true;$original.RootExitConfirmed=$true
        $original.JobEmpty=$true;$original.CleanupConfirmed=$true;$original.BudgetExpired=$true;$original.PreRescueJobState=[KuaiYiLifecycleOperation+JobState]::NonEmpty
        $before=Get-LifecycleOperationEvidence 'NativeConfigure' $original|ConvertTo-Json -Compress
        $reasonBefore=[KuaiYiLifecycleOperation]::GetReason($original)
        $positiveProbe=$null
        $diagnostic=Complete-LifecycleConfigureDiagnostic $original $positive {
            param($owned)
            $script:positiveProbe=Read-ActualProbe $owned 'positive' $owned.Handle $owned.Index
            Check 'CallerLeaseAliveDuringCollection' ([ConfigureFixtureHandleType]::IsDisk($owned.Handle))
            return ($script:positiveProbe.Diagnostic|ConvertTo-Json -Depth 8 -Compress)
        }
        Check 'SyntheticFourFacts' ($diagnostic.DiagnosticStatus -ceq 'Collected' -and $diagnostic.Files.Count -eq 4 -and $diagnostic.Files[0].Facts.CMakeVersion -ceq '3.31.6' -and $diagnostic.Files[1].Facts.GenerateDone -and $diagnostic.Files[2].Facts.VisualStudio2022 -and $diagnostic.Files[3].Facts.TryCompileEvent)
        Check 'BridgeOwnership' ($positiveProbe.BridgeReceives -eq 1 -and $positiveProbe.BridgeCloses -eq 1 -and $positiveProbe.ReadCalls -eq 4 -and $positiveProbe.AdapterHandlesOpen -eq 0)
        $originalAfter=Get-LifecycleOperationEvidence 'NativeConfigure' $original|ConvertTo-Json -Compress
        $summary.OriginalResultPreserved=$originalAfter -ceq $before -and [KuaiYiLifecycleOperation]::GetReason($original) -eq $reasonBefore
        Check 'OriginalResultPreserved' ($summary.OriginalResultPreserved -and $reasonBefore -eq [KuaiYiLifecycleOperation+OperationReason]::OperationTimeout)
        Check 'LeaseClosedOnce' (-not [ConfigureFixtureHandleType]::IsDisk($positiveHandle))
        $positive.Dispose();Check 'SecondDisposeSafe' (-not [ConfigureFixtureHandleType]::IsDisk($positiveHandle))

        $summary.Stage='InvalidProof'
        $negative=Owned-Root 'negative'
        $wrong=Read-ActualProbe $negative 'wrong-identity' $negative.Handle ($negative.Index+1)
        Check 'WrongIdentityNoRead' ($wrong.Diagnostic.DiagnosticStatus -ceq 'SafetyUnknown' -and $wrong.ReadCalls -eq 0 -and $wrong.ReadBytes -eq 0 -and $wrong.BridgeReceives -eq 1 -and $wrong.BridgeCloses -eq 1)
        $invalid=Read-ActualProbe $negative 'invalid-handle' 0 $negative.Index
        Check 'InvalidHandleNoRead' ($invalid.Diagnostic.DiagnosticStatus -ceq 'OwnershipUnconfirmed' -and $invalid.ReadCalls -eq 0 -and $invalid.BridgeReceives -eq 0)
        $stale=Owned-Root 'stale';$staleHandle=$stale.Handle;$stale.Dispose()
        $closed=Read-ActualProbe $negative 'closed-handle' $staleHandle $stale.Index
        Check 'ClosedHandleNoRead' ($closed.Diagnostic.DiagnosticStatus -ceq 'SafetyUnknown' -and $closed.ReadCalls -eq 0 -and $closed.ReadBytes -eq 0)
        $negative.Dispose()

        $summary.Stage='ReadBound'
        $bounded=Owned-Root 'bounded';$boundPath=Join-Path $container 'bounded'
        $large=[byte[]]::new(65537)
        foreach($relative in @('CMakeVersion.local.log','NativeConfigure.local.log','native/CMakeCache.txt','native/CMakeFiles/CMakeConfigureLog.yaml')){Write-Owned $boundPath $relative $large}
        $max=Read-ActualProbe $bounded 'bounded' $bounded.Handle $bounded.Index
        Check 'FourFileReadBound' ($max.Diagnostic.DiagnosticStatus -ceq 'Collected' -and $max.ReadCalls -eq 4 -and $max.ReadBytes -eq 262144 -and @($max.Diagnostic.Files|Where-Object {$_.ReadStatus -cne 'Truncated'}).Count -eq 0)
        Check 'TruncatedFactsCleared' (@($max.Diagnostic.Files|Where-Object {$null -ne $_.Facts.CMakeVersion -or $_.Facts.GenerateDone -or $_.Facts.VisualStudio2022 -or $_.Facts.TryCompileEvent}).Count -eq 0)
        $bounded.Dispose()

        $summary.Stage='MissingFile'
        $missing=Owned-Root 'missing';$missingPath=Join-Path $container 'missing'
        Write-Owned $missingPath 'CMakeVersion.local.log' ($utf8.GetBytes('cmake version 3.31.6'))
        $miss=Read-ActualProbe $missing 'missing' $missing.Handle $missing.Index
        Check 'MissingFilesNotFabricated' ($miss.Diagnostic.Files.Count -eq 4 -and $miss.Diagnostic.Files[1].ReadStatus -ceq 'Missing' -and $miss.Diagnostic.Files[2].ReadStatus -ceq 'Missing' -and $miss.Diagnostic.Files[3].ReadStatus -ceq 'Missing' -and $miss.ReadCalls -eq 1)
        $missing.Dispose()

        $summary.Stage='DiagnosticFailure'
        $failureLease=Owned-Root 'diagnostic-failure'
        $before=Get-LifecycleOperationEvidence 'NativeConfigure' $original|ConvertTo-Json -Compress
        $failed=Complete-LifecycleConfigureDiagnostic $original $failureLease {throw 'PRIVATE_SYNTHETIC_SENTINEL'}
        Check 'DiagnosticFailureIsolated' ($failed.DiagnosticStatus -ceq 'ProjectionError' -and (Get-LifecycleOperationEvidence 'NativeConfigure' $original|ConvertTo-Json -Compress) -ceq $before)
        $summary.Status='Pass';$summary.Stage='Completed';$summary.ExitCode=0
    }
} catch {
    # Never publish paths, handle values, source text or exception messages.
} finally {
    foreach($lease in $leases){try {[void]$lease.Dispose()} catch {$summary.CleanupFailed=$true}}
    if($container){try {Remove-Item -LiteralPath $container -Recurse -Force -ErrorAction Stop;$summary.CleanupConfirmed=$true} catch {$summary.CleanupFailed=$true}}
    if($summary.CleanupFailed -or ($summary.Status -ceq 'Pass' -and -not $summary.CleanupConfirmed)){$summary.Status='Fail';$summary.ExitCode=3;$summary.Stage='FixtureCleanup'}
    $summary.Checks=$checks.ToArray()
}
Write-Host ('KUAIYI_CONFIGURE_BRIDGE_WINDOWS '+($summary|ConvertTo-Json -Depth 6 -Compress))
exit $summary.ExitCode
