Set-StrictMode -Version Latest
. "$PSScriptRoot/../../scripts/sdk-compile-evidence.ps1"
function Get-LifecycleOperationEvidence([string]$Kind,$Receipt) {
    $kinds=@('SourceHead','SourceParent','SourceAncestry','SourceLockedTree','SourceStatus','SourceCompare','CMakeVersion','DotnetVersion','VisualStudioInventory','NativeConfigure','NativeBuild','ManagedRestore','ManagedBuild','LifecycleRuntime')
    if($Kind -cnotin $kinds -or $Receipt -isnot [KuaiYiLifecycleOperation+Receipt]){throw 'Invalid operation receipt'}
    if($Receipt.Status -cnotin @('Failure','Timeout','Completed','CleanupFailure') -or $Receipt.Exit -cnotin @('Unknown','Other','0','1','2','3')){throw 'Invalid operation receipt'}
    foreach($value in @($Receipt.Stage,$Receipt.Failure,$Receipt.Error)){if(-not [Enum]::IsDefined($value.GetType(),$value)){throw 'Invalid operation receipt'}}
    return [pscustomobject][ordered]@{Kind=$Kind;Status=$Receipt.Status;Exit=$Receipt.Exit;Stage=$Receipt.Stage.ToString();FailureKind=$Receipt.Failure.ToString();ErrorCode=$Receipt.Error.ToString();ProcessCreated=$Receipt.ProcessCreated;Resumed=$Receipt.Resumed;RootExitConfirmed=$Receipt.RootExitConfirmed;JobEmpty=$Receipt.JobEmpty;RescueUsed=$Receipt.RescueUsed;CleanupConfirmed=$Receipt.CleanupConfirmed}
}
function Get-LifecycleBuildEvidence([string]$Output) {
    if($null -eq $Output -or $Output.Length -gt 4194304){return}
    $rows=[Collections.Generic.List[object]]::new()
    # Keep the existing finite native-code whitelist. Repository locations outside it stay Unknown.
    foreach($row in @(Get-SdkCompilerEvidence $Output -Profile NativeReuse)){if($rows.Count -lt 16){$rows.Add($row)}}
    $files=@{'program.cs'='ManagedEntry';'lifecycleparent.cs'='ManagedParent';'lifecyclesupervisor.cs'='ManagedSupervisor';'lifecycleprotocol.cs'='ManagedProtocol';'processtracking.cs'='Tracking';'watchdoggate.cs'='WatchdogGate';'exclusivekilljob.cs'='JobOwner'}
    $codes=@('CS0103','CS0117','CS0246','CS0618','CS1503','CS4014','CS8600','CS8602','CS8604','CS9124')
    $seen=@{}
    foreach($line in ($Output -split '\r?\n')){
        if($line.Length -gt 8192){continue}
        $match=[regex]::Match($line,'^(?<Location>[^\r\n]*?):\s*(?:error|warning)\s+(?<Code>CS[0-9]{4})\s*:')
        if(-not $match.Success){continue}
        $code=$match.Groups['Code'].Value;if($code -cnotin $codes){$code='Unknown'}
        $source='Unknown';$number=$null
        $where=[regex]::Match($match.Groups['Location'].Value,'^\s*(?<Path>[^"\r\n]+)\((?<Line>[0-9]{1,6})(?:,\s*[0-9]{1,6})?\)\s*$')
        if($where.Success){$candidate=[int]$where.Groups['Line'].Value;$name=($where.Groups['Path'].Value -split '[\\/]')[-1].ToLowerInvariant();if($candidate -ge 1 -and $candidate -le 100000 -and $files.ContainsKey($name)){$source=$files[$name];$number=$candidate}}
        $key=$code+'/'+$source+'/'+$number;if($seen.ContainsKey($key)){continue};$seen[$key]=$true
        if($rows.Count -lt 16){$rows.Add([pscustomobject][ordered]@{Code=$code;Source=$source;Line=$number})}
    }
    return $rows.ToArray()
}
function Read-LifecyclePublicCases([string]$Output) {
    if($null -eq $Output -or $Output.Length -gt 4194304){throw 'Invalid receipt'}
    $result=[Collections.Generic.List[object]]::new();$unsettled=$false
    $names=@('Case','Status','Stage','ParentExitConfirmed','WorkerExitConfirmed','ReleaseCallsReturned','EofObserved','RescueUsed','IoSettled')
    $stages=@('ProtocolFailure','BeforeBindParentTerminationRequested','BoundParentTerminationRequested','FinalCallsReturned','WatchdogTerminationIssuedAndSettled','WatchdogProofMissing','WatchdogNaturalExit','WatchdogKillNotIssued','Unsettled')
    foreach($line in ($Output -split '\r?\n')){
        if($line -ceq 'KUAIYI_LIFECYCLE_UNSETTLED'){$unsettled=$true;continue}
        if(-not $line.StartsWith('KUAIYI_LIFECYCLE_CASE ')){continue}
        if($line.Length -gt 8192 -or $result.Count -ge 4){throw 'Invalid receipt'}
        $json=$line.Substring('KUAIYI_LIFECYCLE_CASE '.Length)
        $document=[Text.Json.JsonDocument]::Parse($json)
        try{$properties=@($document.RootElement.EnumerateObject());$unique=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);foreach($property in $properties){if(-not $unique.Add($property.Name)){throw 'Invalid receipt'}};if($properties.Count -ne $names.Count){throw 'Invalid receipt'}}finally{$document.Dispose()}
        $row=$json | ConvertFrom-Json -AsHashtable
        foreach($name in $names){if(-not $row.ContainsKey($name)){throw 'Invalid receipt'}}
        if($row.Case -isnot [long] -and $row.Case -isnot [int]){throw 'Invalid receipt'}
        if($row.Case -ne $result.Count+1 -or $row.Status -cnotin @('Pass','Fail') -or $row.Stage -cnotin $stages){throw 'Invalid receipt'}
        foreach($name in @('ParentExitConfirmed','WorkerExitConfirmed','ReleaseCallsReturned','EofObserved','RescueUsed','IoSettled')){if($row[$name] -isnot [bool]){throw 'Invalid receipt'}}
        # Copy only the exact public schema, never arbitrary original strings/properties.
        $safe=[ordered]@{};foreach($name in $names){$safe[$name]=$row[$name]};$result.Add([pscustomobject]$safe)
    }
    return [pscustomobject]@{Cases=$result.ToArray();Unsettled=$unsettled}
}
function Test-LifecycleCasePass($Row) {
    if($Row.Status -cne 'Pass' -or -not $Row.ParentExitConfirmed -or -not $Row.WorkerExitConfirmed -or -not $Row.IoSettled -or $Row.RescueUsed){return $false}
    switch($Row.Case){
        1{return $Row.Stage -ceq 'FinalCallsReturned' -and $Row.ReleaseCallsReturned -and $Row.EofObserved}
        2{return $Row.Stage -ceq 'BoundParentTerminationRequested' -and -not $Row.ReleaseCallsReturned}
        3{return $Row.Stage -ceq 'BeforeBindParentTerminationRequested' -and -not $Row.ReleaseCallsReturned}
        4{return $Row.Stage -ceq 'WatchdogTerminationIssuedAndSettled' -and -not $Row.ReleaseCallsReturned -and $Row.EofObserved}
    }
    return $false
}
