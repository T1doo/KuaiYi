Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$summary=[ordered]@{Kind='JobMemberFixtureEntry';SourceCommit=$null;Scope='SyntheticOwnedJobsOnly';Status='Fail';Stage='Initialize';Fixture=$null;CMake='NotRun';FourCases='NotRun';ProductionReuse='Off';ModelCalls=0;ExitCode=3}
try {
    if(-not $IsWindows -or $env:CI -cne 'true' -or [IntPtr]::Size -ne 8 -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString() -cne 'X64' -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -cne 'X64'){
        $summary.Status='Blocked';$summary.Stage='HostedWindowsRequired';$summary.ExitCode=2
    } else {
        $summary.Stage='SourceIdentity'
        if($env:GITHUB_SHA -cnotmatch '^[0-9a-f]{40}$'){throw 'Source rejected'}
        $summary.SourceCommit=$env:GITHUB_SHA
        $summary.Stage='CompileFixture'
        Add-Type -Path @((Join-Path $repo 'tools/ci/JobMemberDiagnostic.cs'),(Join-Path $PSScriptRoot 'JobMemberWindowsFixture.cs')) 2>$null 3>$null 4>$null 5>$null 6>$null
        $summary.Stage='RunOwnedFixture'
        $result=[KuaiYiJobMemberWindowsFixture]::RunWindows((Join-Path $PSHOME 'pwsh.exe'))
        $json=$result.PublicJson()
        if($json.Length -gt 4096){throw 'Fixture receipt rejected'}
        $summary.Fixture=$json|ConvertFrom-Json -AsHashtable -Depth 6
        $summary.Status=$summary.Fixture.Status;$summary.Stage=$summary.Fixture.Stage;$summary.ExitCode=$summary.Fixture.ExitCode
    }
} catch { # Fixed stage only; no exception, path, command, PID or native error output.
}
Write-Host ('KUAIYI_JOB_MEMBER_WINDOWS '+($summary|ConvertTo-Json -Depth 8 -Compress))
exit $summary.ExitCode
