param([ValidateSet('Debug','Release')][string]$Configuration='Release')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    Assert-SpecIntegrity
    Assert-Sdk
    $root=Join-Path $script:RepositoryRoot 'artifacts/browser-probes'
    if(Test-Path $root){Remove-Item $root -Recurse -Force}
    $hostFolder=Join-Path $root 'host';$extension=Join-Path $root 'extension'
    New-Item -ItemType Directory -Force $hostFolder,$extension | Out-Null
    Invoke-Checked dotnet @('restore','src/KuaiYi.NativeHost/KuaiYi.NativeHost.csproj','--locked-mode')
    Invoke-Checked dotnet @('publish','src/KuaiYi.NativeHost/KuaiYi.NativeHost.csproj','--no-restore','-c',$Configuration,'--self-contained','false','-o',$hostFolder)
    # Explicit production file list; test fixtures, tests and synthetic origins never enter review distribution.
    foreach($name in 'manifest.json','service-worker.js','protocol.js','acquire.js','options.html','options.js','icon.png') {
        Copy-Item (Join-Path 'extensions/chromium' $name) $extension
    }
    $manifest=Get-Content (Join-Path $extension 'manifest.json') -Raw | ConvertFrom-Json
    if(($manifest.permissions -join ',') -ne 'contextMenus,nativeMessaging,activeTab'){throw 'Unexpected extension permissions.'}
    foreach($field in 'host_permissions','optional_host_permissions','externally_connectable','content_scripts','web_accessible_resources','key') {
        if($manifest.PSObject.Properties.Name -contains $field){throw "Unexpected extension manifest entry: $field"}
    }
    $files=@(Get-ChildItem $root -File -Recurse | Sort-Object FullName | ForEach-Object {
        @{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    @{Kind='P0BrowserProtocolReview';FrameworkDependent=$true;NativeRegistration='NotConfigured';ActualExtensionId='NotConfigured';DesktopGrantSource='NotImplementedInApp';WindowsRuntime='NotRun';Installer=$false;Files=$files} |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'review-manifest.json') -Encoding utf8NoBOM
    Compress-Archive -Path (Join-Path $root 'host'),$extension,(Join-Path $root 'review-manifest.json') -DestinationPath 'artifacts/KuaiYi-P0-browser-managed-win-x64.zip' -Force
    Write-Output 'Staged browser/NativeHost source review distribution only; no registration, extension install, activation grant or API call.'
} finally {Pop-Location}
