$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repo
try {
    # Existing locked dependencies must already be restored. No installation or network restore here.
    & dotnet build tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release -m:1 -nr:false -p:UseSharedCompilation=false -v quiet
    if($LASTEXITCODE -ne 0){throw 'Offline test source build failed.'}
    # This runner has no NuGet packages. SDK reference packs must already be installed.
    $emptySource=Join-Path $repo 'artifacts/selection-reuse-empty-source'
    New-Item -ItemType Directory -Force $emptySource | Out-Null
    & dotnet restore tools/KuaiYi.SelectionReusePrototype/KuaiYi.SelectionReusePrototype.csproj --locked-mode --source $emptySource -p:NuGetAudit=false -m:1 -nr:false -v quiet
    if($LASTEXITCODE -ne 0){throw 'Offline runner SDK restore failed.'}
    & dotnet build tools/KuaiYi.SelectionReusePrototype/KuaiYi.SelectionReusePrototype.csproj --no-restore -c Release -m:1 -nr:false -p:UseSharedCompilation=false -v quiet
    if($LASTEXITCODE -ne 0){throw 'Offline runner build failed.'}
    & dotnet tools/KuaiYi.SelectionReusePrototype/bin/Release/net10.0/KuaiYi.SelectionReusePrototype.dll
    if($LASTEXITCODE -ne 0){throw 'Offline prototype assertions failed.'}
} finally {Pop-Location}
