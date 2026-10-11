param([ValidateSet('Debug','Release')][string]$Configuration='Release')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    & "$PSScriptRoot/stage-identity-probes.ps1" -Configuration $Configuration -ManagedOnly
    $root=Join-Path $script:RepositoryRoot 'artifacts/settings-review'
    if(Test-Path $root){Remove-Item -LiteralPath $root -Recurse -Force}
    New-Item -ItemType Directory -Force $root | Out-Null
    Copy-Item -LiteralPath 'artifacts/identity-probes/install' -Destination (Join-Path $root 'app') -Recurse
    Copy-Item -LiteralPath 'assets/config' -Destination (Join-Path $root 'settings-format') -Recurse
    $files=@(Get-ChildItem $root -Recurse -File | Sort-Object FullName | ForEach-Object {
        @{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    @{Kind='P1SettingsIndependentReview';FrameworkDependent=$true;Installer=$false;NativeDLLs='NotIncluded';WindowsWpfDpapiAcl='NotRun';P0='Blocked';P1='InProgress';ModelCalls='None';Credentials='None';Registration='None';Files=$files} |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'review-manifest.json') -Encoding utf8NoBOM
    Compress-Archive -Path (Join-Path $root '*') -DestinationPath 'artifacts/KuaiYi-P1-settings-managed-win-x64.zip' -Force
    Write-Output 'Settings review only. No installer, OS registration, real credentials or model calls; Windows tests remain NotRun.'
} finally {Pop-Location}
