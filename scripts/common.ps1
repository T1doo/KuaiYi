Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:RepositoryRoot = Split-Path $PSScriptRoot -Parent
function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE." }
}
function Assert-SpecIntegrity {
    $spec = Join-Path $script:RepositoryRoot 'SPEC.md'
    $original = Join-Path $script:RepositoryRoot '快译_产品与技术开发规格_v1.0.md'
    $expected = 'EA3AA6529405BCC703986988FD089F476EE8F991D867E2220B3B20C439126166'
    if ((Get-FileHash $spec -Algorithm SHA256).Hash -ne $expected -or (Get-FileHash $original -Algorithm SHA256).Hash -ne $expected) {
        throw 'SPEC or original source has changed; update the explicit specification decision and traceability.'
    }
    $trace = Get-Content (Join-Path $script:RepositoryRoot 'docs/traceability.json') -Raw | ConvertFrom-Json
    if ($trace.items.Count -ne 176) { throw 'Expected 18 requirements + 52 tasks + 106 acceptance cases.' }
    $duplicates = $trace.items | Group-Object id | Where-Object Count -gt 1
    if ($duplicates) { throw 'Duplicate traceability IDs.' }
}
function Assert-Sdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is not on PATH. Activate the documented SDK directory first.' }
    $required = (Get-Content (Join-Path $script:RepositoryRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    $actual = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $actual -ne $required) { throw "Required SDK $required; found $actual." }
}
function Assert-WindowsDesktop {
    if (-not $IsWindows) { throw 'Blocked: requires Windows 11 x64 with an interactive user desktop; Linux checks are not an equivalent.' }
    if (-not [Environment]::UserInteractive -or [System.Diagnostics.Process]::GetCurrentProcess().SessionId -eq 0) {
        throw 'Blocked: service/session-0 execution cannot validate desktop interaction.'
    }
}
