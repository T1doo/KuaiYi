Set-StrictMode -Version Latest
function Assert-SelectionInteger($Value, [int]$Minimum, [int]$Maximum) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) { throw 'Invalid integer evidence' }
}
function ConvertTo-SelectionSummary($Report, [int]$ChildExit, [string]$Commit) {
    Assert-SelectionInteger $Report.ModelCalls 0 0; Assert-SelectionInteger $Report.Planned 120 120
    if ($Commit -cnotmatch '^[0-9a-f]{40}$' -or $Report.SourceCommit -cne $Commit -or $Report.Kind -cne 'HostedInstalledSelection' -or $Report.Scope -cne 'ExplicitProductionUiaOnly' -or $Report.Accessibility -cne 'DefaultOnDemand' -or $Report.PhysicalSelection -cne 'NotRun' -or $Report.Windows11Acceptance -cne 'NotRun' -or $Report.ModelCalls -ne 0 -or $Report.Planned -ne 120 -or $ChildExit -notin @(0,2,3)) { throw 'Invalid source or scope' }
    if (@($Report.Views).Count -ne 6) { throw 'Invalid view denominator' }
    $seen = @{}; $views = @(); $statuses = @('Success','EmptyOrUnsupported','Sensitive','Stale','SoftTimeout','HardTimeout','Crash','Cancelled','ProtocolFailure','CleanupPending','Paused','TooLarge','UntrustedGesture','ClipboardUnsupported','ClipboardTimeout','ClipboardUnrelated','ClipboardBusy','NotRun','SourceNotCurrent')
    $kinds = @('Word','Letter','Paragraph','Unicode','Formula','Long') * 3 + @('Paragraphs','Mixed')
    foreach ($view in $Report.Views) {
        Assert-SelectionInteger $view.Planned 20 20; Assert-SelectionInteger $view.Correct 0 20; Assert-SelectionInteger $view.Mismatches 0 20
        if ($view.Application -cnotin @('Chrome','Edge') -or $view.View -cnotin @('Html','ReadOnly','Input') -or $view.Version -cnotmatch '^(Unknown|[0-9]+(\.[0-9]+){1,3})$' -or $view.Planned -ne 20 -or @($view.Cases).Count -ne 20 -or $view.Status -cnotin @('Pass','Fail','Blocked') -or $view.Reason -cnotin @('Completed','UnavailableOrUnverified','PreviousCleanupOrDeadline','SourceNotCurrent','BrokerCleanupUnconfirmed','ProbeDeadline','SequenceUnconfirmed','FixtureOrSourceFailure','ProfileDeletionFailed')) { throw 'Invalid view evidence' }
        $key=$view.Application+'/'+$view.View
        if ($seen.ContainsKey($key)) { throw 'Duplicate view' }; $seen[$key]=$true
        foreach ($name in @('SequenceCompleted','BrokerCleanup','ApplicationCleanup','ProfileDeleted')) { if ($view.$name -isnot [bool]) { throw 'Invalid cleanup evidence' } }
        $correct=0; $mismatches=0; $outcomes=[ordered]@{}
        for ($i=0; $i -lt 20; $i++) {
            $case=$view.Cases[$i]
            Assert-SelectionInteger $case.Index $i $i
            if ($case.Index -ne $i -or $case.Kind -cne $kinds[$i] -or $case.Outcome -cnotin $statuses) { throw 'Invalid case identity' }
            foreach ($name in @('Captured','Matches','ForegroundStable','MarkerStable','BrokerSettled')) { if ($case.$name -isnot [bool]) { throw 'Invalid case flag' } }
            if ($case.Captured -ne ($case.Outcome -ceq 'Success') -or $case.Matches -and (-not $case.Captured -or -not $case.ForegroundStable -or -not $case.MarkerStable -or -not $case.BrokerSettled)) { throw 'Invalid match proof' }
            if ($case.Matches) { $correct++ }
            if ($case.Captured -and $case.ForegroundStable -and $case.MarkerStable -and -not $case.Matches) { $mismatches++ }
            if (-not $outcomes.Contains($case.Outcome)) { $outcomes[$case.Outcome]=0 }; $outcomes[$case.Outcome]++
        }
        if ($view.Correct -ne $correct -or $view.Mismatches -ne $mismatches) { throw 'Invalid case totals' }
        $pass=$view.Reason -ceq 'Completed' -and $view.SequenceCompleted -and $correct -ge 19 -and $mismatches -eq 0 -and $view.BrokerCleanup -and $view.ApplicationCleanup -and $view.ProfileDeleted
        if (($view.Status -ceq 'Pass') -ne $pass -or $view.Status -ceq 'Blocked' -and ($correct -ne 0 -or $outcomes.Count -ne 1 -or -not $outcomes.Contains('NotRun'))) { throw 'Invalid view status' }
        $views += [pscustomobject][ordered]@{Application=$view.Application;Version=$view.Version;View=$view.View;Status=$view.Status;Reason=$view.Reason;SequenceCompleted=$view.SequenceCompleted;Planned=20;Correct=$correct;Mismatches=$mismatches;Outcomes=[pscustomobject]$outcomes;BrokerCleanup=$view.BrokerCleanup;ApplicationCleanup=$view.ApplicationCleanup;ProfileDeleted=$view.ProfileDeleted}
    }
    $status=if (@($views | Where-Object Status -CEQ 'Fail').Count -gt 0) {'Fail'} elseif (@($views | Where-Object Status -CEQ 'Blocked').Count -gt 0) {'Blocked'} else {'Pass'}
    $exit=if ($status -ceq 'Pass') {0} elseif ($status -ceq 'Blocked') {2} else {3}
    if ($ChildExit -ne $exit) { throw 'Invalid child outcome' }
    return [pscustomobject][ordered]@{Kind='HostedSelectionSummary';SourceCommit=$Commit;Status=$status;Scope='ExplicitProductionUiaOnly';Accessibility='DefaultOnDemand';Planned=120;Correct=($views | Measure-Object Correct -Sum).Sum;ModelCalls=0;ClipboardFallback='Disabled';PhysicalSelection='NotRun';Windows11Acceptance='NotRun';Pdf='NotRun';MarkdownEditors='NotRun';Views=$views;ExitCode=$exit}
}
