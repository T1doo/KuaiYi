param([ValidateSet('Debug','Release')][string]$Configuration='Release', [switch]$ManagedOnly)
. "$PSScriptRoot/common.ps1"
Assert-SpecIntegrity
Assert-Sdk
Push-Location $script:RepositoryRoot
try {
    $root=Join-Path $script:RepositoryRoot 'artifacts/identity-probes'
    $main=Join-Path $root 'main';$worker=Join-Path $root 'worker';$capture=Join-Path $root 'capture';$ole=Join-Path $root 'ole';$stage=Join-Path $root 'install'
    if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force } # Own build artifacts only.
    New-Item -ItemType Directory -Force $stage | Out-Null
    foreach ($entry in @(@{Project='KuaiYi.App';Output=$main},@{Project='KuaiYi.ImageWorker';Output=$worker},@{Project='KuaiYi.CaptureHost';Output=$capture},@{Project='KuaiYi.OleReceiver';Output=$ole})) {
        # P0 portable review artifact, framework dependent; full installer self-contained publication remains P8.
        Invoke-Checked dotnet @('restore',"src/$($entry.Project)/$($entry.Project).csproj",'--locked-mode','/p:EnableWindowsTargeting=true')
        Invoke-Checked dotnet @('build',"src/$($entry.Project)/$($entry.Project).csproj",'--no-restore','-c',$Configuration,'/p:EnableWindowsTargeting=true')
        Invoke-Checked dotnet @('publish',"src/$($entry.Project)/$($entry.Project).csproj",'--no-build','--no-restore','-c',$Configuration,'-r','win-x64','--self-contained','false','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:EnableWindowsTargeting=true','-o',$entry.Output)
        foreach ($file in Get-ChildItem -LiteralPath $entry.Output -File) {
            $destination=Join-Path $stage $file.Name
            if ((Test-Path -LiteralPath $destination) -and (Get-FileHash $destination).Hash -ne (Get-FileHash $file.FullName).Hash) { throw "Conflicting shared dependency: $($file.Name)" }
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    Copy-Item -LiteralPath (Join-Path $script:RepositoryRoot 'packaging/identity/Assets') -Destination $stage -Recurse
    Copy-Item -LiteralPath (Join-Path $script:RepositoryRoot 'tests/fixtures/images/kuaiyi-test.png') -Destination (Join-Path $root 'kuaiyi-test.png')
    if (-not $ManagedOnly) {
        Assert-WindowsDesktop
        foreach ($component in 'KuaiYi.OcrNative','KuaiYi.ShellExtension','KuaiYi.CaptureNative','KuaiYi.OleNativeBridge') {
            $dll=Join-Path $script:RepositoryRoot "artifacts/native/$component/$Configuration/$component.dll"
            if (-not (Test-Path -LiteralPath $dll)) { throw "Native component missing: $component; run build.ps1 -Scope All on Windows." }
            Copy-Item -LiteralPath $dll -Destination $stage
        }
        & "$PSScriptRoot/validate-dev-identity.ps1" -InstallRoot $stage
    }
    $files=@(Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object { @{Path=[IO.Path]::GetRelativePath($stage,$_.FullName);Sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant();Bytes=$_.Length} })
    @{Kind='P0IdentityReview';ManagedOnly=[bool]$ManagedOnly;FrameworkDependent=$true;NativeBuild= $(if($ManagedOnly){'Blocked'}else{'NotRun'});WindowsRuntime='NotRun';Installer='NotStarted';Files=$files} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'manifest.json')
    Compress-Archive -Path (Join-Path $root 'install'),(Join-Path $root 'manifest.json'),(Join-Path $root 'kuaiyi-test.png') -DestinationPath (Join-Path $script:RepositoryRoot 'artifacts/KuaiYi-P0-identity-managed-win-x64.zip') -Force
    Write-Output 'Staged P0 review files; no signing, trust, registration, installation or Windows runtime acceptance.'
} finally { Pop-Location }
