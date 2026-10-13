Set-StrictMode -Version Latest
function Assert-SelectionInteger($Value, [int]$Minimum, [int]$Maximum) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) { throw 'Invalid integer evidence' }
}
function ConvertTo-SelectionSummary($Report, [int]$ChildExit, [string]$Commit, [switch]$DiagnosticOnly) {
    $planned=if ($DiagnosticOnly) {6} else {120};$perView=if ($DiagnosticOnly) {1} else {20}
    $kind=if ($DiagnosticOnly) {'HostedInstalledSelectionDiagnostic'} else {'HostedInstalledSelection'}
    $scope=if ($DiagnosticOnly) {'DiagnosticOnly'} else {'ExplicitProductionUiaOnly'}
    $viewStates=if ($DiagnosticOnly) {@('Observed','Fail','Blocked')} else {@('Pass','Fail','Blocked')}
    Assert-SelectionInteger $Report.ModelCalls 0 0; Assert-SelectionInteger $Report.Planned $planned $planned
    if ($Commit -cnotmatch '^[0-9a-f]{40}$' -or $Report.SourceCommit -cne $Commit -or $Report.Kind -cne $kind -or $Report.Scope -cne $scope -or $Report.Accessibility -cne 'DefaultOnDemand' -or $Report.PhysicalSelection -cne 'NotRun' -or $Report.Windows11Acceptance -cne 'NotRun' -or $Report.ModelCalls -ne 0 -or $ChildExit -notin @(0,2,3)) { throw 'Invalid source or scope' }
    if (@($Report.Views).Count -ne 6) { throw 'Invalid view denominator' }
    $seen = @{}; $views = @(); $statuses = @('Success','EmptyOrUnsupported','Sensitive','Stale','SoftTimeout','HardTimeout','Crash','Cancelled','ProtocolFailure','CleanupPending','Paused','TooLarge','UntrustedGesture','ClipboardUnsupported','ClipboardTimeout','ClipboardUnrelated','ClipboardBusy','NotRun','SourceNotCurrent')
    $kinds = @('Word','Letter','Paragraph','Unicode','Formula','Long') * 3 + @('Paragraphs','Mixed')
    foreach ($view in $Report.Views) {
        Assert-SelectionInteger $view.Planned $perView $perView; Assert-SelectionInteger $view.Correct 0 $perView; Assert-SelectionInteger $view.Mismatches 0 $perView
        if ($view.Application -cnotin @('Chrome','Edge') -or $view.View -cnotin @('Html','ReadOnly','Input') -or $view.Version -cnotmatch '^(Unknown|[0-9]+(\.[0-9]+){1,3})$' -or @($view.Cases).Count -ne $perView -or $view.Status -cnotin $viewStates -or $view.Reason -cnotin @('Completed','UnavailableOrUnverified','PreviousCleanupOrDeadline','SourceNotCurrent','BrokerCleanupUnconfirmed','ProbeDeadline','SequenceUnconfirmed','FixtureOrSourceFailure','ProfileDeletionFailed')) { throw 'Invalid view evidence' }
        $key=$view.Application+'/'+$view.View
        if ($seen.ContainsKey($key)) { throw 'Duplicate view' }; $seen[$key]=$true
        foreach ($name in @('SequenceCompleted','BrokerCleanup','ApplicationCleanup','ProfileDeleted')) { if ($view.$name -isnot [bool]) { throw 'Invalid cleanup evidence' } }
        $correct=0; $mismatches=0; $outcomes=[ordered]@{};$observations=@();$exitCodes=@();$boundaries=@();$markerSetups=@()
        for ($i=0; $i -lt $perView; $i++) {
            $case=$view.Cases[$i]
            Assert-SelectionInteger $case.Index $i $i
            if ($case.Index -ne $i -or $case.Kind -cne $kinds[$i] -or $case.Outcome -cnotin $statuses) { throw 'Invalid case identity' }
            foreach ($name in @('Captured','Matches','ForegroundStable','MarkerStable','BrokerSettled')) { if ($case.$name -isnot [bool]) { throw 'Invalid case flag' } }
            if ($case.Captured -ne ($case.Outcome -ceq 'Success') -or $case.Matches -and (-not $case.Captured -or -not $case.ForegroundStable -or -not $case.MarkerStable -or -not $case.BrokerSettled)) { throw 'Invalid match proof' }
            if ($case.Matches) { $correct++ }
            if ($case.Captured -and $case.ForegroundStable -and $case.MarkerStable -and -not $case.Matches) { $mismatches++ }
            if (-not $outcomes.Contains($case.Outcome)) { $outcomes[$case.Outcome]=0 }; $outcomes[$case.Outcome]++
            $observation=[pscustomobject]@{Status='Unknown';Stage='Unknown';Exception='Unknown';HResult=$null}
            if ($null -ne $case.PSObject.Properties['BrokerObservation'] -and $null -ne $case.BrokerObservation) {
                $o=$case.BrokerObservation
                if ($o.Status -cnotin @('Unknown','Success','Stale','Sensitive','SensitiveOrElevated','UnsupportedOrProtected','SoftTimeout','TooLarge','TooLargeOrInvalidUnicode','EmptyOrUnsupported','BrokerFailure') -or $o.Stage -cnotin @('Unknown','SourceIdentity','SensitiveCheck','Root','Focus','Hit','Password','FocusAncestors','HitAncestors','Descendants','CandidatePassword','TextPattern','ControlBounds','SelectionRanges','RangeBounds','RangeText','FinalIdentity','RuntimeId','CopyEvidence','Completed','MetadataSerialize') -or $o.Exception -cnotin @('Unknown','None','Com','InvalidOperation','Win32','AccessDenied','ElementUnavailable','Argument','NullReference','NotSupported','TypeLoad','FileLoad') -or ($null -ne $o.HResult -and $o.HResult -isnot [int] -and $o.HResult -isnot [long])) { throw 'Invalid broker observation' }
                if ($null -ne $o.HResult -and ($o.HResult -lt [int]::MinValue -or $o.HResult -gt [int]::MaxValue)) { throw 'Invalid HRESULT' }
                if ($case.Outcome -cin @('HardTimeout','Cancelled','ProtocolFailure') -or ($case.Outcome -ceq 'Crash' -and $o.Status -cne 'BrokerFailure') -or ($o.Status -ceq 'BrokerFailure' -and $case.Outcome -cne 'Crash')) { throw 'Late broker observation' }
                $observation=[pscustomobject]@{Status=$o.Status;Stage=$o.Stage;Exception=$o.Exception;HResult=$o.HResult}
            }
            $exitCode=$null
            if ($null -ne $case.PSObject.Properties['BrokerExitCode'] -and $null -ne $case.BrokerExitCode) {
                Assert-SelectionInteger $case.BrokerExitCode ([int]::MinValue) ([int]::MaxValue)
                if ($case.BrokerExitCode -eq 0 -or $case.Outcome -cne 'Crash' -or -not $case.BrokerSettled -or ($null -ne $case.PSObject.Properties['BrokerObservation'] -and $null -ne $case.BrokerObservation)) { throw 'Invalid original process exit' }
                $exitCode=$case.BrokerExitCode
            }
            $setup='Disabled';$reached=$null
            if ($null -ne $case.PSObject.Properties['BoundaryMarkerSetup']) {
                $setup=$case.BoundaryMarkerSetup
                if ($setup -cnotin @('Disabled','NotStarted','Unavailable','Ready') -or (-not $DiagnosticOnly -and $setup -cne 'Disabled')) { throw 'Invalid boundary setup' }
            }
            if ($null -ne $case.PSObject.Properties['LastReachedBoundary'] -and $null -ne $case.LastReachedBoundary) {
                $b=$case.LastReachedBoundary
                if (-not $DiagnosticOnly -or $setup -cne 'Ready' -or $null -eq $exitCode -or $case.Outcome -cne 'Crash' -or -not $case.BrokerSettled -or $b.Boundary -cnotin @('SourceIntegritySid','SourceIntegrityCount','SourceIntegrityValue','OwnIntegritySid','OwnIntegrityCount','OwnIntegrityValue','GetSelection','GetBoundingRectangles','GetText','CaptureReturned','MetadataPrepared','FramesWritten','MainReturning') -or $b.Position -cnotin @('Enter','Returned')) { throw 'Invalid reached boundary' }
                Assert-SelectionInteger $b.Sequence 1 16384
                $reached=[pscustomobject]@{Boundary=$b.Boundary;Position=$b.Position;Sequence=$b.Sequence}
            }
            $boundaries+=,$reached;$markerSetups+=,$setup
            $exitCodes+=,$exitCode
            $observations+=$observation
        }
        if ($view.Correct -ne $correct -or $view.Mismatches -ne $mismatches) { throw 'Invalid case totals' }
        $pass=$view.Reason -ceq 'Completed' -and $view.SequenceCompleted -and $correct -ge 19 -and $mismatches -eq 0 -and $view.BrokerCleanup -and $view.ApplicationCleanup -and $view.ProfileDeleted
        $observed=$view.Reason -ceq 'Completed' -and $view.SequenceCompleted -and $view.BrokerCleanup -and $view.ApplicationCleanup -and $view.ProfileDeleted -and @($view.Cases | Where-Object {-not $_.BrokerSettled}).Count -eq 0
        if ((-not $DiagnosticOnly -and (($view.Status -ceq 'Pass') -ne $pass)) -or ($DiagnosticOnly -and (($view.Status -ceq 'Observed') -ne $observed)) -or $view.Status -ceq 'Blocked' -and ($correct -ne 0 -or $outcomes.Count -ne 1 -or -not $outcomes.Contains('NotRun'))) { throw 'Invalid view status' }
        $views += [pscustomobject][ordered]@{Application=$view.Application;Version=$view.Version;View=$view.View;Status=$view.Status;Reason=$view.Reason;SequenceCompleted=$view.SequenceCompleted;Planned=$perView;Correct=$correct;Mismatches=$mismatches;Outcomes=[pscustomobject]$outcomes;BrokerObservations=$observations;BrokerExitCodes=$exitCodes;LastReachedBoundaries=$boundaries;BoundaryMarkerSetups=$markerSetups;BrokerCleanup=$view.BrokerCleanup;ApplicationCleanup=$view.ApplicationCleanup;ProfileDeleted=$view.ProfileDeleted}
    }
    $status=if (@($views | Where-Object Status -CEQ 'Fail').Count -gt 0) {'Fail'} elseif (@($views | Where-Object Status -CEQ 'Blocked').Count -gt 0) {'Blocked'} elseif ($DiagnosticOnly) {'Observed'} else {'Pass'}
    $exit=if ($status -cin @('Pass','Observed')) {0} elseif ($status -ceq 'Blocked') {2} else {3}
    if ($ChildExit -ne $exit) { throw 'Invalid child outcome' }
    $summaryKind=if ($DiagnosticOnly) {'HostedSelectionDiagnosticSummary'} else {'HostedSelectionSummary'}
    return [pscustomobject][ordered]@{Kind=$summaryKind;SourceCommit=$Commit;Status=$status;Scope=$scope;Accessibility='DefaultOnDemand';Planned=$planned;Correct=($views | Measure-Object Correct -Sum).Sum;ModelCalls=0;ClipboardFallback='Disabled';PhysicalSelection='NotRun';Windows11Acceptance='NotRun';Pdf='NotRun';MarkdownEditors='NotRun';Views=$views;ExitCode=$exit}
}
