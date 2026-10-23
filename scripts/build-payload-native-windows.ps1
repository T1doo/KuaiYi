param([Parameter(Mandatory=$true)][string]$Output)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/windows-ci-common.ps1"
Assert-SpecIntegrity
$root=$script:RepositoryRoot
$facts=Get-CiEnvironment
$facts | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $Output 'native-facts.json') -Encoding utf8
if(@($facts.NativeBlockers).Count -ne 0){throw 'Preinstalled native prerequisites unavailable; no install attempted'}
$rows=[Collections.Generic.List[object]]::new()
function Run-Native {
    param([string]$Name,[string]$Command,[string[]]$Arguments)
    $log=Join-Path $Output ($Name+'.log')
    & $Command @Arguments *> $log
    $code=$LASTEXITCODE
    $rows.Add([ordered]@{Name=$Name;Argv=@($Command)+$Arguments;Exit=$code;Log=$Name+'.log'})
    $rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Output 'native-commands.json') -Encoding utf8
    if($code -ne 0){throw "Native command failed: $Name"}
}
$items=@(
    @{Name='CaptureNative';Targets=@('KuaiYi.CaptureNative','GdiReadContractTests');Test='GdiReadOrderingContract'},
    @{Name='OcrNative';Targets=@('KuaiYi.OcrNative');Test=$null},
    @{Name='OleNativeBridge';Targets=@('KuaiYi.OleNativeBridge');Test=$null},
    @{Name='ShellExtension';Targets=@('KuaiYi.ShellExtension','KuaiYi.CommandPolicyTests');Test='CommandPolicy'}
)
$manifest=[Collections.Generic.List[object]]::new()
foreach($item in $items){
    $build=Join-Path $Output ('build/'+$item.Name)
    Run-Native ($item.Name+'-configure') 'cmake' @('-S',(Join-Path $root ('src/KuaiYi.'+$item.Name)),'-B',$build,'-G','Visual Studio 17 2022','-A','x64','-T',"v143,version=$($facts.Toolset)","-DCMAKE_GENERATOR_INSTANCE=$($facts.VisualStudio.Path)","-DCMAKE_SYSTEM_VERSION=$($facts.WindowsSdk)")
    Run-Native ($item.Name+'-build') 'cmake' (@('--build',$build,'--config','Release','--target')+$item.Targets+@('--parallel','2'))
    $dll=Join-Path $build ('Release/KuaiYi.'+$item.Name+'.dll')
    Assert-CiNativePe $dll
    $destination=Join-Path $Output ('KuaiYi.'+$item.Name+'.dll')
    [IO.File]::Copy($dll,$destination,$false)
    $manifest.Add([ordered]@{Path=[IO.Path]::GetFileName($destination);Bytes=(Get-Item -LiteralPath $destination).Length;Sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant();SourceHead=$env:GITHUB_SHA;Toolset=$facts.Toolset;WindowsSdk=$facts.WindowsSdk})
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Output 'native-products.json') -Encoding utf8
    if($item.Test){
        $xml=Join-Path $Output ($item.Name+'-ctest.xml')
        Run-Native ($item.Name+'-ctest') 'ctest' @('--test-dir',$build,'-C','Release','--no-tests=error','-R',('^'+$item.Test+'$'),'--output-on-failure','--output-junit',$xml)
        [void](Assert-CiCtestResults $xml $item.Test)
    }
}
