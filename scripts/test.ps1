param([ValidateSet('Offline','Windows','Live')][string]$Suite = 'Offline',
      [ValidateSet('Debug','Release')][string]$Configuration = 'Release')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
$process = $null
$oldProvider = [Environment]::GetEnvironmentVariable('KUAIYI_TEST_PROVIDER_URL')
try {
    Assert-SpecIntegrity
    Assert-Sdk
    if ($Suite -eq 'Windows') { Assert-WindowsDesktop; throw 'NotRun: Windows integration test runner is not implemented in this P0 slice.' }
    if ($Suite -eq 'Live') { throw 'Blocked: live API testing needs separate explicit authorization; this script does not read credentials.' }
    & "$PSScriptRoot/build.ps1" -Scope Offline -Configuration $Configuration
    $folder = Join-Path $script:RepositoryRoot 'artifacts/test-results/offline'
    New-Item -ItemType Directory -Force $folder | Out-Null
    $stdout = Join-Path $folder 'mock-stdout.log'
    $stderr = Join-Path $folder 'mock-stderr.log'
    $mock = Join-Path $script:RepositoryRoot "tools/KuaiYi.MockProvider/bin/$Configuration/net10.0/KuaiYi.MockProvider.dll"
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($mock) -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    $uri = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw 'Mock Provider exited before readiness; inspect artifacts/test-results/offline/mock-stderr.log.' }
        if (Test-Path $stdout) {
            $line = Get-Content $stdout | Where-Object { $_.StartsWith('KUAIYI_MOCK_URL=') } | Select-Object -First 1
            if ($line) { $uri = $line.Substring('KUAIYI_MOCK_URL='.Length); break }
        }
        Start-Sleep -Milliseconds 50
    }
    if (-not $uri) { throw 'Mock Provider readiness timed out.' }
    [Environment]::SetEnvironmentVariable('KUAIYI_TEST_PROVIDER_URL', $uri)
    foreach ($name in 'KuaiYi.Core.Tests', 'KuaiYi.Provider.Tests') {
        Invoke-Checked dotnet @('test', "tests/$name/$name.csproj", '--no-build', '--no-restore', '--configuration', $Configuration,
            '--logger', "trx;LogFileName=$name.trx", '--results-directory', $folder)
    }
    if(-not(Get-Command node -ErrorAction SilentlyContinue)){throw 'Node is required for the actual extension protocol tests; see docs/environment.md.'}
    Invoke-Checked node @('--test','--test-concurrency=1','--test-reporter=spec','--test-reporter-destination=stdout',
        '--test-reporter=junit',"--test-reporter-destination=$(Join-Path $folder 'browser-tests.xml')",'tests/browser/protocol.test.mjs','tests/browser/confirmation.test.mjs')
} finally {
    [Environment]::SetEnvironmentVariable('KUAIYI_TEST_PROVIDER_URL', $oldProvider)
    if ($process -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    if ($process) { $process.Dispose() }
    Pop-Location
}
