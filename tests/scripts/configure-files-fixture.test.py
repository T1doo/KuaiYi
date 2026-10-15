"""Offline fixture construction/trigger/error contracts; no real Windows calls."""
import ctypes
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch
sys.dont_write_bytecode = True
REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('file_fixture', REPO/'tests/scripts/run-configure-files-windows.py')
f = importlib.util.module_from_spec(spec)
spec.loader.exec_module(f)


class FixtureContracts(unittest.TestCase):
    def test_native_open_relative_root_creation_marshalling_no_windows(self):
        class Api:
            def NtCreateFile(self, output, access, obj, ios, allocation, attrs, share, disposition, options, ea, length):
                value = ctypes.cast(obj, ctypes.POINTER(f.w.ObjectAttributes)).contents
                self_outer.assertEqual(value.RootDirectory, 17)
                self_outer.assertEqual(value.Attributes, 0x1040)
                name = ctypes.string_at(value.ObjectName.contents.Buffer, value.ObjectName.contents.Length).decode('utf-16-le')
                self_outer.assertEqual(name, 'owned-root')
                self_outer.assertEqual((access, attrs, share, disposition, options), (0x1000a0, 0x10, 7, 2, 0x21))
                ctypes.cast(output, ctypes.POINTER(f.w.HANDLE))[0] = f.w.HANDLE(23)
                ctypes.cast(ios, ctypes.POINTER(f.w.IoStatus)).contents.Information = 2
                return 0
        class Fs:
            n = Api()
            fixture_handles = set()
        self_outer = self
        self.assertEqual(f.native_open(Fs(), 17, 'owned-root', access=0x1000a0, attributes=0x10, sharing=7, disposition=2, options=0x21), (0, 23, 2))
        self.assertEqual(Fs.fixture_handles, {23})

    def test_junction_buffer_tag_lengths_and_unicode_offsets(self):
        target = 'C:\\owned\\public-target'
        raw = f.junction_buffer(target)
        tag, length, reserved = struct.unpack_from('<IHH', raw)
        self.assertEqual((tag, reserved), (0xa0000003, 0))
        self.assertEqual(length, len(raw)-8)
        start, sublen, display, displaylen = struct.unpack_from('<HHHH', raw, 8)
        self.assertEqual(start, 0)
        self.assertEqual(raw[16:16+sublen].decode('utf-16-le'), '\\??\\'+target)
        self.assertEqual(raw[16+display:16+display+displaylen].decode('utf-16-le'), target)
        self.assertEqual(raw[16+sublen:18+sublen], b'\0\0')
        self.assertLess(len(raw), 16384)

    def test_non_windows_or_non_ci_gate_zero_native_calls(self):
        with patch.object(f.os, 'name', 'posix'), patch.object(f, 'CountedFiles', side_effect=AssertionError('native must not run')):
            result = f.main()
        self.assertEqual(result['Status'], 'Blocked')
        self.assertTrue(all(x == 'NotRun' for x in result['Checks'].values()))
        self.assertEqual(result['CMake'], 'NotRun')
        self.assertEqual(result['FourCases'], 'NotRun')

    def test_unsupported_python_blocks_before_native_setup(self):
        with patch.object(f.sys, 'version_info', (3, 7)), patch.object(f.os, 'name', 'nt'), patch.dict(os.environ, {'CI':'true','GITHUB_SHA':'1'*40}), patch.object(f, 'CountedFiles', side_effect=AssertionError('must not run')):
            self.assertEqual(f.main()['Status'], 'Blocked')

    def test_exception_is_fixed_summary_not_exception_or_path(self):
        with patch.object(f.os, 'name', 'nt'), patch.dict(os.environ, {'CI':'true','GITHUB_SHA':'1'*40}), patch.object(f, 'CountedFiles', side_effect=RuntimeError('PRIVATE_SENTINEL C:/private --secret')):
            result = f.main()
        self.assertEqual(result['Status'], 'Fail')
        self.assertEqual(result['Stage'], 'InitializeFileApi')
        self.assertNotIn('PRIVATE_SENTINEL', json.dumps(result))
        self.assertNotIn('private', json.dumps(result))

    def test_invalid_sha_never_copied_or_native_called(self):
        with patch.dict(os.environ, {'CI':'true','GITHUB_SHA':'PRIVATE_SENTINEL --key=C:/private'}), patch.object(f, 'CountedFiles', side_effect=AssertionError('must not run')):
            result = f.main()
        self.assertEqual(result['Status'], 'Blocked')
        self.assertIsNone(result['SourceCommit'])
        self.assertNotIn('PRIVATE_SENTINEL', json.dumps(result))

    def test_only_permission_setup_statuses_are_blocked(self):
        for status in (0xc0000022, 0xc0000061):
            with self.assertRaises(f.ReparseBlocked):
                f.check_junction_open(status, None, 0)
        for status in (0xc000000d, 0xc0000043, 0x103):
            with self.assertRaises(f.FixtureFailure):
                f.check_junction_open(status, None, 0)
        with self.assertRaises(f.FixtureFailure):
            f.check_junction_open(0, 33, 2)

    def test_ioctl_errors_captured_and_classified_without_win32(self):
        class Function:
            def __call__(self, *args):
                return 0
        class Kernel:
            DeviceIoControl = Function()
        class Fs:
            k = Kernel()
        for error, expected in ((5, f.ReparseBlocked), (1314, f.ReparseBlocked), (87, f.FixtureFailure), (50, f.FixtureFailure)):
            with patch.object(f.c, 'get_last_error', return_value=error, create=True) as get_error:
                with self.assertRaises(expected):
                    f.set_mountpoint(Fs(), 33, b'public-buffer')
                get_error.assert_called_once_with()

    def test_shared_metadata_setter_no_zero_share_utime(self):
        opened, closed = [], []
        written = 100
        class Setter:
            def __call__(self, handle, created, access, time):
                nonlocal written
                value = ctypes.cast(time, ctypes.POINTER(f.FileTime)).contents
                written = (value.high << 32) | value.low
                return 1
        class Kernel:
            SetFileTime = Setter()
        class Fs:
            k = Kernel()
            def info(self, handle):
                return f.w.FileInfo(5, 6, 0x10, 0, 1, 1, written)
            def close(self, handle):
                closed.append(handle)
        def opening(fs, root, name, **kwargs):
            opened.append((root, name, kwargs))
            return 0, 33, 1
        with patch.object(f, 'native_open', side_effect=opening):
            f.change_owned_directory_time(Fs(), 17)
        self.assertEqual(written, 20_000_100)
        self.assertEqual(closed, [33])
        self.assertEqual(opened[0], (17, 'native', {'access':0x100180,'sharing':7,'disposition':1,'options':0x200020}))
        self.assertNotIn('os.utime', (REPO/'tests/scripts/run-configure-files-windows.py').read_text())

    def test_cleanup_attempts_all_handles_and_directory_after_failure(self):
        closed, deleted = [], []
        class Container:
            def exists(self):
                return False
        class Fs:
            owned = {33, 34}
            fixture_handles = {35}
            hook = None
            def close(self, handle):
                closed.append(handle)
                if handle == 33:
                    raise RuntimeError('PRIVATE_SENTINEL')
        container = Container()
        with patch.object(f.shutil, 'rmtree', side_effect=lambda p: deleted.append(p)):
            self.assertEqual(f.cleanup_fixture(Fs(), 17, 18, container), (False, True))
        self.assertEqual(set(closed), {33, 34, 35, 17, 18})
        self.assertEqual(deleted, [container])

    def test_temporary_close_failure_remains_registered_for_final_cleanup(self):
        attempted = []
        class Fs:
            owned = set()
            fixture_handles = {33}
            hook = None
            def close(self, handle):
                attempted.append(handle)
                raise RuntimeError('PRIVATE_SENTINEL')
        fs = Fs()
        with self.assertRaises(RuntimeError):
            f.close_if(fs, 33)
        self.assertEqual(fs.fixture_handles, {33})
        self.assertEqual(f.cleanup_fixture(fs, None, None, None), (False, False))
        self.assertEqual(attempted, [33, 33])
        self.assertEqual(fs.fixture_handles, {33})

    def test_setup_error_with_returned_handle_still_registered(self):
        class Api:
            def NtCreateFile(self, output, access, obj, ios, *args):
                ctypes.cast(output, ctypes.POINTER(f.w.HANDLE))[0] = f.w.HANDLE(41)
                return -1073741790  # error status; any nonempty handle must be tracked
        class Fs:
            n = Api()
            fixture_handles = set()
        fs = Fs()
        result = f.native_open(fs, 17, 'native', access=0x1000a0, sharing=7, disposition=1, options=0x200020)
        self.assertNotEqual(result[0], 0)
        self.assertEqual(fs.fixture_handles, {41})

    def test_cleanup_failure_preserves_original_failure_stage(self):
        with patch.object(f.os, 'name', 'nt'), patch.dict(os.environ, {'CI':'true','GITHUB_SHA':'1'*40}), patch.object(f, 'CountedFiles', side_effect=RuntimeError('PRIVATE_SENTINEL')), patch.object(f, 'cleanup_fixture', return_value=(False, False)):
            result = f.main()
        self.assertEqual(result['Status'], 'Fail')
        self.assertEqual(result['Stage'], 'InitializeFileApi')
        self.assertTrue(result['CleanupFailed'])
        self.assertFalse(result['FixtureCleanupConfirmed'])

    def test_windows_adapter_bytes_preserved_by_exact_gitattributes(self):
        attributes = (REPO/'.gitattributes').read_text()
        self.assertIn('tools/ci/lifecycle_configure_projection.py -text', attributes)
        self.assertIn('tools/ci/lifecycle_configure_windows.py -text', attributes)

    def test_workflow_isolation_and_bounds(self):
        workflow = (REPO/'.github/workflows/windows-configure-files.yml').read_text()
        self.assertIn('branches: [dev/configure-files-fixture]', workflow)
        self.assertIn('paths: [.github/workflows/windows-configure-files.yml]', workflow)
        self.assertIn('timeout-minutes: 5', workflow)
        self.assertIn('runs-on: windows-2022', workflow)
        self.assertIn('persist-credentials: false', workflow)
        self.assertIn('ref: ${{ github.sha }}', workflow)
        self.assertIn('fetch-depth: 2', workflow)
        self.assertIn('bf0330cd80abe79a201762f47a9db92a0135f3c7', workflow)
        for forbidden in ('workflow_dispatch', 'rerun', 'upload-artifact', 'setup-python', 'run-lifecycle-windows-ci'):
            self.assertNotIn(forbidden, workflow)
        # Existing workflows' branch filters cannot match new branch; unchanged blobs
        # are separately checked against the actual reviewed remote tree.
        for path in (REPO/'.github/workflows').glob('*.yml'):
            if path.name not in ('windows-configure-files.yml', 'windows-configure-bridge.yml'):
                self.assertIn('branches: [dev/p0-com-selection-diagnostic]', path.read_text())
                self.assertNotIn('dev/configure-files-fixture', path.read_text())

    def test_no_scope_expansion(self):
        source = (REPO/'tests/scripts/run-configure-files-windows.py').read_text()
        for forbidden in ('subprocess', 'OpenProcess(', 'TerminateProcess(', 'AdjustTokenPrivileges', 'os.system(', 'StartService'):
            self.assertNotIn(forbidden, source)
        self.assertEqual(set(f.CHECKS), {'CleanupGate','RootIdentity','FourFacts','ReadBounds','WriteShareConflict','HeldDirectoryRename','MetadataChangeRejected','ReparseRejected','CallerHandleAlive','AdapterHandlesClosed','FixtureRemoved'})


if __name__ == '__main__':
    unittest.main(verbosity=2)
