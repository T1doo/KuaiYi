"""Windows adapter contract tests via isolated fake file API. NO native Windows calls."""
import ctypes
from dataclasses import replace
import importlib.util
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/ci'))
import lifecycle_configure_windows as w

SECRET = 'PRIVATE_SENTINEL C:\\private\\account --key=secret'
PROOF = w.RootProof(900, 5, 6)


class FakeFiles:
    def __init__(self):
        self.calls, self.reads, self.closed = [], [], []
        self.nodes = {(): [w.FileInfo(5, 6, 0x10, 0, 1, 1, 1), b'']}
        self.handles, self.next_handle = {}, 1000
        self.ntfs, self.hook = True, None
        self.directory(('native',), 7)
        self.directory(('native', 'CMakeFiles'), 8)
        for i, (kind, parts) in enumerate(w.projection.FILES):
            self.file(parts, b'unknown: '+SECRET.encode(), 20+i)

    def directory(self, parts, index):
        self.nodes[parts] = [w.FileInfo(5, index, 0x10, 0, 1, 1, 1), b'']

    def file(self, parts, data, index=20):
        self.nodes[parts] = [w.FileInfo(5, index, 0x80, len(data), 1, 1, 1), data]

    def duplicate(self, handle):
        self.calls.append(('duplicate', handle))
        if handle != 900:
            raise w.Rejected(SECRET)
        self.handles[1000] = ()
        return 1000

    def info(self, handle):
        self.calls.append(('info', handle))
        if self.hook:
            self.hook('info', handle)
        return self.nodes[self.handles[handle]][0]

    def local_ntfs(self, handle):
        self.calls.append(('local_ntfs', handle))
        return self.ntfs

    def open_child(self, parent, name, directory):
        self.calls.append(('open', parent, name, directory))
        w._policy(name, directory)
        path = self.handles[parent] + (name,)
        if path not in self.nodes:
            raise w.Missing(SECRET)
        self.next_handle += 1
        self.handles[self.next_handle] = path
        if self.hook:
            self.hook('open', self.next_handle)
        return self.next_handle

    def read(self, handle, amount):
        self.calls.append(('read', handle, amount))
        self.reads.append(amount)
        if self.hook:
            self.hook('read', handle)
        return self.nodes[self.handles[handle]][1][:amount]

    def close(self, handle):
        self.calls.append(('close', handle))
        self.closed.append(handle)
        del self.handles[handle]


class WindowsProjectionTests(unittest.TestCase):
    def run_projection(self, fs, proof=PROOF, cleanup=True):
        r = w.project_windows_after_cleanup(proof, cleanup_confirmed=cleanup, filesystem=fs)
        self.assertNotIn('PRIVATE_SENTINEL', json.dumps(r))
        self.assertNotIn('C:\\private', json.dumps(r))
        self.assertNotIn('Status', r)
        self.assertNotIn('Reason', r)
        self.assertEqual(fs.handles, {})
        return r

    def test_cleanup_gate_zero_api_calls(self):
        for value in (False, None, 1, 'true'):
            fs = FakeFiles()
            self.assertEqual(self.run_projection(fs, cleanup=value)['DiagnosticStatus'], 'SkippedCleanupUnconfirmed')
            self.assertEqual(fs.calls, [])

    def test_absent_or_invalid_ownership_zero_api_calls(self):
        for proof in (None, SECRET, w.RootProof(0, 5, 6), w.RootProof(True, 5, 6), w.RootProof(900, 0, 6), w.RootProof(900, 5, 0), w.RootProof(2**128, 5, 6), w.RootProof(900, 2**32, 6)):
            fs = FakeFiles()
            self.assertEqual(self.run_projection(fs, proof)['DiagnosticStatus'], 'OwnershipUnconfirmed')
            self.assertEqual(fs.calls, [])

    def test_wrong_retained_root_identity_zero_content_reads(self):
        fs = FakeFiles()
        fs.nodes[()][0] = replace(fs.nodes[()][0], index=999)
        self.assertEqual(self.run_projection(fs)['DiagnosticStatus'], 'SafetyUnknown')
        self.assertEqual(fs.reads, [])

    def test_remote_unknown_refs_non_ntfs_zero_reads(self):
        for answer in (False, None, 1, 'NTFS'):
            fs = FakeFiles()
            fs.ntfs = answer
            self.assertEqual(self.run_projection(fs)['DiagnosticStatus'], 'SafetyUnknown')
            self.assertEqual(fs.reads, [])

    def test_root_reparse_or_wrong_type_zero_reads(self):
        for attr in (0x410, 0x1010, 0x50, 0x80):
            fs = FakeFiles()
            fs.nodes[()][0] = replace(fs.nodes[()][0], attributes=attr)
            self.assertEqual(self.run_projection(fs)['DiagnosticStatus'], 'SafetyUnknown')
            self.assertEqual(fs.reads, [])

    def test_complete_projection_four_relative_fixed_paths(self):
        fs = FakeFiles()
        fs.file(w.projection.FILES[0][1], b'cmake version 3.31.6')
        fs.file(w.projection.FILES[1][1], b'-- Configuring done\n-- Generating done')
        rows = self.run_projection(fs)['Files']
        self.assertEqual(len(rows), 4)
        self.assertEqual(rows[0]['Facts']['CMakeVersion'], '3.31.6')
        self.assertTrue(rows[1]['Facts']['GenerateDone'])
        opened = [x[2] for x in fs.calls if x[0] == 'open']
        self.assertEqual(opened, ['CMakeVersion.local.log', 'NativeConfigure.local.log', 'native', 'CMakeCache.txt', 'native', 'CMakeFiles', 'CMakeConfigureLog.yaml'])
        self.assertEqual(len(fs.reads), 4)
        self.assertNotIn(900, fs.closed)

    def test_directory_reparse_rejected_before_read(self):
        fs = FakeFiles()
        fs.nodes[('native',)][0] = replace(fs.nodes[('native',)][0], attributes=0x410)
        rows = self.run_projection(fs)['Files']
        self.assertEqual([x['ReadStatus'] for x in rows[2:]], ['Rejected', 'Rejected'])
        self.assertEqual(len(fs.reads), 2)

    def test_leaf_reparse_offline_device_directory_or_hardlink_rejected(self):
        for change in ({'attributes': 0x480}, {'attributes': 0x1080}, {'attributes': 0xc0}, {'attributes': 0x10}, {'links': 2}, {'volume': 99}, {'index': 0}):
            fs = FakeFiles()
            parts = w.projection.FILES[0][1]
            fs.nodes[parts][0] = replace(fs.nodes[parts][0], **change)
            self.assertEqual(self.run_projection(fs)['Files'][0]['ReadStatus'], 'Rejected')
            self.assertEqual(len(fs.reads), 3)

    def test_directory_changed_before_read_zero_reads(self):
        fs = FakeFiles()
        def mutate(event, handle):
            if event == 'open':
                fs.nodes[()][0] = replace(fs.nodes[()][0], written=2)
        fs.hook = mutate
        rows = self.run_projection(fs)['Files']
        self.assertTrue(all(x['ReadStatus'] == 'Rejected' for x in rows))
        self.assertEqual(fs.reads, [])

    def test_file_changed_after_read_discards_facts(self):
        fs = FakeFiles()
        parts = w.projection.FILES[0][1]
        fs.file(parts, b'cmake version 3.31.6')
        def mutate(event, handle):
            if event == 'read' and fs.handles[handle] == parts:
                fs.nodes[parts][0] = replace(fs.nodes[parts][0], written=2)
        fs.hook = mutate
        row = self.run_projection(fs)['Files'][0]
        self.assertEqual(row['ReadStatus'], 'Rejected')
        self.assertIsNone(row['Facts']['CMakeVersion'])

    def test_four_file_total_bound_and_truncated_facts_empty(self):
        fs = FakeFiles()
        for _, parts in w.projection.FILES:
            fs.file(parts, b'a' * (w.projection.LIMIT + 10))
        rows = self.run_projection(fs)['Files']
        self.assertEqual(sum(fs.reads), 4 * w.projection.LIMIT)
        self.assertTrue(all(x['ReadStatus'] == 'Truncated' and x['Facts'] == w.projection._facts() for x in rows))

    def test_missing_files_no_retry_no_leak(self):
        fs = FakeFiles()
        for _, parts in w.projection.FILES:
            del fs.nodes[parts]
        self.assertTrue(all(x['ReadStatus'] == 'Missing' for x in self.run_projection(fs)['Files']))
        self.assertEqual(fs.reads, [])

    def test_short_read_rejected(self):
        fs = FakeFiles()
        fs.read = lambda handle, amount: b''
        self.assertTrue(all(x['ReadStatus'] == 'Rejected' for x in self.run_projection(fs)['Files']))

    def test_metadata_failure_zero_reads_on_unknown_file(self):
        fs = FakeFiles()
        def fail(event, handle):
            if event == 'info' and handle != 1000:
                raise RuntimeError(SECRET)
        fs.hook = fail
        self.assertTrue(all(x['ReadStatus'] == 'Rejected' for x in self.run_projection(fs)['Files']))
        self.assertEqual(fs.reads, [])

    def test_failure_does_not_mutate_original_receipt(self):
        original = {'Status': 'Fail', 'Reason': 'OperationTimeout', 'CleanupConfirmed': True}
        saved = original.copy()
        fs = FakeFiles()
        fs.open_child = lambda *args: (_ for _ in ()).throw(RuntimeError(SECRET))
        self.run_projection(fs, cleanup=original['CleanupConfirmed'])
        self.assertEqual(original, saved)

    def test_policy_relative_no_create_no_write_no_delete_no_reparse(self):
        for directory in (True, False):
            access, share, disposition, options, attrs = w._policy('native', directory)
            self.assertEqual(share, 1)
            self.assertEqual(disposition, 1)
            self.assertEqual(options & 0x200020, 0x200020)
            self.assertEqual(attrs, 0x1040)
            self.assertEqual(access, 0x1000a0 if directory else 0x100081)
        for name in ('../x', 'a/b', 'a\\b', 'a:stream', '', '.', '..', '\x00', 'x'*65):
            with self.assertRaises(w.Rejected):
                w._policy(name, False)

    def test_fixed_windows_abi_layout_without_native_calls(self):
        self.assertEqual(ctypes.sizeof(w.HandleInfo), 52)
        self.assertEqual(ctypes.sizeof(w.IoStatus), 2 * ctypes.sizeof(ctypes.c_void_p))
        if ctypes.sizeof(ctypes.c_void_p) == 8:
            self.assertEqual(ctypes.sizeof(w.ObjectAttributes), 48)
            self.assertEqual(ctypes.sizeof(w.UnicodeString), 16)

    def test_native_unavailable_host_never_loads_dll(self):
        with patch.object(w.os, 'name', 'posix'):
            self.assertEqual(w.project_windows_after_cleanup(PROOF, cleanup_confirmed=True)['DiagnosticStatus'], 'ReaderUnavailable')
            with self.assertRaises(w.Rejected):
                w.NativeWindowsFiles()

    def test_native_bindings_using_fake_dll_no_win32_calls(self):
        calls = []
        class Function:
            def __init__(self, name):
                self.name = name
            def __call__(self, *args):
                calls.append((self.name, args))
                if self.name == 'DuplicateHandle':
                    ctypes.cast(args[3], ctypes.POINTER(w.HANDLE))[0] = w.HANDLE(1000)
                elif self.name == 'GetFileType':
                    return 1
                elif self.name == 'GetFileInformationByHandle':
                    value = ctypes.cast(args[1], ctypes.POINTER(w.HandleInfo)).contents
                    value.volume, value.indexLow, value.attributes, value.links = 5, 6, 0x10, 1
                elif self.name == 'NtQueryVolumeInformationFile':
                    ctypes.cast(args[2], ctypes.POINTER(w.U32))[0] = 7
                    ctypes.cast(args[2], ctypes.POINTER(w.U32))[1] = 0x20
                    ctypes.cast(args[1], ctypes.POINTER(w.IoStatus)).contents.Information = 8
                    return 0
                elif self.name == 'GetVolumeInformationByHandleW':
                    args[6].value = 'NTFS'
                elif self.name == 'NtCreateFile':
                    obj = ctypes.cast(args[2], ctypes.POINTER(w.ObjectAttributes)).contents
                    text = ctypes.string_at(obj.ObjectName.contents.Buffer, obj.ObjectName.contents.Length).decode('utf-16-le')
                    self_outer.assertEqual(text, 'CMakeCache.txt')
                    self_outer.assertEqual(obj.RootDirectory, 1000)
                    self_outer.assertEqual(obj.Attributes, 0x1040)
                    self_outer.assertEqual((args[1], args[6], args[7], args[8]), (0x100081, 1, 1, 0x200060))
                    ctypes.cast(args[0], ctypes.POINTER(w.HANDLE))[0] = w.HANDLE(1001)
                    ctypes.cast(args[3], ctypes.POINTER(w.IoStatus)).contents.Information = 1
                    return 0
                elif self.name == 'ReadFile':
                    ctypes.memmove(args[1], b'abc', 3)
                    ctypes.cast(args[3], ctypes.POINTER(w.U32))[0] = 3
                return 1
        class Dll:
            def __getattr__(self, name):
                function = Function(name)
                setattr(self, name, function)
                return function
        self_outer = self
        with patch.object(w.os, 'name', 'nt'), patch.object(w.c, 'WinDLL', side_effect=lambda *a, **k: Dll(), create=True):
            fs = w.NativeWindowsFiles()
            self.assertEqual(fs.duplicate(900), 1000)
            self.assertEqual(fs.info(1000).index, 6)
            self.assertTrue(fs.local_ntfs(1000))
            self.assertEqual(fs.open_child(1000, 'CMakeCache.txt', False), 1001)
            self.assertEqual(fs.read(1001, 3), b'abc')
            fs.close(1001)
            self.assertEqual(fs.n.NtCreateFile.restype, ctypes.c_int32)
            self.assertEqual(len(fs.n.NtCreateFile.argtypes), 11)
        self.assertEqual(len([x for x in calls if x[0] == 'NtCreateFile']), 1)

    def test_close_failure_discards_result_and_closes_other_handles(self):
        fs = FakeFiles()
        real = fs.close
        def fail_root(handle):
            real(handle)
            if handle == 1000:
                raise RuntimeError(SECRET)
        fs.close = fail_root
        self.assertEqual(self.run_projection(fs), {'DiagnosticStatus': 'SafetyUnknown', 'Files': []})

    def test_no_existing_critical_path_integration(self):
        for name in ('tools/ci/LifecycleOperation.cs', 'tests/scripts/run-lifecycle-windows-ci.ps1', '.github/workflows/windows-lifecycle-validation.yml'):
            self.assertNotIn('lifecycle_configure_windows', (ROOT/name).read_text())
        text = (ROOT/'tools/ci/lifecycle_configure_windows.py').read_text()
        for forbidden in ('OpenProcess(', 'QueryInformationJobObject(', 'TerminateProcess(', 'subprocess'):
            self.assertNotIn(forbidden, text)


if __name__ == '__main__':
    unittest.main(verbosity=2)
