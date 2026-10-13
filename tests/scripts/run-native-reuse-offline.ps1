$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repo
try {
    & (Join-Path $PSScriptRoot 'run-selection-reuse-host.ps1')
    if($LASTEXITCODE -ne 0){throw 'Managed diagnostic checks failed.'}
    New-Item -ItemType Directory -Force 'artifacts/native-tests' | Out-Null
    foreach($name in @('reuse-request-lifetime-test','reuse-native-protocol-test')) {
        & g++ -std=c++20 -Wall -Wextra -Werror '-fsanitize=address,undefined' -fno-omit-frame-pointer "tests/native/$name.cpp" -o "artifacts/native-tests/$name"
        if($LASTEXITCODE -ne 0){throw 'Portable native source compilation failed.'}
        # Existing cloud ptrace restriction blocks LSAN; do not imply leak validation.
        $oldAsan=$env:ASAN_OPTIONS
        try {$env:ASAN_OPTIONS='detect_leaks=0'; & "./artifacts/native-tests/$name";if($LASTEXITCODE -ne 0){throw 'Portable native assertions failed.'}}
        finally {$env:ASAN_OPTIONS=$oldAsan}
    }
    & python3 tests/scripts/native-reuse-source.test.py
    if($LASTEXITCODE -ne 0){throw 'Frozen capture gates diverged.'}
    # Deliberately never builds/runs the Windows worker or instantiates a Windows Job.
} finally {Pop-Location}
