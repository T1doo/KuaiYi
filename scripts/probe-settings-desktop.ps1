param([Parameter(Mandatory=$true)][string]$Output)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/windows-ci-common.ps1"
Assert-SpecIntegrity
$facts=Get-CiEnvironment
$facts | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Output -Encoding utf8
if(@($facts.DesktopBlockers).Count -ne 0){throw 'Actual interactive desktop prerequisites unavailable; no settings changed'}
