param([Parameter(Mandatory)][string]$InstallRoot, [switch]$RunPublicOcrFixture, [string]$Language='en-US')
. "$PSScriptRoot/common.ps1"
Assert-WindowsDesktop
& "$PSScriptRoot/validate-dev-identity.ps1" -InstallRoot $InstallRoot
$root=(Resolve-Path -LiteralPath $InstallRoot).Path
$registered=@(Get-AppxPackage -Name 'KuaiYi.Development' | Where-Object { $_.Name -ceq 'KuaiYi.Development' -and $_.Publisher -ceq 'CN=KuaiYi Development' })
if ($registered.Count -ne 1) { throw 'Exactly one reviewed development identity must be registered by the user beforehand.' }
function Read-Probe([string]$Exe,[string[]]$Arguments,[int]$Seconds=5) {
    $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $root $Exe));$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process=[Diagnostics.Process]::Start($info)
    try {
        $read=$process.StandardOutput.ReadToEndAsync()
        if (-not $process.WaitForExit($Seconds*1000)) { $process.Kill(); if (-not $process.WaitForExit(2000)) { throw 'Owned diagnostic process exit remains unconfirmed.' }; throw 'Diagnostic timeout.' }
        if ($process.ExitCode -ne 0) { throw "Diagnostic failed with exit code $($process.ExitCode); no runtime Pass." }
        return $read.GetAwaiter().GetResult() | ConvertFrom-Json
    } finally { if ($process.HasExited) { $process.Dispose() } }
}
$app=Read-Probe 'KuaiYi.App.exe' @('--diagnose')
$worker=Read-Probe 'KuaiYi.ImageWorker.exe' @('--diagnose')
foreach ($item in @(@{Value=$app;Id='KuaiYi'},@{Value=$worker.identity;Id='ImageWorker'})) {
    if (-not $item.Value.present -or $item.Value.failure -ne 0 -or $item.Value.fullName -cne $registered[0].PackageFullName -or $item.Value.applicationUserModelId -cne ($registered[0].PackageFamilyName+'!'+$item.Id)) { throw "Runtime independent identity failed: $($item.Id)" }
}
$ocr='NotRun';$match=$null
if ($RunPublicOcrFixture) {
    if ($worker.failure -ne 0 -or $Language -notin $worker.languages) { throw 'Language/native OCR capability missing; install Windows language OCR capability explicitly. No silent fallback.' }
    # Copy only the repository-owned synthetic test PNG, never user images; this is explicit diagnostic fixture preparation.
    $fixture=Join-Path $env:TEMP ('KuaiYi-public-ocr-'+[Guid]::NewGuid().ToString('N')+'.png')
    $data=[IO.File]::ReadAllBytes((Join-Path $script:RepositoryRoot 'tests/fixtures/images/kuaiyi-test.png'))
    $file=[IO.File]::Open($fixture,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $file.Write($data) } finally { $file.Dispose() }
    try { $result=Read-Probe 'KuaiYi.App.exe' @('--ocr-probe',$fixture,$Language) 20; $match=$result.matchesPublicFixture; if ($result.failure -ne 0 -or -not $match -or $result.exitState -ne 2) { throw 'Public fixture OCR or exit confirmation failed.' }; $ocr='Pass' }
    finally { Remove-Item -LiteralPath $fixture }
}
[pscustomobject]@{RuntimeMainIdentity='Pass';RuntimeWorkerIdentity='Pass';Languages=$worker.languages;MaxImageDimension=$worker.maxImageDimension;WorkerCapabilityStatus=$worker.failure;PublicFixtureOcr=$ocr;MatchesPublicFixture=$match;ModernExplorerMenu='NotRun';Note='Menu appearance/invoke/focus needs separate actual Explorer observation.'} | ConvertTo-Json -Depth 6
