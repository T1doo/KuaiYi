param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    & "$PSScriptRoot/build.ps1" -Scope Managed -Configuration $Configuration
    $destination = Join-Path $script:RepositoryRoot 'artifacts/probes-win-x64'
    if (Test-Path $destination) { Remove-Item -Recurse -Force $destination }
    New-Item -ItemType Directory $destination | Out-Null
    foreach ($entry in @(
        @{ name = 'KuaiYi.CaptureBroker'; project = 'src/KuaiYi.CaptureBroker' },
        @{ name = 'KuaiYi.CompatibilityProbe'; project = 'tools/KuaiYi.CompatibilityProbe' },
        @{ name = 'KuaiYi.GrowthHarness'; project = 'tools/KuaiYi.GrowthHarness' }
    )) {
        $folder = Join-Path $destination $entry.name
        $arguments = @('publish', "$($entry.project)/$($entry.name).csproj", '--no-build', '--no-restore', '--configuration', $Configuration,
            '--runtime', 'win-x64', '--self-contained', 'false', '--output', $folder)
        if (-not $IsWindows) { $arguments += '/p:EnableWindowsTargeting=true' }
        Invoke-Checked dotnet $arguments
        $exe = Join-Path $folder "$($entry.name).exe"
        $bytes = [IO.File]::ReadAllBytes($exe)
        if ($bytes.Length -lt 128 -or [BitConverter]::ToUInt16($bytes, 0) -ne 0x5A4D) { throw "Not a Windows PE: $exe" }
        $pe = [BitConverter]::ToInt32($bytes, 0x3c)
        if ($pe -lt 0 -or $pe -gt $bytes.Length - 26 -or [BitConverter]::ToUInt32($bytes, $pe) -ne 0x4550 -or
            [BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes, $pe + 24) -ne 0x20B) {
            throw "Not a Windows x64 PE32+ executable: $exe"
        }
    }
    Copy-Item assets/fixtures/selection-public.txt $destination
    Copy-Item docs/windows-probes.md (Join-Path $destination 'README.md')
    Copy-Item docs/compatibility-matrix.json $destination
    $files = Get-ChildItem $destination -Recurse -File | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($destination, $_.FullName); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    @{ type = 'P0 framework-dependent Windows x64 probes'; requiredRuntime = '.NET 10 Windows Desktop x64';
       desktopExecution = 'NotRun'; productInstaller = $false; files = @($files) } |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $destination 'manifest.json') -Encoding utf8NoBOM
    $archive = Join-Path $script:RepositoryRoot 'artifacts/KuaiYi-P0-probes-win-x64.zip'
    Compress-Archive -Path "$destination/*" -DestinationPath $archive -Force
    Get-FileHash $archive -Algorithm SHA256 | Format-List
    Write-Output 'Packed P0 probes only; no installer, signing, Windows runtime test or product acceptance is claimed.'
} finally { Pop-Location }
