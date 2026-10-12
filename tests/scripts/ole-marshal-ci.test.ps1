$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$file = Join-Path $root 'scripts/run-windows-ole-marshal-ci.ps1'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($file,[ref]$tokens,[ref]$errors)
if (@($errors).Count -ne 0) { throw 'Marshal helper parse failed' }
$fn = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Assert-MarshalReceipt' },$true)
Invoke-Expression $fn.Extent.Text
$passes = 0
function Check([string]$name,[scriptblock]$test) { & $test; $script:passes++; Write-Host "PASS $name" }
function Reject([string]$name,[scriptblock]$mutate) {
    $r = $script:valid | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    & $mutate $r
    $failed = $false
    try { Assert-MarshalReceipt $r } catch { $failed = $true }
    if (-not $failed) { throw "Expected rejection: $name" }
    $script:passes++; Write-Host "PASS rejects $name"
}
$export = @{ Source = 'NativeExport'; Failure = 'None'; Length = 72; Capacity = 80; Signature = 'Match'; Kind = 'Standard'; Iid = 'Match' }
$import = @{ Source = 'ParentImport'; Failure = 'None'; Length = 72; Capacity = 72; Signature = 'Match'; Kind = 'Standard'; Iid = 'Match' }
$valid = [pscustomobject]@{
    Kind = 'OwnedNativeMarshalContract'; Status = 'Pass'; ModelCalls = 0; ExportSource = 'ActualNativeTargetInOwnedReceiver'
    ProductionValidator = $true; ProductionImport = $true; ImportAttempted = $true; Imported = $true; IndependentParentSta = $true
    ImporterReleased = $true; OwnedFinalized = $true; ReceiverExitCode = 0; DiagnosticReadSettled = $true; DiagnosticReadFailure = 'None'
    ExportObservations = @($export); ParentImportObservation = $import; StartupFailure = $null; ContractFailure = $null; ImportFailure = $null
    RegistrationAttempted = $false; ActualDrop = 'NotRun'; GetData = 'NotRun'; CloseRecovery = 'NotRun'; PhysicalE06 = 'NotRun'; Windows11Acceptance = 'NotRun'; ExitCode = 0
}
$valid = $valid | ConvertTo-Json -Depth 10 | ConvertFrom-Json
Check 'valid actual import receipt shape' { Assert-MarshalReceipt $valid }
Check 'failed length receipt is preserved without pass claim' {
    $r = $valid | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $r.Status = 'Fail'; $r.ExitCode = 3; $r.Imported = $false; $r.ImportAttempted = $false; $r.IndependentParentSta = $false; $r.ParentImportObservation = $null
    $r.ExportObservations[0].Failure = 'LengthLower'; $r.ExportObservations[0].Length = 68
    Assert-MarshalReceipt $r
}
Reject 'worker identity field' { param($r) $r | Add-Member PID 123 }
Reject 'raw stderr field' { param($r) $r | Add-Member RawStderr 'PUBLIC_SENTINEL' }
Reject 'OID in observation' { param($r) $r.ExportObservations[0] | Add-Member OID 'PUBLIC_SENTINEL' }
Reject 'wrong source category' { param($r) $r.ExportObservations[0].Source = 'PUBLIC_SENTINEL' }
Reject 'wrong exporter origin' { param($r) $r.ExportObservations[0].Source = 'ParentImport' }
Reject 'wrong importer origin' { param($r) $r.ParentImportObservation.Source = 'NativeExport' }
Reject 'different native packet lengths' { param($r) $r.ParentImportObservation.Length = 73; $r.ParentImportObservation.Capacity = 73 }
Reject 'parent capacity differs from packet' { param($r) $r.ParentImportObservation.Capacity = 80 }
Reject 'unknown signature' { param($r) $r.ExportObservations[0].Signature = 'PUBLIC_SENTINEL' }
Reject 'unknown packet kind' { param($r) $r.ExportObservations[0].Kind = 'PUBLIC_SENTINEL' }
Reject 'oversized observed length' { param($r) $r.ExportObservations[0].Length = 131073 }
Reject 'negative observed capacity' { param($r) $r.ExportObservations[0].Capacity = -1 }
Reject 'floating length' { param($r) $r.ExportObservations[0].Length = 72.5 }
Reject 'raw failure message' { param($r) $r.ContractFailure = [pscustomobject]@{ Stage = 'ParentRegister'; Exception = 'Io'; HResult = 0; Message = 'PUBLIC_SENTINEL' } }
Reject 'arbitrary type name' { param($r) $r.ContractFailure = [pscustomobject]@{ Stage = 'ParentRegister'; Exception = 'PUBLIC_SENTINEL'; HResult = 0 } }
Reject 'false import pass' { param($r) $r.Imported = $false }
Reject 'synthetic header pass before import attempt' { param($r) $r.ImportAttempted = $false }
Reject 'unsettled read pass' { param($r) $r.DiagnosticReadSettled = $false }
Reject 'unconfirmed owner pass' { param($r) $r.OwnedFinalized = $false }
Reject 'failed packet accepted as pass' { param($r) $r.ExportObservations[0].Failure = 'LengthLower'; $r.ExportObservations[0].Length = 68 }
Reject 'physical claim' { param($r) $r.PhysicalE06 = 'Pass' }
Reject 'drag registration claim' { param($r) $r.RegistrationAttempted = $true }
Reject 'string boolean' { param($r) $r.Imported = 'true' }
Reject 'too many records' { param($r) $r.ExportObservations = @(1..33 | ForEach-Object { $export }) }
Reject 'unexpected shutdown EOF' { param($r) $r.StartupFailure = [pscustomobject]@{ Stage = 'ParentRun'; Exception = 'Io'; HResult = -2147024858 } }
Check 'expected owned cancellation is distinct from startup failure' {
    $r = $valid | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $r.StartupFailure = [pscustomobject]@{ Stage = 'ParentRun'; Exception = 'Cancelled'; HResult = -2146233029 }
    Assert-MarshalReceipt $r
}
$compile = $ast.Find({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq 'Add-Type' },$true)
$source = ($compile.CommandElements | Where-Object { $_ -is [Management.Automation.Language.StringConstantExpressionAst] -and $_.StringConstantType -eq 'SingleQuotedHereString' }).Value
Add-Type -TypeDefinition $source
Check 'actual discard drain clears and consumes synthetic flood' {
    $stream = [IO.MemoryStream]::new([byte[]]::new(1048576))
    try { if (-not [KuaiYiCiDiscard]::Drain($stream).GetAwaiter().GetResult() -or $stream.Position -ne $stream.Length) { throw 'Drain failed' } }
    finally { $stream.Dispose() }
}
Write-Host "MARSHAL_SCRIPT_CONTRACTS passed=$passes failed=0; synthetic schema and drain only"
