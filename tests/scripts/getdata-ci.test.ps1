# Synthetic output/receipt contracts; no Windows COM or pointer input is performed here.
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/../../scripts/ole-ci-diagnostics.ps1"
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/run-windows-getdata-ci.ps1'),[ref]$tokens,[ref]$errors)
if (@($errors).Count -ne 0) { throw 'GetData entry parse failed' }
$fn=$ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Get-GetDataSummary'},$true)
Invoke-Expression $fn.Extent.Text
$passes=0; $commit='a'*40
function Check([string]$name,[scriptblock]$body) { & $body; $script:passes++; Write-Host "PASS $name" }
function Reject([string]$name,[scriptblock]$mutate) {
    $r=$script:valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    & $mutate $r
    $rejected=$false
    try { $null=Get-GetDataSummary $r 0 $script:commit } catch { $rejected=$true }
    if (-not $rejected) { throw "Expected rejection: $name" }
    $script:passes++; Write-Host "PASS rejects $name"
}
$owner=@{Admission='Ready';OriginalRunSettled=$true;OriginalControlSettled=$true;OwnedFinalized=$true;DiagnosticReadSettled=$true;DiagnosticReadFailure='None';OwnedExitCode=-1;StartupFailure=$null}
$valid=@{
    Kind='HostedWindowsContracts';Scope='HostedOleFixtureContractsOnly';SourceCommit=$commit;SourceDirty=$false
    Windows11Acceptance='NotRun';E06PhysicalAcceptance='NotRun';NativeStatus='Pass';OleStatus='Pass';ExitCode=0
    NativeDlls=1;PeChecksPassed=1;DllLoadsPassed=1;ExportsPassed=1;CTestsPassed=0;OverlayStatus='NotRun';Errors=@();OleSyntheticPointerStatus='Pass'
    OleReport=@{
        Kind='HostedOleResponsivenessPublicProbe';Source='PublicFixtureOnly';ModelCalls=0;IsolationStatus='Pass';ExitCode=0
        PhysicalMouseAcceptance='NotRun';Windows11Acceptance='NotRun';SyntheticPointerStatus='Pass';ErrorsCount=0;Errors=@()
        ProductionTextDrop=$true;RecoveredAfterConfirmedExit=$true;CloseConfirmed=$true;AllReceiverCreationsAfterConfirmedExit=$true
        Scenarios=@(@{name='cancel-recover';realProductionDrop=$true;realGetDataBlocked=$true;uiHeartbeatAdvanced=$true},@{name='close';realProductionDrop=$true;realGetDataBlocked=$true;uiHeartbeatAdvanced=$true})
        StartupDiagnostics=@($owner,$owner,$owner)
    }
} | ConvertTo-Json -Depth 12 | ConvertFrom-Json
Check 'complete synthetic receipt produces a bounded pass' {
    $r=Get-GetDataSummary $valid 0 $commit
    if ($r.Status -cne 'Pass' -or $r.ExitCode -ne 0 -or $r.GetData -cne 'Pass' -or -not $r.OriginalTasksSettled -or $r.ReceiverCount -ne 3) { throw 'Pass lost evidence' }
}
Check 'arbitrary nested environment and error text never reaches output' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $r | Add-Member Environment @{Private='PUBLIC_SENTINEL'}
    $r | Add-Member RunUrl 'PUBLIC_SENTINEL'
    $r.OleReport.StartupDiagnostics[0] | Add-Member RawStderr 'PUBLIC_SENTINEL'
    $out=Get-GetDataSummary $r 0 $commit | ConvertTo-Json -Depth 4
    if ($out -match 'PUBLIC_SENTINEL|Environment|RunUrl|RawStderr') { throw 'Raw output escaped' }
}
Check 'native prerequisite blocked is not success' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $r.NativeStatus='Blocked';$r.OleStatus='Blocked';$r.OleReport=$null;$r.ExitCode=2
    $x=Get-GetDataSummary $r 2 $commit
    if ($x.Status -cne 'Blocked' -or $x.ExitCode -ne 2 -or $x.GetData -cne 'UNKNOWN') { throw 'Blocked became pass' }
}
Check 'desktop blocked even when legacy process exits zero stays blocked' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json;$r.OleStatus='Blocked';$r.OleReport=$null
    $x=Get-GetDataSummary $r 0 $commit
    if ($x.Status -cne 'Blocked' -or $x.ExitCode -ne 2) { throw 'Blocked became pass' }
}
Check 'failed child stays failed without raw errors' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json;$r.ExitCode=3;$r.OleStatus='Fail';$r.Errors=@('PUBLIC_SENTINEL')
    $x=Get-GetDataSummary $r 3 $commit
    if ($x.Status -cne 'Fail' -or $x.ExitCode -ne 3 -or (($x | ConvertTo-Json) -match 'PUBLIC_SENTINEL')) { throw 'Failure projection' }
}
Check 'synthetic pointer limitation keeps physical acceptance unexecuted' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json;$r.OleSyntheticPointerStatus='Blocked';$r.OleReport.SyntheticPointerStatus='Blocked'
    $x=Get-GetDataSummary $r 0 $commit
    if ($x.Status -cne 'Pass' -or $x.SyntheticPointerStatus -cne 'Blocked' -or $x.PhysicalE06 -cne 'NotRun') { throw 'Evidence conflation' }
}
Reject 'different source commit' {param($r) $r.SourceCommit='b'*40}
Reject 'dirty source' {param($r) $r.SourceDirty=$true}
Reject 'string cleanliness' {param($r) $r.SourceDirty='false'}
Reject 'marshal only scope' {param($r) $r.Scope='OwnedNativeMarshalOnly'}
Reject 'physical pass' {param($r) $r.E06PhysicalAcceptance='Pass'}
Reject 'Windows11 pass' {param($r) $r.Windows11Acceptance='Pass'}
Reject 'unknown native status' {param($r) $r.NativeStatus='PUBLIC_SENTINEL'}
Reject 'unknown isolation status' {param($r) $r.OleStatus='PUBLIC_SENTINEL'}
Reject 'string child exit' {param($r) $r.ExitCode='0'}
Reject 'different child exit' {param($r) $r.ExitCode=3}
Reject 'extra DLL scope' {param($r) $r.NativeDlls=4}
Reject 'DLL not loaded' {param($r) $r.DllLoadsPassed=0}
Reject 'unrelated overlay rerun' {param($r) $r.OverlayStatus='Pass'}
Reject 'legacy raw errors on pass' {param($r) $r.Errors=@('PUBLIC_SENTINEL')}
Reject 'missing original receipt' {param($r) $r.OleReport=$null}
Reject 'model call' {param($r) $r.OleReport.ModelCalls=1}
Reject 'string model calls' {param($r) $r.OleReport.ModelCalls='0'}
Reject 'string nested exit' {param($r) $r.OleReport.ExitCode='0'}
Reject 'string nested error count' {param($r) $r.OleReport.ErrorsCount='0'}
Reject 'not public fixture' {param($r) $r.OleReport.Source='PrivateSelection'}
Reject 'unknown pointer status' {param($r) $r.OleReport.SyntheticPointerStatus='PUBLIC_SENTINEL'}
Reject 'pointer evidence disagreement' {param($r) $r.OleSyntheticPointerStatus='Blocked'}
Reject 'unsettled recovery' {param($r) $r.OleReport.RecoveredAfterConfirmedExit=$false}
Reject 'unconfirmed close' {param($r) $r.OleReport.CloseConfirmed=$false}
Reject 'unsafe receiver creation' {param($r) $r.OleReport.AllReceiverCreationsAfterConfirmedExit=$false}
Reject 'duplicate scenario' {param($r) $r.OleReport.Scenarios[1].name='cancel-recover'}
Reject 'no actual drop' {param($r) $r.OleReport.Scenarios[0].realProductionDrop=$false}
Reject 'no actual GetData block' {param($r) $r.OleReport.Scenarios[0].realGetDataBlocked=$false}
Reject 'no heartbeat' {param($r) $r.OleReport.Scenarios[0].uiHeartbeatAdvanced=$false}
Reject 'string heartbeat' {param($r) $r.OleReport.Scenarios[0].uiHeartbeatAdvanced='true'}
Reject 'unknown admission' {param($r) $r.OleReport.StartupDiagnostics[0].Admission='UNKNOWN'}
Reject 'no ready owners' {param($r) foreach($d in $r.OleReport.StartupDiagnostics){$d.Admission='CancelledBeforeRegistration'}}
Reject 'original run pending' {param($r) $r.OleReport.StartupDiagnostics[0].OriginalRunSettled=$false}
Reject 'original control pending' {param($r) $r.OleReport.StartupDiagnostics[0].OriginalControlSettled=$false}
Reject 'original owner pending' {param($r) $r.OleReport.StartupDiagnostics[0].OwnedFinalized=$false}
Reject 'original read pending' {param($r) $r.OleReport.StartupDiagnostics[0].DiagnosticReadSettled=$false}
Reject 'original diagnostic read failed' {param($r) $r.OleReport.StartupDiagnostics[0].DiagnosticReadFailure='Io'}
Reject 'missing original owners' {param($r) $r.OleReport.StartupDiagnostics=@()}
Reject 'too many original owners' {param($r) $r.OleReport.StartupDiagnostics=@(1..9 | ForEach-Object { $owner })}
$compile=$ast.Find({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -ceq 'Add-Type'},$true)
$source=($compile.CommandElements | Where-Object {$_ -is [Management.Automation.Language.StringConstantExpressionAst] -and $_.StringConstantType -eq 'SingleQuotedHereString'}).Value
Add-Type -TypeDefinition $source
Check 'bounded output drain consumes actual one MiB synthetic stream' {
    $stream=[IO.MemoryStream]::new([byte[]]::new(1048576))
    try { if (-not [KuaiYiGetDataDiscard]::Drain($stream).GetAwaiter().GetResult() -or $stream.Position -ne $stream.Length) { throw 'Drain incomplete' } }
    finally { $stream.Dispose() }
}
Check 'read error fails drain' {
    $stream=[IO.MemoryStream]::new();$stream.Dispose()
    if ([KuaiYiGetDataDiscard]::Drain($stream).GetAwaiter().GetResult()) { throw 'Failed read accepted' }
}
Check 'original output read remains pending until actual writer EOF' {
    $reader=[IO.Pipes.AnonymousPipeServerStream]::new([IO.Pipes.PipeDirection]::In)
    $writer=[IO.Pipes.AnonymousPipeClientStream]::new([IO.Pipes.PipeDirection]::Out,$reader.ClientSafePipeHandle)
    try {
        $drain=[KuaiYiGetDataDiscard]::Drain($reader)
        if ($drain.Wait(25)) { throw 'Original read settled before EOF' }
        $writer.Dispose()
        if (-not $drain.Wait(1000) -or -not $drain.GetAwaiter().GetResult()) { throw 'Original read failed to observe EOF' }
    } finally { $writer.Dispose();$reader.Dispose() }
}
if (-not $IsWindows) {
    Check 'real Linux entry refuses Windows execution with only fixed summary' {
        $output=@(& (Join-Path $PSHOME 'pwsh') -NoProfile -File (Join-Path $root 'scripts/run-windows-getdata-ci.ps1'))
        if ($LASTEXITCODE -ne 2 -or $output.Count -ne 1 -or $output[0] -cnotmatch '^KUAIYI_GETDATA_SUMMARY ') { throw 'Invalid Linux boundary' }
        $x=$output[0].Substring('KUAIYI_GETDATA_SUMMARY '.Length) | ConvertFrom-Json
        if ($x.Status -cne 'Blocked' -or $x.Reason -cne 'WindowsRequired' -or $x.PhysicalE06 -cne 'NotRun') { throw 'Linux misclassified' }
    }
}
Write-Host "GETDATA_ENTRY_CONTRACTS passed=$passes failed=0; synthetic schema and pipes only; actual Windows GetData NotRun"
