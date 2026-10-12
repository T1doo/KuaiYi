param([ValidateSet('Debug','Release')][string]$Configuration='Release')
. "$PSScriptRoot/common.ps1"
Assert-SpecIntegrity
Assert-Sdk
Push-Location $script:RepositoryRoot
try {
    $archivePath='artifacts/KuaiYi-P4-completion-races-managed-win-x64.zip'
    $baseline=Get-Content -LiteralPath 'docs/test-evidence/2026-10-12-p4-phase-timeouts-summary.json' -Raw | ConvertFrom-Json
    $historicalArchives=@($baseline.historicalArchives)+@($baseline.archive)
    if($historicalArchives.archive -contains $archivePath){throw 'Current output would overwrite a historical archive.'}
    function Assert-OriginalArchives {
        foreach($item in $historicalArchives) {
            if(-not(Test-Path -LiteralPath $item.archive) -or (Get-Item -LiteralPath $item.archive).Length -ne $item.bytes -or (Get-FileHash -LiteralPath $item.archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256){throw "Historical archive bytes changed: $($item.archive)"}
        }
    }
    Assert-OriginalArchives
    $root=Join-Path $script:RepositoryRoot 'artifacts/p4-completion-races-review'
    if(Test-Path $root){Remove-Item -LiteralPath $root -Recurse -Force} # This review's own output only; no historical archive is replaced.
    $app=Join-Path $root 'app';New-Item -ItemType Directory -Force $app | Out-Null
    foreach($project in 'KuaiYi.App','KuaiYi.ImageWorker','KuaiYi.CaptureHost','KuaiYi.OleReceiver','KuaiYi.CaptureBroker','KuaiYi.ClipboardHost') {
        $output=Join-Path $root "$project-publish"
        Invoke-Checked dotnet @('restore',"src/$project/$project.csproj",'--locked-mode','/p:EnableWindowsTargeting=true')
        Invoke-Checked dotnet @('build',"src/$project/$project.csproj",'--no-restore','-c',$Configuration,'/p:EnableWindowsTargeting=true')
        Invoke-Checked dotnet @('publish',"src/$project/$project.csproj",'--no-build','--no-restore','-c',$Configuration,'-r','win-x64','--self-contained','false','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:EnableWindowsTargeting=true','-o',$output)
        foreach($file in Get-ChildItem -LiteralPath $output -File) {
            $destination=Join-Path $app $file.Name
            if((Test-Path -LiteralPath $destination) -and (Get-FileHash $destination).Hash -ne (Get-FileHash $file.FullName).Hash){throw "Conflicting dependency: $($file.Name)"}
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
        Remove-Item -LiteralPath $output -Recurse -Force
    }
    Copy-Item -LiteralPath 'packaging/identity/Assets' -Destination $app -Recurse
    Copy-Item -LiteralPath 'assets/config' -Destination (Join-Path $root 'settings-format') -Recurse
    Copy-Item -LiteralPath 'docs/p4-completion-races-windows-checklist.md' -Destination (Join-Path $root 'WINDOWS-CHECKS.md')
    foreach($name in 'KuaiYi.App.exe','KuaiYi.ImageWorker.exe','KuaiYi.NativeHost.exe','KuaiYi.CaptureHost.exe','KuaiYi.OleReceiver.exe','KuaiYi.CaptureBroker.exe','KuaiYi.ClipboardHost.exe') {
        $bytes=[IO.File]::ReadAllBytes((Join-Path $app $name));$offset=[BitConverter]::ToInt32($bytes,0x3c)
        if($bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a -or [BitConverter]::ToUInt32($bytes,$offset) -ne 0x4550 -or [BitConverter]::ToUInt16($bytes,$offset+4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes,$offset+24) -ne 0x20b){throw "Not PE32+ x64: $name"}
    }
    $probe=Join-Path $root 'public-probe'
    Invoke-Checked dotnet @('restore','tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj','--locked-mode','/p:EnableWindowsTargeting=true')
    Invoke-Checked dotnet @('build','tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj','--no-restore','-c',$Configuration,'/p:EnableWindowsTargeting=true')
    Invoke-Checked dotnet @('publish','tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj','--no-build','--no-restore','-c',$Configuration,'-r','win-x64','--self-contained','false','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:EnableWindowsTargeting=true','-o',$probe)
    $mock=Join-Path $root 'test-provider'
    Invoke-Checked dotnet @('restore','tools/KuaiYi.MockProvider/KuaiYi.MockProvider.csproj','--locked-mode')
    Invoke-Checked dotnet @('build','tools/KuaiYi.MockProvider/KuaiYi.MockProvider.csproj','--no-restore','-c',$Configuration)
    Invoke-Checked dotnet @('publish','tools/KuaiYi.MockProvider/KuaiYi.MockProvider.csproj','--no-build','--no-restore','-c',$Configuration,'--self-contained','false','-p:UseAppHost=false','-o',$mock)
    Copy-Item -LiteralPath 'docs/p4-completion-races-review.md' -Destination (Join-Path $root 'REVIEW.md')
    $files=@(Get-ChildItem $root -Recurse -File | Sort-Object FullName | ForEach-Object {
        @{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    @{Kind='P4CompletionRacesReview';FrameworkDependent=$true;Installer=$false;NativeDLLs='NotIncluded';WindowsRuntime='NotRun';P0='Blocked';P1='InProgress';P2='InProgress';P3='InProgress';P4='InProgress';ModelCalls='None';TestHttp='Real loopback only';Credentials='No real credentials';Registration='None';Files=$files} |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'review-manifest.json') -Encoding utf8NoBOM
    Compress-Archive -Path (Join-Path $root '*') -DestinationPath $archivePath -Force
    Assert-OriginalArchives
    Write-Output 'Production absolute deadline checks at connection/header/body/parse/event delivery boundaries with deferred timer and cancellation race evidence; original immutable local authorization remains. This staging writes only its own output; native DLL/Windows Drop/remote CI remain NotRun.'
} finally {Pop-Location}
