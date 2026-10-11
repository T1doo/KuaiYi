param(
    [ValidateSet('Sync','Async','AsyncDisabled','StartRejected','GetDataFailure','GetDataBlocked','QueryBlocked','ModeBlocked','Oversized','InvalidUtf16','MissingTerminator','AsyncGetDataBlocked','AsyncEndBlocked','AsyncEndFailure')][string]$Fault = 'Sync',
    [ValidateSet('Copy','CopyMove','MoveOnly')][string]$Offered = 'Copy',
    [string]$ProbeDirectory,
    [string]$EvidenceDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or -not [Environment]::UserInteractive -or [Diagnostics.Process]::GetCurrentProcess().SessionId -eq 0) {
    throw 'Blocked: this experiment requires a Windows interactive desktop. No OLE/focus pass is recorded.'
}
if (-not $ProbeDirectory) {
    $ProbeDirectory = if (Test-Path (Join-Path $PSScriptRoot 'selection-public.txt')) { $PSScriptRoot } else { Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/probes-win-x64' }
}
if (-not $EvidenceDirectory) { $EvidenceDirectory = Join-Path $ProbeDirectory ('evidence-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')) }
$ProbeDirectory = [IO.Path]::GetFullPath($ProbeDirectory)
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
$fixture = Join-Path $ProbeDirectory 'selection-public.txt'
$hostExe = Join-Path $ProbeDirectory 'KuaiYi.OleProbe/KuaiYi.OleProbe.exe'
$sourceExe = Join-Path $ProbeDirectory 'KuaiYi.OleFaultSource/KuaiYi.OleFaultSource.exe'
foreach ($file in $fixture, $hostExe, $sourceExe) { if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing probe file: $file" } }
New-Item -ItemType Directory -Force $EvidenceDirectory | Out-Null
$hostLog = Join-Path $EvidenceDirectory 'target.jsonl'
$sourceLog = Join-Path $EvidenceDirectory 'source.jsonl'
# ArgumentList preserves spaces and literal input without constructing command text.
function Launch([string]$Executable, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    return [Diagnostics.Process]::Start($start)
}
$target = Launch $hostExe @($fixture, $hostLog)
$source = Launch $sourceExe @($fixture, $Fault, $Offered, $sourceLog)
@{ fault = $Fault; offered = $Offered; targetProcessId = $target.Id; sourceProcessId = $source.Id;
   fixtureSha256 = (Get-FileHash $fixture -Algorithm SHA256).Hash.ToLowerInvariant();
   launchedUtc = [DateTime]::UtcNow.ToString('o'); outcome = 'NotEvaluated'; desktopAcceptance = 'NotRun';
   instructions = 'Perform the steps in ole-probe.md, close both probe windows to flush metadata, then review actual focus/effects/counters/timeouts. Launching is not a pass.' } |
    ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'launch.json') -Encoding utf8NoBOM
$target.Dispose(); $source.Dispose()
Write-Output "Started owned public-fixture probes. Evidence on normal close: $EvidenceDirectory. Outcome remains NotEvaluated."
