Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/selection-ci-summary.ps1"
$root=Split-Path $PSScriptRoot -Parent
$summary=[pscustomobject]@{Kind='HostedSelectionSummary';Status='Fail';Reason='RunnerFailure';PhysicalSelection='NotRun';Windows11Acceptance='NotRun';ModelCalls=0;ExitCode=3}
$child=$null; $stdout=$null; $stderr=$null
try {
    if (-not $IsWindows -or $env:CI -cne 'true') { $summary.Status='Blocked';$summary.Reason='HostedWindowsRequired';$summary.ExitCode=2 }
    else {
        Add-Type @'
using System;
using System.IO;
using System.Threading.Tasks;
public static class KuaiYiSelectionDiscard {
    public static async Task<bool> Drain(Stream stream) {
        byte[] bytes = new byte[4096];
        try { while (await stream.ReadAsync(bytes,0,bytes.Length) != 0) Array.Clear(bytes,0,bytes.Length); return true; }
        catch { return false; }
        finally { Array.Clear(bytes,0,bytes.Length); }
    }
}
'@
        $commit=(& git -C $root rev-parse HEAD 2>$null).Trim()
        if ($LASTEXITCODE -ne 0 -or $commit -cnotmatch '^[0-9a-f]{40}$' -or $env:GITHUB_SHA -cne $commit -or @(& git -C $root status --porcelain --untracked-files=no 2>$null).Count -ne 0) { throw 'Invalid source' }
        $output=Join-Path $root ('artifacts/windows-selection/'+[guid]::NewGuid().ToString('N'))
        [void](New-Item -ItemType Directory -Path $output)
        $buildLog=Join-Path $output 'build.local.log'
        foreach ($project in @('src/KuaiYi.CaptureBroker/KuaiYi.CaptureBroker.csproj','tools/KuaiYi.CompatibilityProbe/KuaiYi.CompatibilityProbe.csproj')) {
            & dotnet restore (Join-Path $root $project) --locked-mode *> $buildLog
            if ($LASTEXITCODE -ne 0) { $summary.Reason='LockedRestoreFailed';throw 'Restore failed' }
            & dotnet build (Join-Path $root $project) --no-restore -c Release -m:1 -nr:false -p:UseSharedCompilation=false *> $buildLog
            if ($LASTEXITCODE -ne 0) { $summary.Reason='BuildFailed';throw 'Build failed' }
        }
        function Find-OfficialInstalledBrowser([string]$Relative, [string]$Publisher) {
            foreach ($base in @($env:ProgramFiles,${env:ProgramFiles(x86)})) {
                if ([string]::IsNullOrEmpty($base)) { continue }
                $file=Join-Path $base $Relative
                if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
                $signature=Get-AuthenticodeSignature -FilePath $file
                if ($signature.Status -eq 'Valid' -and $signature.SignerCertificate.Subject -match $Publisher) { return $file }
            }
            return '-'
        }
        $chrome=Find-OfficialInstalledBrowser 'Google/Chrome/Application/chrome.exe' '(^|,\s*)CN=Google LLC(,|$)'
        $edge=Find-OfficialInstalledBrowser 'Microsoft/Edge/Application/msedge.exe' '(^|,\s*)CN=Microsoft Corporation(,|$)'
        $exe=Join-Path $root 'tools/KuaiYi.CompatibilityProbe/bin/Release/net10.0-windows/win-x64/KuaiYi.CompatibilityProbe.exe'
        $broker=Join-Path $root 'src/KuaiYi.CaptureBroker/bin/Release/net10.0-windows/win-x64/KuaiYi.CaptureBroker.exe'
        $receipt=Join-Path $output 'receipt.local.json'
        $info=[Diagnostics.ProcessStartInfo]::new($exe)
        $info.UseShellExecute=$false;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true;$info.WorkingDirectory=$root
        foreach ($key in @($info.Environment.Keys)) { if ($key -notin @('SystemRoot','WINDIR','TEMP','TMP','PATH','DOTNET_ROOT','DOTNET_ROOT_X64','ProgramFiles','ProgramFiles(x86)')) { [void]$info.Environment.Remove($key) } }
        $info.Environment['CI']='true';$info.Environment['GITHUB_SHA']=$commit
        foreach ($argument in @('--ci-installed-selection',$broker,$receipt,$chrome,$edge)) { [void]$info.ArgumentList.Add($argument) }
        $child=[Diagnostics.Process]::Start($info)
        $stdout=[KuaiYiSelectionDiscard]::Drain($child.StandardOutput.BaseStream);$stderr=[KuaiYiSelectionDiscard]::Drain($child.StandardError.BaseStream)
        if (-not $child.WaitForExit(540000)) { $summary.Reason='ProbeDeadline';throw 'Probe deadline' }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),2000) -or -not $stdout.IsCompletedSuccessfully -or -not $stderr.IsCompletedSuccessfully -or -not $stdout.Result -or -not $stderr.Result) { $summary.Reason='OriginalReadsUnsettled';throw 'Read settlement failed' }
        if (-not (Test-Path -LiteralPath $receipt) -or (Get-Item -LiteralPath $receipt).Length -gt 262144) { $summary.Reason='ReceiptUnavailable';throw 'Missing receipt' }
        try { $summary=ConvertTo-SelectionSummary (Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json) $child.ExitCode $commit }
        catch { $summary.Reason='ReceiptRejected' }
    }
} catch { # Only the fixed summary crosses the runner boundary.
} finally {
    if ($null -ne $child) {
        $settled=$false
        try {
            if (-not $child.HasExited) { $child.Kill($true);[void]$child.WaitForExit(5000) }
            if ($null -ne $stdout -and $null -ne $stderr) { [void][Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),2000) }
            $settled=$child.HasExited -and $null -ne $stdout -and $null -ne $stderr -and $stdout.IsCompletedSuccessfully -and $stderr.IsCompletedSuccessfully -and $stdout.Result -and $stderr.Result
            $summary | Add-Member RunnerSettled $settled
        } catch { $settled=$false }
        if ($settled) { $child.Dispose() }
        else { $script:UnsettledSelectionChild=$child;$script:UnsettledSelectionReads=@($stdout,$stderr);$summary.Status='Fail';$summary.ExitCode=3;$summary | Add-Member -Force CleanupReason 'OriginalOwnerSettlementUnconfirmed' }
    }
}
Write-Host ('KUAIYI_SELECTION_SUMMARY '+($summary | ConvertTo-Json -Depth 8 -Compress))
exit $summary.ExitCode
