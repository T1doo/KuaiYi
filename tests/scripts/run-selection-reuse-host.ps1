$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Pure 57 remain a distinct mode. This then executes only the 26 real synthetic-process cases.
& (Join-Path $PSScriptRoot 'run-selection-reuse-offline.ps1')
if($LASTEXITCODE -ne 0){throw 'Pure prototype prerequisites failed.'}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repo
try {
    & dotnet tools/KuaiYi.SelectionReusePrototype/bin/Release/net10.0/KuaiYi.SelectionReusePrototype.dll --host-tests
    if($LASTEXITCODE -ne 0){throw 'Synthetic host assertions failed.'}
} finally {Pop-Location}
