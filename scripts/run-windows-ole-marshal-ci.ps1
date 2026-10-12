param([Parameter(Mandatory)][string]$NativeRoot,[Parameter(Mandatory)][string]$OutputRoot)
. "$PSScriptRoot/windows-ci-common.ps1"
function Assert-MarshalReceipt($result) {
    $allowed = @('Kind','Status','ModelCalls','ExportSource','ProductionValidator','ProductionImport','ImportAttempted','Imported','IndependentParentSta','ImporterReleased','OwnedFinalized','ReceiverExitCode','DiagnosticReadSettled','DiagnosticReadFailure','ExportObservations','ParentImportObservation','StartupFailure','ContractFailure','ImportFailure','RegistrationAttempted','ActualDrop','GetData','CloseRecovery','PhysicalE06','Windows11Acceptance','ExitCode')
    if (@($result.PSObject.Properties.Name).Count -ne $allowed.Count -or @($result.PSObject.Properties.Name | Where-Object { $_ -cnotin $allowed }).Count -ne 0) { throw 'Unexpected marshal receipt field' }
    foreach ($name in @('ProductionValidator','ProductionImport','ImportAttempted','Imported','IndependentParentSta','ImporterReleased','OwnedFinalized','DiagnosticReadSettled','RegistrationAttempted')) { if ($result.$name -isnot [bool]) { throw 'Invalid marshal boolean field' } }
    if ($result.Kind -cne 'OwnedNativeMarshalContract' -or $result.Status -cnotin @('Pass','Fail') -or $result.ExportSource -cne 'ActualNativeTargetInOwnedReceiver' -or -not $result.ProductionValidator -or -not $result.ProductionImport -or $result.ModelCalls -ne 0 -or $result.RegistrationAttempted) { throw 'Invalid marshal evidence boundary' }
    foreach ($name in @('ActualDrop','GetData','CloseRecovery','PhysicalE06','Windows11Acceptance')) { if ($result.$name -cne 'NotRun') { throw 'Invalid marshal acceptance claim' } }
    foreach ($name in @('ModelCalls','ExitCode','ReceiverExitCode')) { if ($null -ne $result.$name -and (($result.$name -isnot [int] -and $result.$name -isnot [long]) -or $result.$name -lt [int]::MinValue -or $result.$name -gt [int]::MaxValue)) { throw 'Invalid marshal integer field' } }
    $codes = @('None','Argument','InvalidOperation','InvalidData','Io','FileMissing','DirectoryMissing','Unauthorized','DllMissing','BadImage','Com','Cancelled','Disposed','Timeout','Other')
    if ($result.DiagnosticReadFailure -cnotin $codes) { throw 'Invalid drain code' }
    $stages = @('Entry','Apartment','Dispatcher','HelloRead','HelloValidated','BackendCreate','ParentVerify','ActivationOpen','NativeExportCreate','ExportValidate','ExportQueued','Controls','ParentStart','ParentHelloWrite','ParentHelloRead','ParentExportRead','ParentRegister','ParentReady','ParentRun','ParentShutdown')
    foreach ($name in @('StartupFailure','ContractFailure','ImportFailure')) {
        $item = $result.$name
        if ($null -eq $item) { continue }
        if (@($item.PSObject.Properties.Name).Count -ne 3 -or @($item.PSObject.Properties.Name | Where-Object { $_ -cnotin @('Stage','Exception','HResult') }).Count -ne 0 -or $item.Stage -cnotin $stages -or $item.Exception -cnotin $codes -or ($item.HResult -isnot [int] -and $item.HResult -isnot [long]) -or $item.HResult -lt [int]::MinValue -or $item.HResult -gt [int]::MaxValue) { throw 'Invalid startup failure projection' }
    }
    if (@($result.ExportObservations).Count -gt 32) { throw 'Export observation limit' }
    if (@($result.ExportObservations | Where-Object { $_.Source -cne 'NativeExport' }).Count -ne 0 -or ($null -ne $result.ParentImportObservation -and $result.ParentImportObservation.Source -cne 'ParentImport')) { throw 'Invalid export/import observation origin' }
    $observations = @($result.ExportObservations)
    if ($null -ne $result.ParentImportObservation) { $observations += $result.ParentImportObservation }
    foreach ($item in $observations) {
        if (@($item.PSObject.Properties.Name).Count -ne 7 -or @($item.PSObject.Properties.Name | Where-Object { $_ -cnotin @('Source','Failure','Length','Capacity','Signature','Kind','Iid') }).Count -ne 0 -or $item.Source -cnotin @('NativeExport','ParentImport') -or $item.Failure -cnotin @('None','LengthLower','LengthUpper','Capacity','Signature','PacketType','Iid') -or $item.Signature -cnotin @('Missing','Match','Mismatch') -or $item.Kind -cnotin @('Missing','Standard','Handler','Custom','Extended','Unknown') -or $item.Iid -cnotin @('Missing','Match','Mismatch')) { throw 'Invalid export projection' }
        foreach ($name in @('Length','Capacity')) { if ($null -ne $item.$name -and (($item.$name -isnot [int] -and $item.$name -isnot [long]) -or $item.$name -lt 0 -or $item.$name -gt 131072)) { throw 'Invalid observed length' } }
    }
    if ($result.Status -ceq 'Pass') {
        if ($null -ne $result.StartupFailure -and ($result.StartupFailure.Stage -cne 'ParentRun' -or $result.StartupFailure.Exception -cne 'Cancelled')) { throw 'Unexpected receiver shutdown failure' }
        if (-not $result.ImportAttempted -or -not $result.Imported -or -not $result.IndependentParentSta -or -not $result.OwnedFinalized -or -not $result.ImporterReleased -or -not $result.DiagnosticReadSettled -or $result.DiagnosticReadFailure -cne 'None' -or $null -ne $result.ContractFailure -or $null -ne $result.ImportFailure -or $null -eq $result.ParentImportObservation -or @($result.ExportObservations).Count -ne 1) { throw 'Actual native import pass unconfirmed' }
        if ($result.ExportObservations[0].Length -ne $result.ParentImportObservation.Length -or $result.ParentImportObservation.Capacity -ne $result.ParentImportObservation.Length) { throw 'Inconsistent native packet length' }
        foreach ($item in $observations) { if ($item.Failure -cne 'None' -or $item.Signature -cne 'Match' -or $item.Kind -cne 'Standard' -or $item.Iid -cne 'Match' -or $null -eq $item.Length -or $item.Length -lt 72 -or $item.Length -gt 65536 -or $null -eq $item.Capacity -or $item.Capacity -lt $item.Length) { throw 'Invalid passing export observation' } }
    }
}
$app = Join-Path $OutputRoot 'app'
[void][IO.Directory]::CreateDirectory($app)
foreach ($project in @('src/KuaiYi.OleReceiver/KuaiYi.OleReceiver.csproj','src/KuaiYi.App/KuaiYi.App.csproj')) {
    Invoke-Checked dotnet @('restore',$project,'--locked-mode') | ForEach-Object { Write-Host $_ }
}
Invoke-Checked dotnet @('build','src/KuaiYi.OleReceiver/KuaiYi.OleReceiver.csproj','--configuration','Release','--no-restore') | ForEach-Object { Write-Host $_ }
Invoke-Checked dotnet @('build','src/KuaiYi.App/KuaiYi.App.csproj','--configuration','Release','--no-restore','--output',$app,'-p:KuaiYiIncludeWindowsCiProbe=true') | ForEach-Object { Write-Host $_ }
$receiver = Join-Path $script:RepositoryRoot 'src/KuaiYi.OleReceiver/bin/Release/net10.0-windows/win-x64'
foreach ($name in @('KuaiYi.OleReceiver.exe','KuaiYi.OleReceiver.dll','KuaiYi.OleReceiver.runtimeconfig.json','KuaiYi.OleReceiver.deps.json')) { Copy-Item -LiteralPath (Join-Path $receiver $name) -Destination $app }
Copy-Item -LiteralPath (Join-Path $NativeRoot 'KuaiYi.OleNativeBridge/Release/KuaiYi.OleNativeBridge.dll') -Destination $app
if (-not ('KuaiYiCiDiscard' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Threading.Tasks;
public static class KuaiYiCiDiscard {
    public static async Task<bool> Drain(Stream stream) {
        byte[] buffer = new byte[4096];
        try { while (await stream.ReadAsync(buffer, 0, buffer.Length) != 0) Array.Clear(buffer, 0, buffer.Length); return true; }
        catch { return false; }
        finally { Array.Clear(buffer, 0, buffer.Length); }
    }
}
'@
}
$receipt = Join-Path $OutputRoot 'marshal-report.json'
$info = [Diagnostics.ProcessStartInfo]::new((Join-Path $app 'KuaiYi.App.exe')); $info.UseShellExecute = $false
$info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
foreach ($arg in @('--ci-ole-marshal-contract',$receipt)) { [void]$info.ArgumentList.Add($arg) }
$heldApp = Get-Variable -Name UnconfirmedMarshalApp -Scope Script -ErrorAction SilentlyContinue
if ($null -ne $heldApp -and $null -ne $heldApp.Value) { if (-not $script:UnconfirmedMarshalApp.HasExited) { throw 'Previous owned marshal App exit unconfirmed' }; $script:UnconfirmedMarshalApp.Dispose(); $script:UnconfirmedMarshalApp = $null }
$child = [Diagnostics.Process]::Start($info)
$stdout = [KuaiYiCiDiscard]::Drain($child.StandardOutput.BaseStream); $stderr = [KuaiYiCiDiscard]::Drain($child.StandardError.BaseStream)
try {
    if (-not $child.WaitForExit(35000)) {
        $child.Kill($true)
        if (-not $child.WaitForExit(5000)) { throw 'Owned marshal App exit unconfirmed' }
        throw 'Owned marshal App deadline exceeded'
    }
    if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),1000)) { throw 'Owned marshal output drain deadline' }
    if (-not $stdout.IsCompletedSuccessfully -or -not $stderr.IsCompletedSuccessfully -or -not $stdout.Result -or -not $stderr.Result) { throw 'Owned marshal output drain unconfirmed' }
    if (-not (Test-Path -LiteralPath $receipt)) { throw 'Missing actual marshal receipt' }
    $result = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
    Assert-MarshalReceipt $result
    Write-Host 'KUAIYI_MARSHAL_REPORT_BEGIN'; Write-Host ($result | ConvertTo-Json -Depth 8 -Compress); Write-Host 'KUAIYI_MARSHAL_REPORT_END'
    if ($child.ExitCode -ne 0 -or $result.ExitCode -ne 0 -or $result.Status -cne 'Pass' -or -not $result.Imported -or -not $result.IndependentParentSta -or -not $result.ImporterReleased -or -not $result.OwnedFinalized -or -not $result.DiagnosticReadSettled -or $null -ne $result.ContractFailure -or $null -ne $result.ImportFailure) { throw 'Actual native marshal/export/import contract failed' }
    return $result
} finally { if ($child.HasExited) { $child.Dispose() } else { $script:UnconfirmedMarshalApp = $child; throw 'Owned marshal App retained; exit unconfirmed' } }
