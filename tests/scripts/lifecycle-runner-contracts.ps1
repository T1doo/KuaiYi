Set-StrictMode -Version Latest
function Select-LifecycleApplicationPath([object[]]$Candidates) {
    if($Candidates.Count -eq 0 -or $null -eq $Candidates[0] -or $Candidates[0].CommandType -ne [Management.Automation.CommandTypes]::Application -or $Candidates[0].Source -isnot [string] -or [string]::IsNullOrWhiteSpace($Candidates[0].Source)){throw 'Missing application'}
    return $Candidates[0].Source
}
function Get-LifecycleApplicationPath([ValidateSet('git','cmake','dotnet')][string]$Name) {
    return (Select-LifecycleApplicationPath @(Get-Command $Name -CommandType Application -ErrorAction Stop))
}
function Read-LifecycleExactSha([string]$Output) {
    if($null -eq $Output -or $Output.Length -gt 128 -or $Output -cnotmatch '\A[0-9a-f]{40}(?:\r?\n)?\z'){throw 'Invalid source identity'}
    return $Output.TrimEnd([char[]]"`r`n")
}
function Test-LifecycleSingleParent([string]$Output,[string]$Head,[string]$Parent) {
    if($Head -cnotmatch '^[0-9a-f]{40}$' -or $Parent -cnotmatch '^[0-9a-f]{40}$' -or $Output.Length -gt 128){return $false}
    return $Output -cmatch ('\A'+$Head+' '+$Parent+'(?:\r?\n)?\z')
}
function Get-LifecycleRuntimePaths {
    return @('tools/KuaiYi.ReuseLifecycle','tools/KuaiYi.ReuseLifecycleNative','tools/KuaiYi.SelectionReuseDiagnostic/ExclusiveKillJob.cs','tools/KuaiYi.SelectionReuseNative/RequestLifetime.h','src/KuaiYi.Windows/app.manifest','global.json','Directory.Build.props','Directory.Build.targets')
}
