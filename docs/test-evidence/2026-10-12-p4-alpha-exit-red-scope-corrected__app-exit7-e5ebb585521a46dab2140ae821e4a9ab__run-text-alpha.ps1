param($BundleRoot,$Mode,[switch]$Execute,$DurationSeconds)
. '/workspace/KuaiYi/artifacts/test-results/p4-alpha-exit/baseline-source/text-alpha-common.ps1'
$Mode='TextAlpha';$DurationSeconds=10;$timedOut=$false
$manifest=[pscustomobject]@{SourceCommit='TEST_COMPLETION_ONLY';SourceDirty=$false}
$evidence=$PSScriptRoot;$provider=$null;$receipt='{"WrongSourceRequests":0,"Status":"Pass"}' | ConvertFrom-Json
$info=[Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'pwsh'))
$info.UseShellExecute=$false;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
foreach($arg in @('-NoProfile','-Command','exit 7')){[void]$info.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($info);$output=$process.StandardOutput.ReadToEndAsync();$errorOutput=$process.StandardError.ReadToEndAsync()
if(-not $process.WaitForExit(10000)){$process.Kill($true);throw 'Test-only child did not settle.'}
$childExitCode=$process.ExitCode;$child=[pscustomobject]@{Process=$process;Output=$output;Error=$errorOutput}
function Stop-AlphaChild($Owned) {
    if($null -eq $Owned){return}
    try {
        if(-not $Owned.Process.HasExited){[void]$Owned.Process.CloseMainWindow();if(-not $Owned.Process.WaitForExit(3000)){$Owned.Process.Kill($true);if(-not $Owned.Process.WaitForExit(5000)){throw 'Owned child exit unconfirmed.'}}}
    }finally{$Owned.Process.Dispose()}
}
. {
    try{Stop-AlphaChild $child}finally{Stop-AlphaChild $provider}
    @{Kind='BoundedWindowsTextAlphaSession';Mode=$Mode;SourceCommit=$manifest.SourceCommit;SourceDirty=$manifest.SourceDirty;OS=[Runtime.InteropServices.RuntimeInformation]::OSDescription;DurationSeconds=$DurationSeconds;ChildExitCode=$childExitCode;BoundedTimeout=$timedOut;Receipt=$receipt;WholeAcceptance='NotRun';WindowsCompatibilityMatrix='NotRun';NativeProductDlls='NotIncluded';Installer=$false;RealModelCalls='None';Registration='None';Credentials='None';ManualPending=@('Off/On privacy and focus','production take-words/source association','actual rendered growth and collapse/reopen','both protocol settings','mixed-DPI boundaries','full core compatibility matrix')} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'session.json') -Encoding utf8NoBOM
    Write-Output "Evidence directory: $evidence; whole acceptance remains NotRun."
}

