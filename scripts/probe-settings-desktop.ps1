param([Parameter(Mandatory=$true)][string]$Output)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$probeClock=[Diagnostics.Stopwatch]::StartNew()
function Write-DesktopStage([string]$Stage) {
    # Fixed stage labels and monotonic ticks only: no identity or user content.
    [Console]::Error.WriteLine(('KUAIYI_DESKTOP_STAGE {0} {1} {2}' -f $Stage,$probeClock.ElapsedTicks,[Diagnostics.Stopwatch]::Frequency))
    [Console]::Error.Flush()
}
Write-DesktopStage 'ScriptEntered'
. "$PSScriptRoot/windows-ci-common.ps1"
Write-DesktopStage 'BeforeSpec'
Assert-SpecIntegrity
Write-DesktopStage 'SpecVerified'
$facts=Get-CiEnvironment -DesktopOnly -OnDesktopStage { param($Stage) Write-DesktopStage $Stage }
Write-DesktopStage 'BeforeFactsWrite'
$facts | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Output -Encoding utf8
Write-DesktopStage 'FactsWritten'
if(@($facts.DesktopBlockers).Count -ne 0){throw 'Actual interactive desktop prerequisites unavailable; no settings changed'}
Write-DesktopStage 'DesktopAccepted'
