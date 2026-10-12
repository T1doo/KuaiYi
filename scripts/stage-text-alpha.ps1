param([ValidateSet('Debug','Release')][string]$Configuration='Release',[switch]$InitializeAlphaLocks)
. "$PSScriptRoot/common.ps1"
. "$PSScriptRoot/text-alpha-common.ps1"
Assert-SpecIntegrity
Assert-Sdk
Push-Location $script:RepositoryRoot
try {
    $contract=Get-Content -LiteralPath 'packaging/text-alpha.json' -Raw | ConvertFrom-Json
    $run=[Guid]::NewGuid().ToString('N')
    $work=Join-Path $script:RepositoryRoot "artifacts/text-alpha/$run"
    $root=Join-Path $work 'bundle'
    if(Test-Path -LiteralPath $work){throw 'Unique Alpha work directory already exists.'}
    New-Item -ItemType Directory -Path $root | Out-Null
    $provenance=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach($role in $contract.roles) {
        $publish=Join-Path $work "publish/$($role.name)"
        $props=@('-p:KuaiYiTextAlpha=true','-p:SelfContained=true','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:PublishReadyToRun=false',"-p:RuntimeFrameworkVersion=$($contract.runtime)")
        if(-not $IsWindows){$props+='-p:EnableWindowsTargeting=true'}
        $restore=@('restore',$role.project,'-r',$contract.rid)
        if($InitializeAlphaLocks){$restore+='--force-evaluate'}else{$restore+='--locked-mode'}
        Invoke-Checked dotnet ($restore+$props)
        Invoke-Checked dotnet (@('publish',$role.project,'--no-restore','-c',$Configuration,'-r',$contract.rid,'--self-contained','true','-o',$publish)+$props)
        $target=Join-Path $root $role.folder
        foreach($file in Get-ChildItem -LiteralPath $publish -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($publish,$file.FullName)
            $output=Join-Path $target $relative
            $key="$($role.folder)/$($relative.Replace('\','/'))"
            $digest=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            if(Test-Path -LiteralPath $output){if((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant() -cne $digest){throw "Conflicting role output; nothing overwritten: $key"}}
            else{New-Item -ItemType Directory -Force (Split-Path $output -Parent) | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $output}
            if(-not $provenance.ContainsKey($key)){$provenance[$key]=[Collections.Generic.List[object]]::new()}
            $provenance[$key].Add(@{Role=$role.name;PublishPath=$relative.Replace('\','/');Sha256=$digest})
        }
    }
    Copy-Item -LiteralPath 'packaging/identity/Assets' -Destination (Join-Path $root 'app') -Recurse
    Copy-Item -LiteralPath 'scripts/run-text-alpha.ps1','scripts/text-alpha-common.ps1','assets/fixtures/selection-public.txt' -Destination $root
    Copy-Item -LiteralPath 'docs/text-alpha.md' -Destination (Join-Path $root 'README.md')
    $files=@(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName | ForEach-Object{@{Path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
    $commit=(& git rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0){throw 'Cannot identify build commit.'}
    $dirty=@(& git status --porcelain=v1);if($LASTEXITCODE -ne 0){throw 'Cannot identify work tree state.'}
    @{SchemaVersion=1;Kind='KuaiYiManagedTextAlpha';Rid=$contract.rid;Runtime=$contract.runtime;SelfContained=$true;SingleFile=$false;Trimmed=$false;Installer=$false;ProductNativeDlls='NotIncluded';WindowsExecution='NotRun';P4='InProgress';ModelCalls='None';Registration='None';SourceCommit=$commit;SourceDirty=($dirty.Count -gt 0);Sdk=((& dotnet --version).Trim());Roles=$contract.roles;Files=$files;RoleProvenance=$provenance} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $root 'text-alpha-manifest.json') -Encoding utf8NoBOM
    $verified=Assert-AlphaBundle $root
    $archive=Join-Path $script:RepositoryRoot "artifacts/KuaiYi-TextAlpha-$($commit.Substring(0,8))-$run-win-x64.zip"
    if(Test-Path -LiteralPath $archive){throw 'Archive already exists; never replace another Alpha or review package.'}
    Compress-Archive -Path (Join-Path $root '*') -DestinationPath $archive
    @{BundleRoot=$root;Archive=$archive;Bytes=(Get-Item -LiteralPath $archive).Length;Sha256=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant();Files=$verified.Files.Count;WindowsExecution='NotRun';Installer=$false;SelfContained=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'stage-result.json') -Encoding utf8NoBOM
    Get-Content -LiteralPath (Join-Path $work 'stage-result.json')
}finally{Pop-Location}
