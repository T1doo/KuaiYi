"""One Windows-only public file fixture. No CMake, lifecycle tools, services or scans."""
import ctypes as c
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import sys
import tempfile

sys.dont_write_bytecode = True
REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools/ci'))
import lifecycle_configure_windows as w

CHECKS = ('CleanupGate', 'RootIdentity', 'FourFacts', 'ReadBounds', 'WriteShareConflict',
          'HeldDirectoryRename', 'MetadataChangeRejected', 'ReparseRejected',
          'CallerHandleAlive', 'AdapterHandlesClosed', 'FixtureRemoved')


class FixtureFailure(Exception):
    pass


class ReparseBlocked(Exception):
    pass


class CountedFiles(w.NativeWindowsFiles):
    def __init__(self):
        super().__init__()
        self.owned, self.fixture_handles, self.names, self.reads, self.hook = set(), set(), {}, [], None

    def duplicate(self, handle):
        value = super().duplicate(handle)
        self.owned.add(value)
        return value

    def open_child(self, parent, name, directory):
        value = super().open_child(parent, name, directory)
        self.owned.add(value)
        self.names[value] = name
        return value

    def close(self, handle):
        super().close(handle)
        self.owned.discard(handle)
        self.fixture_handles.discard(handle)
        self.names.pop(handle, None)

    def read(self, handle, count):
        self.reads.append(count)
        if self.hook is not None:
            self.hook(handle)
        return super().read(handle, count)


def native_open(fs, parent, name, *, access, sharing, disposition, options, attributes=0):
    # Fixture setup only: parent=None is used once for the freshly created private
    # container. All owned fixture directories are then created handle-relative.
    encoded = name.encode('utf-16-le')
    buf = c.create_string_buffer(encoded + b'\0\0')
    text = w.UnicodeString(len(encoded), len(encoded)+2, c.cast(buf, c.c_void_p))
    obj = w.ObjectAttributes(c.sizeof(w.ObjectAttributes), w.HANDLE(parent), c.pointer(text), 0x1040, None, None)
    handle, ios = w.HANDLE(), w.IoStatus()
    status = fs.n.NtCreateFile(c.byref(handle), access, c.byref(obj), c.byref(ios), None,
                               attributes, sharing, disposition, options, None, 0)
    if handle.value is not None:
        fs.fixture_handles.add(handle.value)
    return status & 0xffffffff, handle.value, ios.Information


def close_if(fs, handle):
    if handle is not None:
        fs.close(handle)


def junction_buffer(target):
    substitute = ('\\??\\' + target).encode('utf-16-le')
    display = target.encode('utf-16-le')
    payload = struct.pack('<HHHH', 0, len(substitute), len(substitute)+2, len(display)) + substitute + b'\0\0' + display + b'\0\0'
    return struct.pack('<IHH', 0xa0000003, len(payload), 0) + payload


class FileTime(c.Structure):
    _fields_ = [('low', w.U32), ('high', w.U32)]


def change_owned_directory_time(fs, root_handle):
    handle = None
    try:
        status, handle, opened = native_open(fs, root_handle, 'native', access=0x100180,
                                            sharing=7, disposition=1, options=0x200020)
        if status != 0 or handle is None or opened != 1:
            raise FixtureFailure()
        before = fs.info(handle)
        w._validate(before, True)
        written = before.written + 20_000_000
        time = FileTime(written & 0xffffffff, written >> 32)
        setter = fs.k.SetFileTime
        setter.argtypes = [w.HANDLE, c.c_void_p, c.c_void_p, c.POINTER(FileTime)]
        setter.restype = c.c_int32
        if not setter(w.HANDLE(handle), None, None, c.byref(time)):
            raise FixtureFailure()
        if fs.info(handle).written != written:
            raise FixtureFailure()
    finally:
        close_if(fs, handle)


def check_junction_open(status, handle, opened):
    if status in (0xc0000022, 0xc0000061):
        raise ReparseBlocked()
    if status != 0 or handle is None or opened != 1:
        raise FixtureFailure()


def set_mountpoint(fs, handle, raw):
    ioctl = fs.k.DeviceIoControl
    ioctl.argtypes = [w.HANDLE, w.U32, c.c_void_p, w.U32, c.c_void_p, w.U32, c.POINTER(w.U32), c.c_void_p]
    ioctl.restype = c.c_int32
    data, returned = c.create_string_buffer(raw), w.U32()
    if not ioctl(w.HANDLE(handle), 0x900a4, data, len(raw), None, 0, c.byref(returned), None):
        error = c.get_last_error()  # capture immediately, before any other native call
        if error in (5, 1314):
            raise ReparseBlocked()
        raise FixtureFailure()


def cleanup_fixture(fs, root, parent, container):
    ok, removed = True, False
    handles = []
    if fs is not None:
        fs.hook = None
        handles.extend(list(fs.owned))
        handles.extend(list(fs.fixture_handles))
    handles.extend(h for h in (root, parent) if h is not None)
    for handle in dict.fromkeys(handles):
        try:
            fs.close(handle)
        except Exception:
            ok = False
    if container is not None:
        try:
            shutil.rmtree(container)
        except Exception:
            ok = False
        try:
            removed = not container.exists()
        except Exception:
            ok = False
        if not removed:
            ok = False
    return ok, removed


def create_junction(fs, root_handle, root_path):
    native = root_path / 'native'
    saved = root_path / 'native-saved'
    native.rename(saved)
    native.mkdir()
    link_handle = None
    try:
        status, link_handle, opened = native_open(fs, root_handle, 'native', access=0x40100080,
                                            sharing=7, disposition=1, options=0x200020)
        check_junction_open(status, link_handle, opened)
        target = str(saved)
        raw = junction_buffer(target)
        set_mountpoint(fs, link_handle, raw)
        if not fs.info(link_handle).attributes & 0x400:
            raise FixtureFailure()
    finally:
        close_if(fs, link_handle)
    return native, saved


def main():
    summary = dict(Kind='ConfigureFilesWindowsFixture', SourceCommit=None,
                   ReviewedParent='bf0330cd80abe79a201762f47a9db92a0135f3c7',
                   Scope='OwnedTemporaryFilesOnly', Status='Fail', Stage='Prerequisites',
                   Checks={name: 'NotRun' for name in CHECKS},
                   Windows='NotRun', CMake='NotRun', FourCases='NotRun',
                   OriginalLifecycle='NotRun', ModelCalls=0, SecurityChanges=0, FixtureCleanupConfirmed=False, CleanupFailed=False)
    fs, parent, root, container, root_path = None, None, None, None, None
    active = None
    def check(name, condition):
        summary['Stage'] = name
        summary['Checks'][name] = 'Pass' if condition else 'Fail'
        if not condition:
            raise FixtureFailure()
    try:
        candidate = os.environ.get('GITHUB_SHA', '')
        if not re.fullmatch('[0-9a-f]{40}', candidate):
            summary['Status'] = 'Blocked'
            return summary
        summary['SourceCommit'] = candidate
        if sys.version_info < (3, 8) or os.name != 'nt' or os.environ.get('CI') != 'true' or c.sizeof(c.c_void_p) != 8:
            summary['Status'] = 'Blocked'
            return summary
        expected = {'lifecycle_configure_windows.py': 'c67d69f0c85c367fad53a355939e4b4c408ec003132eb5a625bb27cadaf14b24',
                    'lifecycle_configure_projection.py': '892d7ea922f48dff110070a042471b71c592f9ff8994e0270ebc3cc8d5c4a254'}
        if any(hashlib.sha256((REPO/'tools/ci'/name).read_bytes()).hexdigest() != value for name, value in expected.items()):
            raise FixtureFailure()
        summary['Stage'] = 'InitializeFileApi'
        fs = CountedFiles()
        summary['Windows'] = 'Executed'
        container = Path(tempfile.mkdtemp(prefix='kuaiyi-files-public-')).absolute()
        if not re.fullmatch(r'[A-Za-z]:\\[^:]+', str(container)):
            raise FixtureFailure()
        summary['Stage'] = 'BindPrivateContainer'
        status, parent, opened = native_open(fs, None, '\\??\\'+str(container), access=0x1000a0,
                                            sharing=7, disposition=1, options=0x200020)
        if status != 0 or parent is None or opened != 1 or not fs.local_ntfs(parent):
            raise FixtureFailure()
        parent_info = fs.info(parent)
        w._validate(parent_info, True)
        summary['Stage'] = 'CreateOwnedRoot'
        status, root, created = native_open(fs, parent, 'owned-root', access=0x1000a0,
                                          sharing=7, disposition=2, options=0x21, attributes=0x10)
        if status != 0 or root is None or created != 2:
            raise FixtureFailure()
        info = fs.info(root)
        proof = w.RootProof(root, info.volume, info.index)
        root_path = container/'owned-root'
        (root_path/'native/CMakeFiles').mkdir(parents=True)
        data = (
            b'cmake version 3.31.6\n',
            b'-- Configuring done\n-- Generating done\n',
            b'CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022\n',
            b'events:\n  -\n    kind: "try_compile-v1"\n')
        # Every writer is opened/closed synchronously before CleanupConfirmed=True.
        summary['Stage'] = 'WritePublicFixture'
        for (_, parts), content in zip(w.projection.FILES, data):
            root_path.joinpath(*parts).write_bytes(content)
        before = len(fs.reads)
        skipped = w.project_windows_after_cleanup(proof, cleanup_confirmed=False, filesystem=fs)
        check('CleanupGate', skipped['DiagnosticStatus'] == 'SkippedCleanupUnconfirmed' and len(fs.reads) == before)
        before = len(fs.reads)
        bad = w.project_windows_after_cleanup(w.RootProof(root, info.volume, info.index+1), cleanup_confirmed=True, filesystem=fs)
        check('RootIdentity', bad['DiagnosticStatus'] == 'SafetyUnknown' and len(fs.reads) == before)
        result = w.project_windows_after_cleanup(proof, cleanup_confirmed=True, filesystem=fs)
        rows = result['Files']
        check('FourFacts', len(rows) == 4 and all(x['ReadStatus'] == 'Projected' for x in rows)
              and rows[0]['Facts']['CMakeVersion'] == '3.31.6' and rows[1]['Facts']['GenerateDone']
              and rows[2]['Facts']['VisualStudio2022'] and rows[3]['Facts']['TryCompileEvent'])
        for _, parts in w.projection.FILES:
            root_path.joinpath(*parts).write_bytes(b'a'*(w.projection.LIMIT+1))
        before = len(fs.reads)
        result = w.project_windows_after_cleanup(proof, cleanup_confirmed=True, filesystem=fs)
        check('ReadBounds', len(result['Files']) == 4 and all(x['ReadStatus'] == 'Truncated' and x['Facts'] == w.projection._facts() for x in result['Files'])
              and len(fs.reads[before:]) == 4 and sum(fs.reads[before:]) == 4*w.projection.LIMIT)
        # Real simultaneous write access must conflict with adapter's read-only share.
        summary['Stage'] = 'WriteShareConflict'
        share_result = []
        def sharing_hook(handle):
            if fs.names.get(handle) == 'NativeConfigure.local.log' and not share_result:
                status, writer, _ = native_open(fs, root, 'NativeConfigure.local.log', access=0x100002,
                                               sharing=7, disposition=1, options=0x200060)
                close_if(fs, writer)
                share_result.append(status == 0xc0000043)
        fs.hook = sharing_hook
        w.project_windows_after_cleanup(proof, cleanup_confirmed=True, filesystem=fs)
        fs.hook = None
        check('WriteShareConflict', share_result == [True])
        summary['Stage'] = 'HeldDirectoryRename'
        renamed = []
        def rename_hook(handle):
            if fs.names.get(handle) == 'CMakeCache.txt' and not renamed:
                try:
                    (root_path/'native').rename(root_path/'native-moved')
                except OSError as error:
                    renamed.append(error.winerror in (5, 32))
                else:
                    (root_path/'native-moved').rename(root_path/'native')
                    renamed.append(False)
        fs.hook = rename_hook
        w.project_windows_after_cleanup(proof, cleanup_confirmed=True, filesystem=fs)
        fs.hook = None
        check('HeldDirectoryRename', renamed == [True])
        summary['Stage'] = 'MetadataChangeRejected'
        changed = []
        def change_hook(handle):
            if fs.names.get(handle) == 'CMakeCache.txt' and not changed:
                change_owned_directory_time(fs, root)
                changed.append(True)
        fs.hook = change_hook
        result = w.project_windows_after_cleanup(proof, cleanup_confirmed=True, filesystem=fs)
        fs.hook = None
        check('MetadataChangeRejected', changed == [True] and result['Files'][2]['ReadStatus'] == 'Rejected')
        summary['Stage'] = 'ReparseRejected'
        active = 'ReparseRejected'
        native, saved = create_junction(fs, root, root_path)
        before = len(fs.reads)
        result = w.project_windows_after_cleanup(proof, cleanup_confirmed=True, filesystem=fs)
        check('ReparseRejected', len(result['Files']) == 4 and all(x['ReadStatus'] == 'Rejected' for x in result['Files'][2:]) and len(fs.reads)-before == 2)
        # Remove only the owned junction entry; target is also within this fixture.
        native.rmdir()
        saved.rename(native)
        active = None
        current = fs.info(root)
        check('CallerHandleAlive', (current.volume, current.index) == (proof.volume, proof.index))
        check('AdapterHandlesClosed', not fs.owned and fs.fixture_handles == {root, parent})
        summary['Status'] = 'Pass'
    except ReparseBlocked:
        summary['Status'] = 'Blocked'
        summary['Checks']['ReparseRejected'] = 'Blocked'
    except Exception:
        summary['Status'] = 'Fail'
        failed_check = active if active is not None else summary['Stage']
        if failed_check in summary['Checks'] and summary['Checks'][failed_check] == 'NotRun':
            summary['Checks'][failed_check] = 'Fail'
    finally:
        ok, removed = cleanup_fixture(fs, root, parent, container)
        summary['CleanupFailed'] = not ok
        summary['FixtureCleanupConfirmed'] = container is not None and ok and removed
        if container is not None:
            summary['Checks']['FixtureRemoved'] = 'Pass' if removed else 'Fail'
        if not ok:
            summary['Status'] = 'Fail'  # preserve the original Stage, separately report cleanup
        if summary['Status'] == 'Pass':
            summary['Stage'] = 'Completed'
    return summary


if __name__ == '__main__':
    receipt = main()
    print('KUAIYI_CONFIGURE_FILES '+json.dumps(receipt, separators=(',', ':')))
    sys.exit(0 if receipt['Status'] == 'Pass' else 2 if receipt['Status'] == 'Blocked' else 1)
