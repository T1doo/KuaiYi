param([ValidateSet('Debug','Release')][string]$Configuration = 'Release',
      [ValidateSet('Offline','Managed','All')][string]$Scope = 'All', [switch]$InitializeLocks)
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    Assert-SpecIntegrity
    Assert-Sdk
    $projects = if ($Scope -eq 'Offline') {
        @('tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj', 'tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj')
    } else { @('KuaiYi.sln') }
    foreach ($project in $projects) {
        $restore = @('restore', $project)
        if (-not $InitializeLocks) { $restore += '--locked-mode' }
        if (-not $IsWindows) { $restore += '/p:EnableWindowsTargeting=true' }
        Invoke-Checked dotnet $restore
        $build = @('build', $project, '--no-restore', '--configuration', $Configuration)
        if (-not $IsWindows) { $build += '/p:EnableWindowsTargeting=true' }
        Invoke-Checked dotnet $build
    }
    if ($Scope -eq 'All') {
        if (-not $IsWindows) { throw 'Blocked: full native Windows build requires Windows SDK and MSVC; managed cross-build is only partial evidence.' }
        foreach ($component in 'KuaiYi.OcrNative','KuaiYi.ShellExtension','KuaiYi.CaptureNative') {
            Invoke-Checked cmake @('-S',"src/$component",'-B',"artifacts/native/$component",'-A','x64')
            Invoke-Checked cmake @('--build',"artifacts/native/$component",'--config',$Configuration)
        }
        Invoke-Checked ctest @('--test-dir','artifacts/native/KuaiYi.ShellExtension','-C',$Configuration,'--output-on-failure')
    }
    Write-Output "Built scope $Scope with SDK $((& dotnet --version).Trim()). This is not desktop acceptance."
} finally { Pop-Location }
