Set-StrictMode -Version Latest
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
function Get-SourceHeadOutcome($Operation,[string]$Output,[string]$Expected) {
    if($Expected -cnotmatch '^[0-9a-f]{40}$'){return 'InvalidExpectedSource'}
    if($Operation.Kind -cne 'SourceHead' -or $Operation.Status -cne 'Completed' -or $Operation.Stage -cne 'Completed' -or $Operation.FailureKind -cne 'None' -or $Operation.ErrorCode -cne 'None' -or -not $Operation.ProcessCreated -or -not $Operation.Resumed -or -not $Operation.RootExitConfirmed -or -not $Operation.JobEmpty -or -not $Operation.CleanupConfirmed -or $Operation.RescueUsed){return 'InvalidOperationReceipt'}
    if($Operation.Exit -cne '0'){return 'CommandExitNonzero'}
    # Match the whole owned Git output, never expose unexpected text or a mismatching SHA.
    if($null -eq $Output -or $Output.Length -gt 4096 -or $Output -cnotmatch '\A[0-9a-f]{40}(?:\r?\n)?\z'){return 'InvalidGitOutput'}
    if($Output.TrimEnd([char[]]"`r`n") -cne $Expected){return 'SourceMismatch'}
    return 'SourceHeadSucceeded'
}
function Get-SourceHeadPreparationError([Exception]$Failure) {
    # This is for script preparation only; native operation errors use the existing finite projection.
    if($Failure -is [Management.Automation.CommandNotFoundException]){return 'CommandMissing'}
    if($Failure -is [IO.IOException]){return 'Io'}
    if($Failure -is [ArgumentException]){return 'Argument'}
    if($Failure -is [DllNotFoundException]){return 'LibraryMissing'}
    if($Failure -is [EntryPointNotFoundException]){return 'EntryMissing'}
    return 'Other'
}
