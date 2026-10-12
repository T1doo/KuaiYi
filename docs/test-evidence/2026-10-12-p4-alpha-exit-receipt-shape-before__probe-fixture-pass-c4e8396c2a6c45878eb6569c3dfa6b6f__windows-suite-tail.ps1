param($BundleRoot,$Mode,[switch]$Execute,$DurationSeconds)
$WindowsMode=$Mode;$ExecuteWindows=$Execute;$DesktopDurationSeconds=$DurationSeconds
& "$PSScriptRoot/run-desktop-tests.ps1" -BundleRoot $BundleRoot -Mode $WindowsMode -Execute -DurationSeconds $DesktopDurationSeconds
exit $LASTEXITCODE
