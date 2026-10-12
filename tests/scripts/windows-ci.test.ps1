# Synthetic script-contract tests on the host OS; never Windows desktop acceptance.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../../scripts/windows-ci-common.ps1"
$root = Join-Path ([IO.Path]::GetTempPath()) ('kuaiyi-ci-tests-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$passed = 0
function Case([string]$CaseName,[scriptblock]$Body) { & $Body; $script:passed++; Write-Host "PASS $CaseName" }
function Reject([scriptblock]$Body) { $rejected = $false; try { & $Body | Out-Null } catch { $rejected = $true }; if (-not $rejected) { throw 'Expected rejection' } }
function Check([bool]$Value) { if (-not $Value) { throw 'Assertion failed' } }
function PeFixture {
    $bytes = [byte[]]::new(512)
    [BitConverter]::GetBytes([uint16]0x5A4D).CopyTo($bytes,0)
    [BitConverter]::GetBytes([uint32]128).CopyTo($bytes,60)
    [BitConverter]::GetBytes([uint32]0x4550).CopyTo($bytes,128)
    [BitConverter]::GetBytes([uint16]0x8664).CopyTo($bytes,132)
    [BitConverter]::GetBytes([uint16]240).CopyTo($bytes,148)
    [BitConverter]::GetBytes([uint16]0x2000).CopyTo($bytes,150)
    [BitConverter]::GetBytes([uint16]0x20B).CopyTo($bytes,152)
    return ,$bytes
}
function Receipt {
    return '{"Kind":"ActualProductionOverlayPublicProbe","Source":"PublicFixtureOnly","ModelCalls":0,"DesktopAcceptance":"NotEvaluated","Mode":"BoundedCi","ExitCode":0,"RetainedFrames":20,"DiscardedFrames":0,"Errors":[],"StreamCompleted":true,"Checks":{"AnchorContained":true,"WorkContained":true,"NativeMatches":true,"ActualAnchorContained":true,"ActualWorkContained":true,"ForegroundUnchanged":true,"ActualButtonMatchesAnchor":true,"AnchorStable":true,"Grew":true}}' | ConvertFrom-Json
}
try {
    Case 'Win32 facts interop compiles, no Win32 call on this host' { Add-Type -Path "$script:RepositoryRoot/tools/ci/WindowsFacts.cs" }
    foreach ($name in @('windows-ci-common.ps1','run-windows-ci.ps1')) {
        Case "PowerShell parses $name" {
            $tokens=$null; $errors=$null
            [void][Management.Automation.Language.Parser]::ParseFile("$script:RepositoryRoot/scripts/$name",[ref]$tokens,[ref]$errors)
            Check ($errors.Count -eq 0)
        }
    }
    $pe = Join-Path $root 'synthetic-header.dll'
    Case 'Synthetic x64 PE32+ DLL header accepted (not a loadable DLL)' { [IO.File]::WriteAllBytes($pe,(PeFixture)); Assert-CiNativePe $pe }
    foreach ($corruption in @('dos','offset-small','offset-large','offset-overflow','signature','machine-x86','optional-small','optional-truncated','executable','pe32','truncated')) {
        Case "PE rejects $corruption" {
            $bytes = PeFixture
            switch ($corruption) {
                dos { $bytes[0]=0 }
                offset-small { [BitConverter]::GetBytes([uint32]8).CopyTo($bytes,60) }
                offset-large { [BitConverter]::GetBytes([uint32]500).CopyTo($bytes,60) }
                offset-overflow { [BitConverter]::GetBytes([uint32]::MaxValue).CopyTo($bytes,60) }
                signature { $bytes[128]=0 }
                machine-x86 { [BitConverter]::GetBytes([uint16]0x14C).CopyTo($bytes,132) }
                optional-small { [BitConverter]::GetBytes([uint16]1).CopyTo($bytes,148) }
                optional-truncated { [BitConverter]::GetBytes([uint16]500).CopyTo($bytes,148) }
                executable { [BitConverter]::GetBytes([uint16]0).CopyTo($bytes,150) }
                pe32 { [BitConverter]::GetBytes([uint16]0x10B).CopyTo($bytes,152) }
                truncated { $bytes = [byte[]]::new(63) }
            }
            [IO.File]::WriteAllBytes($pe,$bytes); Reject { Assert-CiNativePe $pe }
        }
    }
    $facts = [pscustomobject]@{ IsWindows=$true; Architecture='X64'; UserInteractive=$true; SessionId=1; WindowStation='WinSta0'; InputDesktop='Default'; Monitors=@([pscustomobject]@{WorkArea=[pscustomobject]@{Left=0;Top=0;Right=1920;Bottom=1040}}); HasShell=$true; DwmEnabled=$true }
    Case 'Server/admin fixture session allowed only as limited contract capability' { Check (@(Get-CiDesktopBlockers $facts).Count -eq 0) }
    foreach ($field in @('IsWindows','UserInteractive','SessionId','InputDesktop','Monitors','HasShell','DwmEnabled')) {
        Case "Desktop capability rejects $field" {
            $copy = $facts | ConvertTo-Json -Depth 5 | ConvertFrom-Json
            switch ($field) {
                IsWindows { $copy.IsWindows=$false }
                UserInteractive { $copy.UserInteractive=$false }
                SessionId { $copy.SessionId=0 }
                InputDesktop { $copy.InputDesktop='Winlogon' }
                Monitors { $copy.Monitors=@() }
                HasShell { $copy.HasShell=$false }
                DwmEnabled { $copy.DwmEnabled=$false }
            }
            Check (@(Get-CiDesktopBlockers $copy).Count -gt 0)
        }
    }
    $junit = Join-Path $root 'junit.xml'
    Case 'CTest counts one executed success' { [IO.File]::WriteAllText($junit,'<testsuite><testcase name="CommandPolicy" status="run"/></testsuite>'); Check ((Assert-CiCtestResults $junit 'CommandPolicy') -eq 1) }
    foreach ($xml in @('<testsuite/>','<testsuite><testcase name="Other" status="run"/></testsuite>','<testsuite><testcase name="CommandPolicy" status="run"><skipped/></testcase></testsuite>','<testsuite><testcase name="CommandPolicy" status="run"><failure/></testcase></testsuite>','<testsuite><testcase name="CommandPolicy" status="run"/><testcase name="CommandPolicy" status="run"/></testsuite>','<testsuite><testcase name="CommandPolicy" status="notrun"/></testsuite>')) {
        Case 'CTest empty/wrong/skipped/failed/duplicate result rejects' { [IO.File]::WriteAllText($junit,$xml); Reject { Assert-CiCtestResults $junit 'CommandPolicy' } }
    }
    Case 'Complete synthetic overlay receipt counts nine checks' { Check ((Assert-CiOverlayReport (Receipt)) -eq 9) }
    foreach ($field in @('Source','DesktopAcceptance','RetainedFrames','Errors','StreamCompleted','nonbool-completed','ExitCode','ModelCalls','DiscardedFrames','false-check','nonbool-check','extra-check')) {
        Case "Overlay receipt rejects $field" {
            $value = Receipt
            switch ($field) {
                Source { $value.Source='RealSelection' }
                DesktopAcceptance { $value.DesktopAcceptance='Pass' }
                RetainedFrames { $value.RetainedFrames=0 }
                Errors { $value.Errors=@('Failure') }
                StreamCompleted { $value.StreamCompleted=$false }
                nonbool-completed { $value.StreamCompleted='false' }
                ExitCode { $value.ExitCode=4 }
                ModelCalls { $value.ModelCalls=1 }
                DiscardedFrames { $value.DiscardedFrames=1 }
                false-check { $value.Checks.NativeMatches=$false }
                nonbool-check { $value.Checks.NativeMatches=1 }
                extra-check { $value.Checks | Add-Member Extra $true }
            }
            Reject { Assert-CiOverlayReport $value }
        }
    }
    if (-not $IsWindows) {
        foreach ($probe in @($false,$true)) {
            Case "Actual Linux entry subprocess ProbeOnly=$probe" {
                $info = [Diagnostics.ProcessStartInfo]::new([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
                $info.UseShellExecute=$false; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
                foreach ($argument in @('-NoProfile','-File',"$script:RepositoryRoot/scripts/run-windows-ci.ps1")) { [void]$info.ArgumentList.Add($argument) }
                if ($probe) { [void]$info.ArgumentList.Add('-ProbeOnly') }
                $child = [Diagnostics.Process]::Start($info)
                try {
                    $stdout=$child.StandardOutput.ReadToEndAsync(); $stderr=$child.StandardError.ReadToEndAsync()
                    if (-not $child.WaitForExit(30000)) { $child.Kill($true); [void]$child.WaitForExit(5000); throw 'Owned script test timeout' }
                    $output=$stdout.GetAwaiter().GetResult(); $errorOutput=$stderr.GetAwaiter().GetResult()
                    Check ($child.ExitCode -eq $(if ($probe) { 0 } else { 2 }))
                    Check ([string]::IsNullOrEmpty($errorOutput))
                    $json=($output -split 'KUAIYI_CONTRACT_REPORT_BEGIN\r?\n',2)[1] -split '\r?\nKUAIYI_CONTRACT_REPORT_END',2
                    $value=$json[0] | ConvertFrom-Json
                    $expected=if ($probe) { 'NotRun' } else { 'Blocked' }
                    Check ($value.NativeStatus -ceq $expected -and $value.OverlayStatus -ceq $expected -and $value.Windows11Acceptance -ceq 'NotRun')
                    foreach ($counter in @('NativeDlls','CTestsPassed','PeChecksPassed','DllLoadsPassed','ExportsPassed','OverlayChecksPassed')) { Check ($value.$counter -eq 0) }
                    Write-Host $output
                } finally { $child.Dispose() }
            }
        }
    }
    Write-Host "Script contracts: $passed passed; synthetic headers/receipts only; Windows DLL/desktop NotRun."
} finally { [IO.Directory]::Delete($root,$true) }
