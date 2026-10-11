param([ValidateSet('Debug','Release')][string]$Configuration='Release')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    $folder=Join-Path $script:RepositoryRoot 'artifacts/test-results/gdi-contract'
    New-Item -ItemType Directory -Force $folder | Out-Null
    $source='tests/native/GdiReadContractTests.cpp'
    if($IsWindows){
        if(-not(Get-Command cl -ErrorAction SilentlyContinue)){throw 'NotRun: shared C++ contract requires MSVC developer shell (or g++ on Linux); real Windows GDI/WGC is always separate.'}
        $binary=Join-Path $folder 'gdi-contract.exe'
        Invoke-Checked cl @('/nologo','/std:c++20','/W4','/WX','/EHsc',$source,"/Fe:$binary","/Fo:$(Join-Path $folder 'gdi-contract.obj')")
    }else{
        if(-not(Get-Command g++ -ErrorAction SilentlyContinue)){throw 'NotRun: g++ required to execute the shared portable GDI ordering contract.'}
        $binary=Join-Path $folder 'gdi-contract'
        Invoke-Checked g++ @('-std=c++20','-Wall','-Wextra','-Werror','-pthread',$source,'-o',$binary)
    }
    Invoke-Checked $binary @()
    Write-Output 'Portable ordering contract only; all real Windows DIB/GdiFlush/BitBlt/capture tests remain NotRun.'
} finally {Pop-Location}
