param([string]$BuildDirectory='artifacts/native-reuse-build')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(-not $IsWindows){throw 'Windows x64 MSVC/SDK required; no substitute build or UIA run.'}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repo
try {
    & cmake -S tools/KuaiYi.SelectionReuseNative -B $BuildDirectory -G 'Visual Studio 17 2022' -A x64
    if($LASTEXITCODE -ne 0){throw 'Native diagnostic configuration failed.'}
    & cmake --build $BuildDirectory --config Release
    if($LASTEXITCODE -ne 0){throw 'Native diagnostic compilation failed.'}
    if(-not (Test-Path (Join-Path $BuildDirectory 'Release/KuaiYi.SelectionReuseNative.exe'))){throw 'Native diagnostic binary missing.'}
    # Build only. Deliberately never starts a worker, Job, UIA or experiment.
} finally {Pop-Location}
