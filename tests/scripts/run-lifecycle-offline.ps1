Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repo
try {
    $emptySource=Join-Path $repo 'artifacts/selection-reuse-empty-source'
    [void](New-Item -ItemType Directory -Force $emptySource)
    & dotnet restore tools/KuaiYi.ReuseLifecycle/KuaiYi.ReuseLifecycle.csproj --locked-mode --source $emptySource -p:NuGetAudit=false -m:1 -nr:false -v quiet
    if($LASTEXITCODE -ne 0){throw 'Offline SDK reference restore failed.'}
    & dotnet build tools/KuaiYi.ReuseLifecycle/KuaiYi.ReuseLifecycle.csproj --no-restore -c Release -m:1 -nr:false -p:UseSharedCompilation=false -v quiet
    if($LASTEXITCODE -ne 0){throw 'Offline lifecycle source compilation failed.'}
    & dotnet tools/KuaiYi.ReuseLifecycle/bin/Release/net10.0/KuaiYi.ReuseLifecycle.dll --offline-tests
    if($LASTEXITCODE -ne 0){throw 'Offline lifecycle policy assertions failed.'}
    [void](New-Item -ItemType Directory -Force artifacts/native-tests)
    & g++ -std=c++20 -Wall -Wextra -Werror '-fsanitize=address,undefined' -fno-omit-frame-pointer tests/native/lifecycle-protocol-test.cpp -o artifacts/native-tests/lifecycle-protocol-test
    if($LASTEXITCODE -ne 0){throw 'Portable lifecycle packet compilation failed.'}
    $prior=$env:ASAN_OPTIONS
    try {$env:ASAN_OPTIONS='detect_leaks=0'; & ./artifacts/native-tests/lifecycle-protocol-test;if($LASTEXITCODE -ne 0){throw 'Portable packet assertions failed.'}}finally{$env:ASAN_OPTIONS=$prior}
    & python3 tests/scripts/lifecycle-no-target-source.test.py
    if($LASTEXITCODE -ne 0){throw 'Non-target entry source boundary failed.'}
} finally {Pop-Location}
