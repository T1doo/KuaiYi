param([ValidateSet('Debug','Release')][string]$Configuration='Release')
. "$PSScriptRoot/common.ps1"
Assert-SpecIntegrity
Assert-Sdk
Push-Location $script:RepositoryRoot
try {
    $root=Join-Path $script:RepositoryRoot 'artifacts/p1-shell-review'
    if(Test-Path $root){Remove-Item -LiteralPath $root -Recurse -Force} # This review's own output only; no historical archive is replaced.
    $app=Join-Path $root 'app';New-Item -ItemType Directory -Force $app | Out-Null
    foreach($project in 'KuaiYi.App','KuaiYi.ImageWorker','KuaiYi.CaptureHost','KuaiYi.OleReceiver') {
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
    Copy-Item -LiteralPath 'docs/p1-shell-windows-checklist.md' -Destination (Join-Path $root 'WINDOWS-CHECKS.md')
    foreach($name in 'KuaiYi.App.exe','KuaiYi.ImageWorker.exe','KuaiYi.NativeHost.exe','KuaiYi.CaptureHost.exe','KuaiYi.OleReceiver.exe') {
        $bytes=[IO.File]::ReadAllBytes((Join-Path $app $name));$offset=[BitConverter]::ToInt32($bytes,0x3c)
        if($bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a -or [BitConverter]::ToUInt32($bytes,$offset) -ne 0x4550 -or [BitConverter]::ToUInt16($bytes,$offset+4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes,$offset+24) -ne 0x20b){throw "Not PE32+ x64: $name"}
    }
    $files=@(Get-ChildItem $root -Recurse -File | Sort-Object FullName | ForEach-Object {
        @{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    @{Kind='P1ShellReview';FrameworkDependent=$true;Installer=$false;NativeDLLs='NotIncluded';WindowsRuntime='NotRun';P0='Blocked';P1='InProgress';ModelCalls='None';Credentials='None';Registration='None';Files=$files} |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'review-manifest.json') -Encoding utf8NoBOM
    Compress-Archive -Path (Join-Path $root '*') -DestinationPath 'artifacts/KuaiYi-P1-shell-managed-win-x64.zip' -Force
    Write-Output 'P1 shell review only. Historical archives untouched; no installation, registration, model call or Windows runtime claim.'
} finally {Pop-Location}
