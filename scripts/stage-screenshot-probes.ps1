param([ValidateSet('Debug','Release')][string]$Configuration='Release', [string]$ArtifactName='KuaiYi-P0-capture-host-managed-win-x64.zip')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    # Reuse actual App + ImageWorker publication. ManagedOnly never registers/installs or pretends native capture/OCR exists.
    & "$PSScriptRoot/stage-identity-probes.ps1" -Configuration $Configuration -ManagedOnly
    $root=Join-Path $script:RepositoryRoot 'artifacts/screenshot-probes'
    if(Test-Path $root){Remove-Item -LiteralPath $root -Recurse -Force}
    New-Item -ItemType Directory -Force $root | Out-Null
    Copy-Item -LiteralPath 'artifacts/identity-probes/install' -Destination (Join-Path $root 'app') -Recurse
    Copy-Item -LiteralPath 'src/KuaiYi.CaptureNative' -Destination (Join-Path $root 'capture-native-source') -Recurse
    $files=@(Get-ChildItem $root -Recurse -File | Sort-Object FullName | ForEach-Object {
        @{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    @{Kind='P0ExplicitScreenshotReview';FrameworkDependent=$true;Installer=$false;CaptureNative='SourceOnly;WindowsBuildNotRun';OcrNative='NotIncluded';WindowsRuntime='NotRun';Hotkey='SourceOnly;NotRegistered';ModelCalls='None';Files=$files} |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'review-manifest.json') -Encoding utf8NoBOM
    if($ArtifactName -ne [IO.Path]::GetFileName($ArtifactName) -or $ArtifactName -eq 'KuaiYi-P0-screenshot-managed-win-x64.zip'){throw 'Use a distinct simple archive name; preserve the reviewed screenshot baseline.'}
    Compress-Archive -Path (Join-Path $root '*') -DestinationPath (Join-Path 'artifacts' $ArtifactName) -Force
    Write-Output 'Staged managed App/Worker and native capture source only; no OS registration, permission, capture, credentials or model call.'
} finally {Pop-Location}
