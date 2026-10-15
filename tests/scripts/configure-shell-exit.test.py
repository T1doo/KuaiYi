"""Actual local PowerShell processes; no Windows, CMake, or lifecycle execution."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = '.github/workflows/windows-configure-only.yml'
EXECUTION = '4b29c3f95e9f5e5d16dfc501c445a7428fe74f27'
PWSH = os.environ.get('KUAIYI_TEST_PWSH') or shutil.which('pwsh')
MARKER = 'KUAIYI_CONFIGURE_ONLY_SUMMARY '


def quote_ps(path):
    return "'" + str(path).replace("'", "''") + "'"


class ConfigureShellExitTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not PWSH or not Path(PWSH).is_file():
            raise RuntimeError('Existing PowerShell required; set KUAIYI_TEST_PWSH')
        cls.workflow = (ROOT / WORKFLOW).read_text()
        cls.prior = subprocess.check_output(
            ['git', 'show', EXECUTION + ':' + WORKFLOW], cwd=ROOT, text=True)
        cls.step = cls.workflow.split(
            '      - name: Configure once, collect after cleanup, then stop\n', 1)[1]
        cls.shell = re.search(r'^        shell: (.+)$', cls.step, re.M).group(1)
        body = cls.step.split('        run: |\n', 1)[1]
        cls.body = '\n'.join(line[10:] for line in body.splitlines()) + '\n'
        cls.temp = tempfile.TemporaryDirectory(prefix='configure-shell-exit-')
        cls.directory = Path(cls.temp.name) / "space ' path"
        cls.directory.mkdir()
        cls.counter = 0
        cls.driver = Path(cls.temp.name) / 'launch.ps1'
        cls.driver.write_text(r'''param([string]$Payload)
$ErrorActionPreference='Stop'
$data=Get-Content -LiteralPath $Payload -Raw | ConvertFrom-Json
$info=[Diagnostics.ProcessStartInfo]::new()
$info.FileName=$data.Executable
$info.Arguments=$data.Arguments
$info.WorkingDirectory=$data.WorkingDirectory
$info.UseShellExecute=$false
$info.RedirectStandardOutput=$true
$info.RedirectStandardError=$true
$process=[Diagnostics.Process]::new()
$process.StartInfo=$info
try {
    [void]$process.Start()
    $stdout=$process.StandardOutput.ReadToEndAsync()
    $stderr=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(20000)){$process.Kill($true);throw 'Synthetic shell timeout'}
    $result=@{ExitCode=$process.ExitCode;Stdout=$stdout.GetAwaiter().GetResult();Stderr=$stderr.GetAwaiter().GetResult()}
    $result | ConvertTo-Json -Compress
} finally {$process.Dispose()}
''')

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def invoke(self, script, *, builtin=False, fixture=None, unquoted=False, runner_fixup=True):
        type(self).counter += 1
        wrapper = self.directory / f'wrapper {self.counter}.ps1'
        if builtin:
            # The real built-in template does not escape apostrophes in script paths.
            # Reproduce exit normalization with its ordinary runner-style wrapper path.
            wrapper = Path(self.temp.name) / f'wrapper{self.counter}.ps1'
        if fixture is not None:
            child = self.directory / f'fixture {self.counter}.ps1'
            child.write_text(fixture)
            script = script.replace('./tests/scripts/run-configure-only-windows.ps1', quote_ps(child))
        if runner_fixup:
            # ScriptHandlerHelpers currently applies this for shellCommand=pwsh,
            # including the custom template. Also test our explicit body without it.
            script = "$ErrorActionPreference='Stop'\n" + script
            script += '\nif ((Test-Path -LiteralPath variable:\\LASTEXITCODE)) { exit $LASTEXITCODE }\n'
        if builtin:
            argument_format = '-NoLogo -NoProfile -NonInteractive -Command ". \'{0}\'"'
        else:
            executable, argument_format = self.shell.split(' ', 1)
            self.assertEqual(executable, 'pwsh')
            self.assertEqual(argument_format.count('{0}'), 1)
            if unquoted:
                argument_format = argument_format.replace('"{0}"', '{0}')
        wrapper.write_text(script)
        # Match runner ScriptHandler: format the argument STRING, then pass it to
        # ProcessStartInfo.Arguments. An argv list would mask missing path quotes.
        arguments = argument_format.format(str(wrapper).replace('"', '\\"'))
        payload = wrapper.with_suffix('.json')
        payload.write_text(json.dumps(dict(Executable=PWSH, Arguments=arguments, WorkingDirectory=str(ROOT))))
        driver = subprocess.run([PWSH, '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(self.driver), str(payload)],
                                cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=25)
        self.assertEqual(driver.returncode, 0, driver.stderr)
        result = json.loads(driver.stdout)
        return subprocess.CompletedProcess(arguments, result['ExitCode'], result['Stdout'], result['Stderr'])

    @staticmethod
    def synthetic(code, *, cleanup=True):
        receipt = dict(Kind='SyntheticShellExitOnly', Status='Pass' if code == 0 else 'Fail',
                       ExitCode=code, CleanupConfirmed=cleanup, ModelCalls=0)
        return "Write-Output '" + MARKER + json.dumps(receipt, separators=(',', ':')) + f"'\nexit {code}\n"

    def receipt(self, result):
        rows = [line[len(MARKER):] for line in result.stdout.splitlines() if line.startswith(MARKER)]
        self.assertEqual(len(rows), 1)
        return json.loads(rows[0])

    def test_builtin_reproduces_observed_three_to_one(self):
        result = self.invoke(self.body, builtin=True, fixture=self.synthetic(3))
        self.assertEqual(result.returncode, 1)
        self.assertEqual(self.receipt(result)['ExitCode'], 3)

    def test_custom_file_preserves_all_defined_exit_codes(self):
        for code in (0, 1, 2, 3):
            with self.subTest(code=code):
                result = self.invoke(self.body, fixture=self.synthetic(code))
                self.assertEqual(result.returncode, code)
                self.assertEqual(self.receipt(result)['ExitCode'], code)

    def test_unquoted_argument_string_rejects_space_path(self):
        result = self.invoke(self.body, unquoted=True, fixture=self.synthetic(3))
        self.assertEqual(result.returncode, 64)
        self.assertNotIn(MARKER, result.stdout)

    def test_explicit_step_body_preserves_failure_without_runner_fixups(self):
        result = self.invoke(self.body, runner_fixup=False, fixture=self.synthetic(3))
        self.assertEqual(result.returncode, 3)
        self.assertEqual(self.receipt(result)['ExitCode'], 3)

    def test_custom_file_keeps_cleanup_failure_receipt(self):
        result = self.invoke(self.body, fixture=self.synthetic(3, cleanup=False))
        self.assertEqual(result.returncode, 3)
        row = self.receipt(result)
        self.assertFalse(row['CleanupConfirmed'])
        self.assertEqual(row['Status'], 'Fail')

    def test_custom_file_retains_nonterminating_error_stop(self):
        result = self.invoke(self.body, fixture="Write-Error 'SYNTHETIC_ERROR'\nWrite-Output 'UNREACHABLE'\nexit 0\n")
        self.assertEqual(result.returncode, 1)
        self.assertNotIn('UNREACHABLE', result.stdout)
        self.assertNotIn(MARKER, result.stdout)

    def test_custom_file_keeps_terminating_error_failure(self):
        result = self.invoke(self.body, fixture="throw 'SYNTHETIC_THROW'\nWrite-Output 'UNREACHABLE'\n")
        self.assertEqual(result.returncode, 1)
        self.assertNotIn('UNREACHABLE', result.stdout)
        self.assertNotIn(MARKER, result.stdout)

    @unittest.skipIf(os.name == 'nt', 'Actual entry must remain on its Linux Blocked path')
    def test_actual_linux_blocked_entry_keeps_two_and_no_operations(self):
        result = self.invoke(self.body)
        self.assertEqual(result.returncode, 2)
        row = self.receipt(result)
        self.assertEqual(row['Status'], 'Blocked')
        self.assertEqual(row['Reason'], 'HostedWindowsRequired')
        self.assertEqual(row['Operations'], [])
        self.assertEqual(row['CollectorSettlement'], 'NotStarted')
        for key in ('NativeBuild', 'ManagedRestore', 'ManagedBuild', 'FourCases'):
            self.assertEqual(row[key], 'NotRun')
        self.assertEqual(row['ModelCalls'], 0)

    def test_only_execution_shell_changed_and_source_gate_still_rejects_new_head(self):
        old = '        shell: pwsh\n        run: |\n          & ./tests/scripts/run-configure-only-windows.ps1'
        new = '        shell: pwsh -NoLogo -NoProfile -NonInteractive -File "{0}"\n        run: |\n          $ErrorActionPreference=\'Stop\'\n          & ./tests/scripts/run-configure-only-windows.ps1'
        self.assertEqual(self.prior.count(old), 1)
        self.assertEqual(self.workflow, self.prior.replace(old, new))
        # No future CI is prepared: the frozen b459 parent still rejects this local descendant.
        locked_parent = re.search(r"\$parent='([0-9a-f]{40})'", self.workflow).group(1)
        current = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        actual_parent = subprocess.check_output(['git', 'rev-parse', 'HEAD^'], cwd=ROOT, text=True).strip()
        self.assertNotEqual(current, EXECUTION)
        self.assertNotEqual(actual_parent, locked_parent)


if __name__ == '__main__':
    unittest.main(verbosity=2)
