param([switch]$RequireWindows)
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    Assert-SpecIntegrity
    Assert-Sdk
    $commands = 'dotnet', 'pwsh', 'node', 'npm', 'cmake', 'cl', 'MakeAppx.exe', 'SignTool.exe', 'ISCC.exe'
    $tools = [ordered]@{}
    foreach ($command in $commands) {
        $found = Get-Command $command -ErrorAction SilentlyContinue | Select-Object -First 1
        $tools[$command] = if ($found) { $found.Source } else { $null }
    }
    $report = [ordered]@{
        timestampUtc = [DateTime]::UtcNow.ToString('O')
        os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        sdk = (& dotnet --version).Trim()
        powershell = $PSVersionTable.PSVersion.ToString()
        tools = $tools
        windowsDesktopStatus = if ($IsWindows -and [Environment]::UserInteractive) { 'NotRun' } else { 'Blocked' }
        productTestsExecuted = $false
    }
    $directory = Join-Path $script:RepositoryRoot 'artifacts/environment'
    New-Item -ItemType Directory -Force $directory | Out-Null
    $report | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $directory 'environment.json')
    if ($RequireWindows) {
        Assert-WindowsDesktop
        foreach ($command in 'cmake', 'cl', 'MakeAppx.exe', 'SignTool.exe', 'ISCC.exe') {
            if (-not $tools[$command]) { throw "Blocked: Windows build prerequisite $command missing." }
        }
    }
} finally { Pop-Location }
