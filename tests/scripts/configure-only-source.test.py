"""Owned local Git and in-memory bridge contracts. No Windows or CMake calls."""
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'tools/ci'))
import lifecycle_configure_collect as bridge
import lifecycle_configure_windows as w

spec = importlib.util.spec_from_file_location('adapter_tests', ROOT/'tests/scripts/lifecycle-configure-windows.test.py')
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)
PARENT = 'b4592d5d60d38cbbf186a38dbba2b1ae07b0482c'
PARENT_TREE = '0b420e3ffe04d332422f9f1aa16b6a09331f2f39'
LOCKED = '3ae69419a1152ea9694a3a9cf11130fbae645fa3'
LOCKED_TREE = '870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b'
RUNTIME = ['tools/KuaiYi.ReuseLifecycle','tools/KuaiYi.ReuseLifecycleNative',
           'tools/KuaiYi.SelectionReuseDiagnostic/ExclusiveKillJob.cs',
           'tools/KuaiYi.SelectionReuseNative/RequestLifetime.h',
           'src/KuaiYi.Windows/app.manifest','global.json','Directory.Build.props','Directory.Build.targets']


def git(*args, cwd=ROOT, check=True):
    return subprocess.run(['git', *args], cwd=cwd, check=check,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=20)


class ReferenceBridge:
    def __init__(self, refs):
        self.refs, self.closed = refs, []

    def receive(self, pid, handle):
        assert (pid, handle) == (123, 456)
        if 'parent' not in self.refs:
            raise w.Rejected()
        self.refs.add('received')
        self.refs.remove('parent')  # Source closes after the actual duplicate.
        return 900

    def close(self, handle):
        assert handle == 900
        self.refs.remove('received')
        self.closed.append(handle)


class ReferenceFiles(adapter.FakeFiles):
    def __init__(self, refs):
        super().__init__()
        self.refs = refs

    def duplicate(self, handle):
        assert self.refs == {'received'}
        result = super().duplicate(handle)
        self.refs.add('adapter')
        return result

    def read(self, handle, amount):
        assert self.refs == {'received', 'adapter'}
        return super().read(handle, amount)

    def close(self, handle):
        super().close(handle)
        if handle == 1000:
            self.refs.remove('adapter')


class ConfigureOnlySourceTests(unittest.TestCase):
    def test_received_root_survives_parent_close_until_reads_end(self):
        refs = {'parent'}
        b, fs = ReferenceBridge(refs), ReferenceFiles(refs)
        result = bridge.collect(123, w.RootProof(456, 5, 6), cleanup_confirmed=True, bridge=b, filesystem=fs)
        self.assertEqual(result['DiagnosticStatus'], 'Collected')
        self.assertEqual(len(fs.reads), 4)
        self.assertEqual(refs, set())
        self.assertEqual(b.closed, [900])
        self.assertEqual(fs.handles, {})

    def test_parent_close_before_receive_rejects_without_file_api(self):
        refs = set()
        b, fs = ReferenceBridge(refs), ReferenceFiles(refs)
        result = bridge.collect(123, w.RootProof(456, 5, 6), cleanup_confirmed=True, bridge=b, filesystem=fs)
        self.assertEqual(result, dict(DiagnosticStatus='SafetyUnknown', Files=[]))
        self.assertEqual(fs.calls, [])
        self.assertEqual(b.closed, [])

    def test_exact_new_parent_and_real_shallow_ancestry(self):
        self.assertEqual(git('rev-parse', PARENT+'^{tree}').stdout.strip(), PARENT_TREE)
        # One candidate child is required, including both endpoints needs 12 commits.
        self.assertEqual(int(git('rev-list', '--count', LOCKED+'..'+PARENT).stdout), 10)
        with tempfile.TemporaryDirectory(prefix='configure-only-source-') as temp:
            bare = Path(temp)/'owned.git'
            git('clone', '--bare', '--shared', str(ROOT), str(bare))
            candidate = git('-c', 'user.name=Synthetic', '-c', 'user.email=fixture@example.invalid',
                            'commit-tree', PARENT_TREE, '-p', PARENT, '-m', 'Owned checkout depth fixture', cwd=bare).stdout.strip()
            git('update-ref', 'refs/heads/candidate', candidate, cwd=bare)
            for depth in (11, 12):
                clone = Path(temp)/str(depth)
                git('clone', '--no-checkout', '--depth', str(depth), '--single-branch', '--branch', 'candidate', bare.as_uri(), str(clone))
                self.assertEqual(git('rev-list', '--parents', '-n', '1', 'HEAD', cwd=clone).stdout.strip(), candidate+' '+PARENT)
                ancestry = git('merge-base', '--is-ancestor', LOCKED, 'HEAD', cwd=clone, check=False)
                if depth == 11:
                    self.assertNotEqual(ancestry.returncode, 0)
                else:
                    self.assertEqual(ancestry.returncode, 0)
                    self.assertEqual(git('rev-parse', LOCKED+'^{tree}', cwd=clone).stdout.strip(), LOCKED_TREE)
                    self.assertEqual(git('diff', '--quiet', LOCKED, 'HEAD', '--', *RUNTIME, cwd=clone).returncode, 0)
                    git('checkout', '--quiet', 'HEAD', cwd=clone)
                    p = clone/'global.json';p.write_text(p.read_text()+'\nfixture mutation\n')
                    self.assertEqual(git('diff', '--quiet', LOCKED, '--', *RUNTIME, cwd=clone, check=False).returncode, 1)

    def test_old_runner_workflows_and_native_dependencies_unchanged(self):
        paths = ['tests/scripts/run-lifecycle-windows-ci.ps1','tools/ci/LifecycleOperation.cs',
                 'tools/ci/ConfigureRootLease.cs','tools/ci/lifecycle_configure_collect.py',
                 'tools/ci/lifecycle_configure_windows.py','tools/ci/lifecycle_configure_projection.py',
                 'tests/scripts/lifecycle-configure-diagnostics.ps1']
        paths += ['.github/workflows/'+name for name in ('windows-contracts.yml','windows-lifecycle-validation.yml','windows-configure-files.yml','windows-configure-bridge.yml')]
        for path in paths:
            prior = subprocess.check_output(['git','show',PARENT+':'+path], cwd=ROOT)
            self.assertEqual((ROOT/path).read_bytes(), prior, path)

    def test_workflow_scope_bounded_and_exact_source_gates(self):
        s = (ROOT/'.github/workflows/windows-configure-only.yml').read_text()
        for expected in ('branches: [dev/configure-only-diagnostic]', 'paths: [.github/workflows/windows-configure-only.yml]',
                         "github.event_name == 'push'", 'runs-on: windows-2022', 'fetch-depth: 12',
                         'timeout-minutes: 10','timeout-minutes: 7','persist-credentials: false',
                         'ref: ${{ github.sha }}', PARENT, PARENT_TREE, LOCKED, LOCKED_TREE,
                         'Compare-Object $allowed $changed', 'ConfigureOnlySourceIdentityVerified'):
            self.assertIn(expected, s)
        self.assertNotRegex(s, r'workflow_dispatch|schedule:|matrix:|rerun|upload-artifact|setup-python|run-lifecycle-windows-ci')
        for path in (ROOT/'.github/workflows').glob('*.yml'):
            if path.name != 'windows-configure-only.yml':
                self.assertNotIn('branches: [dev/configure-only-diagnostic]', path.read_text())

    def test_entry_retains_original_configure_and_terminal_scope(self):
        s = (ROOT/'tests/scripts/run-configure-only-windows.ps1').read_text()
        self.assertIn("ReviewedParent='"+PARENT+"'", s)
        operations = re.findall(r"=Invoke-ConfigureOnlyOperation '([A-Za-z]+)'", s)
        self.assertEqual(operations, ['SourceHead','SourceParent','SourceAncestry','SourceLockedTree','SourceStatus','SourceCompare',
                                     'CMakeVersion','DotnetVersion','VisualStudioInventory','NativeConfigure'])
        self.assertNotRegex(s, r'--build|--supervise|--locked-mode|AdjustTokenPrivileges|StartService|Install-')
        self.assertIn("$script:summary.CollectorSettlement='Unknown'", s)
        self.assertNotIn('Get-LifecycleBuildEvidence', s)
        self.assertIn("Scope='NativeConfigureOnly'", s)


if __name__ == '__main__':
    unittest.main(verbosity=2)
