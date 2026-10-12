param([Parameter(Mandatory)][string]$BundleRoot,
      [ValidateSet('Preflight','TextAlpha','SelectionProbe','Overlay')][string]$Mode='Preflight',
      [switch]$Execute,[ValidateRange(10,900)][int]$DurationSeconds=120)
# P0—P4 only. The runnable managed Alpha has a separate explicit opt-in from validation.
& "$PSScriptRoot/run-text-alpha.ps1" -BundleRoot $BundleRoot -Mode $Mode -Execute:$Execute -DurationSeconds $DurationSeconds
