param([ValidateSet('Debug','Release')][string]$Configuration='Release')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    & "$PSScriptRoot/stage-identity-probes.ps1" -Configuration $Configuration -ManagedOnly
    $root=Join-Path $script:RepositoryRoot 'artifacts/ole-presentation-probes'
    if(Test-Path $root){Remove-Item -LiteralPath $root -Recurse -Force}
    New-Item -ItemType Directory -Force $root | Out-Null
    Copy-Item -LiteralPath 'artifacts/identity-probes/install' -Destination (Join-Path $root 'app') -Recurse
    Copy-Item -LiteralPath 'src/KuaiYi.OleNativeBridge' -Destination (Join-Path $root 'ole-native-source') -Recurse
    $files=@(Get-ChildItem $root -Recurse -File | Sort-Object FullName | ForEach-Object {
        @{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    @{Kind='P0E06OlePresentationReview';FrameworkDependent=$true;Installer=$false;NativeBridge='SourceOnly;MSVCBuildNotRun';WindowsComAndInput='NotRun';E06='NotRun';CaptureNative='NotIncluded';OcrNative='NotIncluded';Registration='None';ModelCalls='None';Files=$files} |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'review-manifest.json') -Encoding utf8NoBOM
    Compress-Archive -Path (Join-Path $root '*') -DestinationPath 'artifacts/KuaiYi-P0-ole-presentation-managed-win-x64.zip' -Force
    Write-Output 'Staged review only; native bridge must be built on Windows. No install/OS registration/credentials/model call.'
} finally {Pop-Location}
