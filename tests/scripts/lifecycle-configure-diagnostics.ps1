Set-StrictMode -Version Latest
function New-LifecycleConfigureDiagnostic([string]$Status) {
    return [pscustomobject][ordered]@{DiagnosticStatus=$Status;Files=@()}
}
function Assert-ConfigureJson($Element,[int]$Depth=0) {
    if($Depth -gt 6){throw 'Invalid diagnostic'}
    if($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object){
        $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($property in $Element.EnumerateObject()){
            if(-not $names.Add($property.Name)){throw 'Invalid diagnostic'}
            Assert-ConfigureJson $property.Value ($Depth+1)
        }
    } elseif($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array){
        foreach($item in $Element.EnumerateArray()){Assert-ConfigureJson $item ($Depth+1)}
    }
}
function Assert-ConfigureFields($Value,[string[]]$Names) {
    if($Value -isnot [Collections.IDictionary] -or $Value.Count -ne $Names.Count){throw 'Invalid diagnostic'}
    foreach($name in $Names){if($name -cnotin @($Value.Keys)){throw 'Invalid diagnostic'}}
}
function Read-LifecycleConfigureDiagnostic([string]$Output) {
    if($null -eq $Output -or $Output.Length -gt 8192){throw 'Invalid diagnostic'}
    $document=[Text.Json.JsonDocument]::Parse($Output)
    try {Assert-ConfigureJson $document.RootElement} finally {$document.Dispose()}
    $value=ConvertFrom-Json -InputObject $Output -AsHashtable -Depth 8
    Assert-ConfigureFields $value @('DiagnosticStatus','Files')
    if($value.DiagnosticStatus -isnot [string] -or $value.DiagnosticStatus -cnotin @('Collected','SkippedCleanupUnconfirmed','OwnershipUnconfirmed','ReaderUnavailable','SafetyUnknown','ProjectionError') -or $value.Files -isnot [array]){throw 'Invalid diagnostic'}
    if($value.DiagnosticStatus -cne 'Collected'){
        if($value.Files.Count -ne 0){throw 'Invalid diagnostic'}
        return (New-LifecycleConfigureDiagnostic $value.DiagnosticStatus)
    }
    if($value.Files.Count -ne 4){throw 'Invalid diagnostic'}
    $kinds=@('CMakeVersion','NativeConfigure','CMakeCache','ConfigureEvents');$rows=[Collections.Generic.List[object]]::new()
    $booleans=@('ConfigureDone','GenerateDone','CompilerIdSeen','VisualStudio2022','TryCompileEvent')
    foreach($i in 0..3){
        $row=$value.Files[$i];Assert-ConfigureFields $row @('Kind','ReadStatus','Truncated','Facts')
        if($row.Kind -isnot [string] -or $row.Kind -cne $kinds[$i] -or $row.ReadStatus -isnot [string] -or $row.ReadStatus -cnotin @('Projected','Unknown','Truncated','InvalidEncoding','Missing','Rejected') -or $row.Truncated -isnot [bool] -or $row.Truncated -ne ($row.ReadStatus -ceq 'Truncated')){throw 'Invalid diagnostic'}
        $facts=$row.Facts;Assert-ConfigureFields $facts (@('CMakeVersion','AbiResult','WorkingResult')+$booleans)
        if($null -ne $facts.CMakeVersion -and ($facts.CMakeVersion -isnot [string] -or $facts.CMakeVersion -cnotmatch '\A(?:0|[1-9][0-9]{0,3})\.(?:0|[1-9][0-9]{0,3})\.(?:0|[1-9][0-9]{0,3})\z')){throw 'Invalid diagnostic'}
        if($facts.AbiResult -isnot [string] -or $facts.AbiResult -cnotin @('Unknown','Done','Failed') -or $facts.WorkingResult -isnot [string] -or $facts.WorkingResult -cnotin @('Unknown','Works','Broken','Skipped')){throw 'Invalid diagnostic'}
        foreach($key in $booleans){if($facts[$key] -isnot [bool]){throw 'Invalid diagnostic'}}
        # Clear/nonmatching statuses cannot smuggle facts from another file kind.
        $allowed=switch($row.Kind){CMakeVersion {@('CMakeVersion')};NativeConfigure {@('ConfigureDone','GenerateDone','CompilerIdSeen','AbiResult','WorkingResult')};CMakeCache {@('VisualStudio2022')};ConfigureEvents {@('TryCompileEvent')}}
        $safe=[ordered]@{CMakeVersion=$null;ConfigureDone=$false;GenerateDone=$false;CompilerIdSeen=$false;AbiResult='Unknown';WorkingResult='Unknown';VisualStudio2022=$false;TryCompileEvent=$false}
        $known=$false
        foreach($key in @($safe.Keys)){
            if($facts[$key] -cne $safe[$key]){
                if($row.ReadStatus -cne 'Projected' -or $key -cnotin $allowed){throw 'Invalid diagnostic'}
                $safe[$key]=$facts[$key];$known=$true
            }
        }
        if($row.ReadStatus -ceq 'Projected' -and -not $known){throw 'Invalid diagnostic'}
        $rows.Add([pscustomobject][ordered]@{Kind=$kinds[$i];ReadStatus=$row.ReadStatus;Truncated=$row.Truncated;Facts=[pscustomobject]$safe})
    }
    return [pscustomobject][ordered]@{DiagnosticStatus='Collected';Files=$rows.ToArray()}
}
function Complete-LifecycleConfigureDiagnostic($Receipt,$Lease,[scriptblock]$Collector) {
    $result=New-LifecycleConfigureDiagnostic 'ProjectionError'
    try {
        if($Receipt.CleanupConfirmed -isnot [bool] -or $Receipt.CleanupConfirmed -ne $true){$result=New-LifecycleConfigureDiagnostic 'SkippedCleanupUnconfirmed'}
        elseif($null -eq $Lease){$result=New-LifecycleConfigureDiagnostic 'OwnershipUnconfirmed'}
        else {$result=Read-LifecycleConfigureDiagnostic (& $Collector $Lease)}
    } catch {$result=New-LifecycleConfigureDiagnostic 'ProjectionError'}
    finally {
        if($null -ne $Lease){try {[void]$Lease.Dispose()} catch {$result=New-LifecycleConfigureDiagnostic 'LeaseCloseFailed'}}
    }
    return $result
}
