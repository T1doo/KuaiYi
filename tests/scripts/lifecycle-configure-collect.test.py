"""Offline injected bridge/adapter tests. No Windows API or CMake execution."""
import contextlib
import ctypes as c
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools/ci'))
import lifecycle_configure_collect as collector
import lifecycle_configure_windows as w

spec = importlib.util.spec_from_file_location('adapter_tests', Path(__file__).with_name('lifecycle-configure-windows.test.py'))
adapter_tests = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter_tests)
FakeFiles = adapter_tests.FakeFiles
SECRET = adapter_tests.SECRET
PROOF = w.RootProof(456, 5, 6)  # foreign handle; FakeFiles must never see 456


class Bridge:
    def __init__(self):
        self.events = []

    def receive(self, pid, handle):
        self.events.append(('receive', pid, handle))
        return 900  # handle now valid in Python, distinct from parent's handle

    def close(self, handle):
        self.events.append(('close', handle))


class CollectTests(unittest.TestCase):
    def run_collect(self, *, cleanup=True, fs=None, bridge=None, proof=PROOF, pid=123):
        fs = fs if fs is not None else FakeFiles()
        bridge = bridge if bridge is not None else Bridge()
        result = collector.collect(pid, proof, cleanup_confirmed=cleanup, bridge=bridge, filesystem=fs)
        encoded = json.dumps(result)
        for text in ('PRIVATE_SENTINEL', 'C:\\private', '--key=secret'):
            self.assertNotIn(text, encoded)
        self.assertEqual(fs.handles, {})
        return result, fs, bridge

    def test_cleanup_gate_no_bridge_or_file_calls(self):
        for value in (False, None, 1, 'true'):
            result, fs, bridge = self.run_collect(cleanup=value)
            self.assertEqual(result, collector.empty('SkippedCleanupUnconfirmed'))
            self.assertEqual((fs.calls, bridge.events), ([], []))
        with patch.object(collector, 'SourceHandles', side_effect=AssertionError('must not construct')):
            self.assertEqual(collector.collect(123, PROOF, cleanup_confirmed=False)['Files'], [])

    def test_foreign_handle_only_used_with_explicit_source_then_local_duplicate(self):
        result, fs, bridge = self.run_collect()
        self.assertEqual(result['DiagnosticStatus'], 'Collected')
        self.assertEqual(bridge.events, [('receive', 123, 456), ('close', 900)])
        self.assertEqual(fs.calls[0], ('duplicate', 900))
        self.assertNotIn(('duplicate', 456), fs.calls)

    def test_invalid_source_proof_zero_native_calls(self):
        for pid, proof in ((0, PROOF), (True, PROOF), (2**32, PROOF), (123, None),
                           (123, w.RootProof(True, 5, 6)), (123, w.RootProof(2**128, 5, 6))):
            result, fs, bridge = self.run_collect(pid=pid, proof=proof)
            self.assertEqual(result['DiagnosticStatus'], 'OwnershipUnconfirmed')
            self.assertEqual((bridge.events, fs.calls), ([], []))

    def test_receive_exception_preserves_original_and_reads_nothing(self):
        original = dict(Status='Timeout', Exit='0', CleanupConfirmed=True, RescueUsed=True, BudgetExpired=True)
        saved = original.copy()
        bridge = Bridge()
        bridge.receive = lambda *args: (_ for _ in ()).throw(RuntimeError(SECRET))
        result, fs, bridge = self.run_collect(bridge=bridge, cleanup=original['CleanupConfirmed'])
        self.assertEqual(result['DiagnosticStatus'], 'SafetyUnknown')
        self.assertEqual(fs.calls, [])
        self.assertEqual(original, saved)

    def test_projection_exception_still_closes_received_handle(self):
        with patch.object(w, 'project_windows_after_cleanup', side_effect=RuntimeError(SECRET)):
            result, fs, bridge = self.run_collect()
        self.assertEqual(result, collector.empty('SafetyUnknown'))
        self.assertEqual(bridge.events[-1], ('close', 900))

    def test_close_error_discards_projection(self):
        bridge = Bridge()
        bridge.close = lambda handle: (_ for _ in ()).throw(RuntimeError(SECRET))
        result, fs, _ = self.run_collect(bridge=bridge)
        self.assertEqual(result, collector.empty('SafetyUnknown'))
        self.assertEqual(len(fs.reads), 4)

    def test_four_files_bounded_at_64k_each_total_256k_and_no_partial_facts(self):
        fs = FakeFiles()
        for _, parts in w.projection.FILES:
            fs.file(parts, b'cmake version 3.31.6\n' + b'x' * 65536)
        result, fs, _ = self.run_collect(fs=fs)
        self.assertEqual(fs.reads, [65536] * 4)
        self.assertEqual(sum(fs.reads), 262144)
        self.assertTrue(all(row['ReadStatus'] == 'Truncated' and row['Facts'] == w.projection._facts() for row in result['Files']))

    def test_missing_logs_are_missing_never_created(self):
        fs = FakeFiles()
        for _, parts in w.projection.FILES:
            del fs.nodes[parts]
        before = fs.nodes.copy()
        result, fs, _ = self.run_collect(fs=fs)
        self.assertTrue(all(row['ReadStatus'] == 'Missing' for row in result['Files']))
        self.assertEqual(fs.reads, [])
        self.assertEqual(fs.nodes, before)

    def test_hostile_unknown_log_bytes_and_duplicate_facts_do_not_escape(self):
        fs = FakeFiles()
        fs.file(w.projection.FILES[0][1], b'cmake version 3.31.6\ncmake version 3.31.6\n'+SECRET.encode())
        result, _, _ = self.run_collect(fs=fs)
        self.assertEqual(result['Files'][0]['ReadStatus'], 'Unknown')
        self.assertTrue(all(row['Facts'] == w.projection._facts() for row in result['Files']))

    def test_wrong_root_identity_zero_content_reads_and_handle_closed(self):
        result, fs, bridge = self.run_collect(proof=w.RootProof(456, 5, 99))
        self.assertEqual(result, collector.empty('SafetyUnknown'))
        self.assertEqual(fs.reads, [])
        self.assertEqual(bridge.events[-1], ('close', 900))

    def test_real_isolated_cli_unconfirmed_and_non_windows_no_traceback(self):
        for args, expected in ((['unconfirmed'], 'SkippedCleanupUnconfirmed'),
                               (['confirmed', '123', '456', '5', '6'], 'SafetyUnknown'),
                               ([SECRET], 'OwnershipUnconfirmed')):
            if sys.platform == 'win32' and args[0] == 'confirmed':
                continue  # never perform a real cross-process/native operation in this suite
            p = subprocess.run([sys.executable, '-I', '-B', str(REPO/'tools/ci/lifecycle_configure_collect.py'), *args], capture_output=True, text=True, timeout=10)
            self.assertEqual(p.returncode, 0)
            self.assertEqual(p.stderr, '')
            self.assertEqual(json.loads(p.stdout), collector.empty(expected))

    def test_cli_rejects_private_or_unbounded_numeric_inputs(self):
        for args in (['confirmed', SECRET, '456', '5', '6'], ['confirmed', '1'*21, '2', '3', '4'], ['confirmed', '１２３', '2', '3', '4']):
            output = io.StringIO()
            with contextlib.redirect_stdout(output), patch.object(collector, 'SourceHandles', side_effect=AssertionError('no binding')):
                self.assertEqual(collector.main(args), 0)
            self.assertEqual(json.loads(output.getvalue()), collector.empty('OwnershipUnconfirmed'))

    def test_native_bridge_abi_uses_source_process_and_closes_process_immediately(self):
        calls = []
        class Function:
            def __init__(self, name): self.name = name
            def __call__(self, *args):
                calls.append((self.name, args))
                if self.name == 'OpenProcess': return 700
                if self.name == 'DuplicateHandle':
                    c.cast(args[3], c.POINTER(w.HANDLE))[0] = w.HANDLE(900)
                return 1
        class Dll:
            def __getattr__(self, name):
                fn = Function(name)
                setattr(self, name, fn)
                return fn
        with patch.object(collector.os, 'name', 'nt'), patch.object(c, 'WinDLL', return_value=Dll(), create=True):
            bridge = collector.SourceHandles()
            self.assertEqual(bridge.receive(123, 456), 900)
            self.assertEqual(calls[0], ('OpenProcess', (0x40, False, 123)))
            args = calls[1][1]
            self.assertEqual((args[0].value, args[1].value, args[2].value, args[4:]), (700, 456, w.HANDLE(-1).value, (0, False, 2)))
            self.assertEqual(calls[-1][0], 'CloseHandle')
            self.assertEqual(calls[-1][1][0].value, 700)
            bridge.close(900)
            self.assertEqual(calls[-1][1][0].value, 900)

    def test_duplicate_or_source_close_failure_never_double_closes_target(self):
        for duplicate_ok in (False, True):
            closed = []
            class Api:
                def OpenProcess(self, *args): return 700
                def DuplicateHandle(self, *args):
                    c.cast(args[3], c.POINTER(w.HANDLE))[0] = w.HANDLE(900)
                    return int(duplicate_ok)
                def CloseHandle(self, handle):
                    closed.append(handle.value)
                    return 0 if handle.value == 700 else 1
            bridge = object.__new__(collector.SourceHandles)
            bridge.k = Api()
            with self.assertRaises(w.Rejected): bridge.receive(123, 456)
            self.assertEqual(closed.count(700), 1)
            self.assertEqual(closed.count(900), 1)


if __name__ == '__main__':
    unittest.main(verbosity=2)
