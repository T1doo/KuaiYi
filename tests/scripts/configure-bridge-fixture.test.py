"""Offline contracts for the isolated Windows bridge fixture. No native calls."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'tools/ci'))
import lifecycle_configure_windows as w

probe_spec = importlib.util.spec_from_file_location('configure_bridge_owned_probe', ROOT/'tests/scripts/configure-bridge-owned-probe.py')
probe = importlib.util.module_from_spec(probe_spec)
probe_spec.loader.exec_module(probe)

spec = importlib.util.spec_from_file_location('adapter_tests', Path(__file__).with_name('lifecycle-configure-windows.test.py'))
adapter_tests = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter_tests)


class FakeBridge:
    def __init__(self):
        self.receives = 0
        self.closed = 0

    def receive(self, pid, handle):
        self.receives += 1
        assert (pid, handle) == (123, 456)
        return 900

    def close(self, handle):
        assert handle == 900
        self.closed += 1


class FakeFiles(adapter_tests.FakeFiles):
    def __init__(self):
        super().__init__()
        self.owned = set()

    def duplicate(self, handle):
        value = super().duplicate(handle)
        self.owned.add(value)
        return value

    def open_child(self, parent, name, directory):
        value = super().open_child(parent, name, directory)
        self.owned.add(value)
        return value

    def close(self, handle):
        super().close(handle)
        self.owned.discard(handle)


class FixtureTests(unittest.TestCase):
    def test_actual_collector_with_counted_binding_four_files(self):
        fs, bridge = FakeFiles(), FakeBridge()
        fs.file(w.projection.FILES[0][1], b'cmake version 3.31.6')
        fs.file(w.projection.FILES[1][1], b'-- Configuring done\n-- Generating done\nPRIVATE_SYNTHETIC_SENTINEL')
        fs.file(w.projection.FILES[2][1], b'CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022')
        fs.file(w.projection.FILES[3][1], b'  -\n    kind: "try_compile-v1"')
        row = probe.observe(123, w.RootProof(456, 5, 6), bridge=bridge, filesystem=fs)
        self.assertEqual((row['BridgeReceives'], row['BridgeCloses'], row['ReadCalls'], row['AdapterHandlesOpen']), (1, 1, 4, 0))
        self.assertEqual(row['ReadBytes'], sum(fs.reads))
        self.assertEqual(row['Diagnostic']['Files'][0]['Facts']['CMakeVersion'], '3.31.6')
        self.assertTrue(row['Diagnostic']['Files'][1]['Facts']['GenerateDone'])
        self.assertTrue(row['Diagnostic']['Files'][2]['Facts']['VisualStudio2022'])
        self.assertTrue(row['Diagnostic']['Files'][3]['Facts']['TryCompileEvent'])
        self.assertNotIn('PRIVATE_SYNTHETIC_SENTINEL', json.dumps(row))

    def test_wrong_identity_zero_read_and_target_closed_once(self):
        fs, bridge = FakeFiles(), FakeBridge()
        row = probe.observe(123, w.RootProof(456, 5, 99), bridge=bridge, filesystem=fs)
        self.assertEqual(row['Diagnostic'], dict(DiagnosticStatus='SafetyUnknown', Files=[]))
        self.assertEqual((row['BridgeReceives'], row['BridgeCloses'], row['ReadCalls'], row['ReadBytes'], row['AdapterHandlesOpen']), (1, 1, 0, 0, 0))

    def test_invalid_proof_zero_read_no_target_close(self):
        fs, bridge = FakeFiles(), FakeBridge()
        row = probe.observe(123, w.RootProof(0, 5, 6), bridge=bridge, filesystem=fs)
        self.assertEqual(row['Diagnostic'], dict(DiagnosticStatus='OwnershipUnconfirmed', Files=[]))
        self.assertEqual((row['BridgeReceives'], row['BridgeCloses'], row['ReadCalls'], row['AdapterHandlesOpen']), (0, 0, 0, 0))

    def test_actual_four_file_read_cap(self):
        fs, bridge = FakeFiles(), FakeBridge()
        for _, parts in w.projection.FILES:
            fs.file(parts, b'x'*65537)
        row = probe.observe(123, w.RootProof(456, 5, 6), bridge=bridge, filesystem=fs)
        self.assertEqual(row['ReadBytes'], 262144)
        self.assertEqual(fs.reads, [65536]*4)
        self.assertEqual((row['BridgeCloses'], row['AdapterHandlesOpen']), (1, 0))
        self.assertTrue(all(file['ReadStatus'] == 'Truncated' and file['Facts'] == w.projection._facts() for file in row['Diagnostic']['Files']))

    def test_missing_files_not_created(self):
        fs, bridge = FakeFiles(), FakeBridge()
        for _, parts in w.projection.FILES[1:]:
            del fs.nodes[parts]
        before = fs.nodes.copy()
        row = probe.observe(123, w.RootProof(456, 5, 6), bridge=bridge, filesystem=fs)
        self.assertEqual([x['ReadStatus'] for x in row['Diagnostic']['Files'][1:]], ['Missing']*3)
        self.assertEqual(row['ReadCalls'], 1)
        self.assertEqual(fs.nodes, before)

    def test_isolated_entry_has_closed_linux_result(self):
        args = ['confirmed', '123', '456', '5', '6'] if sys.platform != 'win32' else []
        p = subprocess.run([sys.executable, '-I', '-B', str(ROOT/'tests/scripts/configure-bridge-owned-probe.py'), *args], capture_output=True, text=True, timeout=10)
        self.assertEqual(p.returncode, 0)
        self.assertEqual(p.stderr, '')
        self.assertEqual(json.loads(p.stdout)['Diagnostic'], dict(DiagnosticStatus='ReaderUnavailable' if args else 'OwnershipUnconfirmed', Files=[]))

    def test_isolated_workflow_scope_and_no_product_execution(self):
        workflow = (ROOT/'.github/workflows/windows-configure-bridge.yml').read_text()
        script = (ROOT/'tests/scripts/run-configure-bridge-windows.ps1').read_text()
        self.assertIn('branches: [dev/configure-bridge-fixture]', workflow)
        self.assertIn('paths: [.github/workflows/windows-configure-bridge.yml]', workflow)
        self.assertIn("github.ref == 'refs/heads/dev/configure-bridge-fixture'", workflow)
        self.assertIn('fetch-depth: 2', workflow)
        self.assertIn('2798893ddd3f0aadbbbb5811cc3ca81f9996f918', workflow)
        self.assertIn('timeout-minutes: 10', workflow)
        self.assertIn('timeout-minutes: 7', workflow)
        self.assertIn('runs-on: windows-2022', workflow)
        self.assertIn('persist-credentials: false', workflow)
        self.assertIn('BridgeSourceIdentityVerified', workflow)
        self.assertNotRegex(workflow, r'workflow_dispatch|upload-artifact|setup-python|run-lifecycle-windows-ci|run-source-head-windows-ci|rerun')
        for forbidden in ('Invoke-BoundedLifecycleOperation', "Get-LifecycleApplicationPath 'cmake'", 'KuaiYi.ReuseLifecycleNative.exe', 'AdjustTokenPrivileges', 'StartService', 'OpenProcess('):
            self.assertNotIn(forbidden, script)
        self.assertIn("$original.Status='Timeout'", script)
        self.assertIn('Complete-LifecycleConfigureDiagnostic $original $positive', script)
        self.assertIn('Check \'CleanupGate\'', script)
        self.assertIn('Read-ActualProbe', script)

    def test_original_workflows_unchanged(self):
        paths = ('.github/workflows/windows-lifecycle-validation.yml', '.github/workflows/windows-contracts.yml', '.github/workflows/windows-configure-files.yml')
        for path in paths:
            prior = subprocess.check_output(['git', 'show', '2798893ddd3f0aadbbbb5811cc3ca81f9996f918:'+path], cwd=ROOT)
            self.assertEqual((ROOT/path).read_bytes(), prior)


if __name__ == '__main__':
    unittest.main(verbosity=2)
