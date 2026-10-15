"""Isolated Windows file adapter; no lifecycle/runner/workflow integration.

Requires a caller-owned, continuously held root handle and original identity.
Only invoke in a separate post-operation/post-rescue diagnostic step. Synchronous
file APIs have no hard wall-clock guarantee. Native calls are not run by offline tests.
"""
import ctypes as c
from dataclasses import dataclass
import os

try:
    from . import lifecycle_configure_projection as projection
except ImportError:
    import lifecycle_configure_projection as projection


@dataclass(frozen=True)
class RootProof:
    handle: int
    volume: int
    index: int


@dataclass(frozen=True)
class FileInfo:
    volume: int
    index: int
    attributes: int
    size: int
    links: int
    created: int
    written: int


class Rejected(Exception):
    pass


class Missing(Exception):
    pass


# Fixed-width Windows ABI declarations, including on non-Windows test hosts.
U32, U16, HANDLE, STATUS = c.c_uint32, c.c_uint16, c.c_void_p, c.c_int32


class UnicodeString(c.Structure):
    _fields_ = [('Length', U16), ('MaximumLength', U16), ('Buffer', c.c_void_p)]


class ObjectAttributes(c.Structure):
    _fields_ = [('Length', U32), ('RootDirectory', HANDLE),
                ('ObjectName', c.POINTER(UnicodeString)), ('Attributes', U32),
                ('SecurityDescriptor', c.c_void_p), ('SecurityQualityOfService', c.c_void_p)]


class IoStatus(c.Structure):
    # NTSTATUS/Pointer union occupies pointer-width; unsigned value never published.
    _fields_ = [('StatusOrPointer', c.c_void_p), ('Information', c.c_size_t)]


class HandleInfo(c.Structure):
    _fields_ = [(name, U32) for name in (
        'attributes', 'createLow', 'createHigh', 'accessLow', 'accessHigh',
        'writeLow', 'writeHigh', 'volume', 'sizeHigh', 'sizeLow', 'links', 'indexHigh', 'indexLow')]


def _policy(name, directory):
    if not isinstance(name, str) or not name or len(name) > 64 or any(ch in name for ch in '\\/:\x00') or name in ('.', '..'):
        raise Rejected()
    # FILE_READ_ATTRIBUTES | SYNCHRONIZE, plus FILE_READ_DATA for leaves.
    access = 0x100080 | (0x20 if directory else 1)
    # FILE_OPEN_REPARSE_POINT | FILE_SYNCHRONOUS_IO_NONALERT; no create/write/delete.
    options = 0x200020 | (0 if directory else 0x40)
    return access, 1, 1, options, 0x1040  # share READ, FILE_OPEN, OBJ_DONT_REPARSE|CASE_INSENSITIVE


class NativeWindowsFiles:
    """Real file-only bindings. Construction is explicit and Windows-only."""
    def __init__(self):
        if os.name != 'nt':
            raise Rejected()
        self.k = c.WinDLL('kernel32', use_last_error=True)
        self.n = c.WinDLL('ntdll', use_last_error=True)
        bindings = (
            (self.k.DuplicateHandle, [HANDLE, HANDLE, HANDLE, c.POINTER(HANDLE), U32, c.c_int32, U32], c.c_int32),
            (self.k.CloseHandle, [HANDLE], c.c_int32),
            (self.k.GetFileType, [HANDLE], U32),
            (self.k.GetFileInformationByHandle, [HANDLE, c.POINTER(HandleInfo)], c.c_int32),
            (self.k.ReadFile, [HANDLE, c.c_void_p, U32, c.POINTER(U32), c.c_void_p], c.c_int32),
            (self.k.GetVolumeInformationByHandleW, [HANDLE, c.c_void_p, U32, c.POINTER(U32), c.POINTER(U32), c.POINTER(U32), c.c_void_p, U32], c.c_int32),
            (self.n.NtCreateFile, [c.POINTER(HANDLE), U32, c.POINTER(ObjectAttributes), c.POINTER(IoStatus), c.c_void_p, U32, U32, U32, U32, c.c_void_p, U32], STATUS),
            (self.n.NtQueryVolumeInformationFile, [HANDLE, c.POINTER(IoStatus), c.c_void_p, U32, U32], STATUS),
        )
        for function, args, result in bindings:
            function.argtypes, function.restype = args, result

    def duplicate(self, handle):
        result = HANDLE()
        if not self.k.DuplicateHandle(HANDLE(-1), HANDLE(handle), HANDLE(-1), c.byref(result), 0, False, 2):
            raise Rejected()
        return result.value

    def close(self, handle):
        if not self.k.CloseHandle(HANDLE(handle)):
            raise Rejected()

    def info(self, handle):
        info = HandleInfo()
        if self.k.GetFileType(HANDLE(handle)) != 1:  # FILE_TYPE_DISK
            raise Rejected()
        if not self.k.GetFileInformationByHandle(HANDLE(handle), c.byref(info)):
            raise Rejected()
        return FileInfo(info.volume, (info.indexHigh << 32) | info.indexLow,
                        info.attributes, (info.sizeHigh << 32) | info.sizeLow,
                        info.links, (info.createHigh << 32) | info.createLow,
                        (info.writeHigh << 32) | info.writeLow)

    def local_ntfs(self, handle):
        device, ios = (U32 * 2)(), IoStatus()
        status = self.n.NtQueryVolumeInformationFile(HANDLE(handle), c.byref(ios), c.byref(device), 8, 4)
        if status != 0 or ios.Information != 8 or device[0] != 7 or device[1] & (0x10 | 0x1 | 0x4000):
            return False  # remote/removable/portable/unknown not supported
        filesystem = c.create_unicode_buffer(16)
        serial, maximum, flags = U32(), U32(), U32()
        if not self.k.GetVolumeInformationByHandleW(HANDLE(handle), None, 0, c.byref(serial), c.byref(maximum), c.byref(flags), filesystem, 16):
            return False
        return filesystem.value == 'NTFS'

    def open_child(self, parent, name, directory):
        access, sharing, disposition, options, attrs = _policy(name, directory)
        # Explicit UTF-16 storage: ctypes wchar is 32-bit on some offline hosts.
        encoded = name.encode('utf-16-le')
        buffer = c.create_string_buffer(encoded + b'\0\0')
        string = UnicodeString(len(encoded), len(encoded) + 2, c.cast(buffer, c.c_void_p))
        obj = ObjectAttributes(c.sizeof(ObjectAttributes), HANDLE(parent), c.pointer(string), attrs, None, None)
        result, ios = HANDLE(), IoStatus()
        status = self.n.NtCreateFile(c.byref(result), access, c.byref(obj), c.byref(ios), None, 0, sharing, disposition, options, None, 0)
        if status != 0:
            if result.value:
                self.close(result.value)
            # No path/error strings cross the interface; never retry reparse/sharing failures.
            if status & 0xffffffff in (0xc0000034, 0xc000003a):
                raise Missing()
            raise Rejected()
        if not result.value or ios.Information != 1:  # FILE_OPENED only
            if result.value:
                self.close(result.value)
            raise Rejected()
        return result.value

    def read(self, handle, count):
        if type(count) is not int or not 0 <= count <= projection.LIMIT:
            raise Rejected()
        if count == 0:
            return b''
        data, read = c.create_string_buffer(count), U32()
        if not self.k.ReadFile(HANDLE(handle), data, count, c.byref(read), None) or read.value != count:
            raise Rejected()  # short reads fail closed, no retry
        return data.raw[:count]


def _validate(info, directory, volume=None):
    if not isinstance(info, FileInfo) or any(type(v) is not int or v < 0 for v in vars(info).values()):
        raise Rejected()
    if not info.volume or not info.index or info.attributes & (0x400 | 0x1000 | 0x40):
        raise Rejected()  # reparse, offline, device
    if bool(info.attributes & 0x10) != directory or (volume is not None and info.volume != volume):
        raise Rejected()
    if info.links < 1 or (not directory and info.links != 1):
        raise Rejected()


def _one_file(fs, root, root_info, parts):
    held, snapshots = [], [(root, root_info, True)]
    try:
        parent = root
        for i, name in enumerate(parts):
            directory = i < len(parts) - 1
            child = fs.open_child(parent, name, directory)
            held.append(child)
            info = fs.info(child)
            _validate(info, directory, root_info.volume)
            snapshots.append((child, info, directory))
            parent = child
        # Validate every bound directory/leaf before content access, then again after.
        for handle, previous, directory in snapshots:
            current = fs.info(handle)
            _validate(current, directory, root_info.volume)
            if current != previous:
                raise Rejected()
        amount = min(info.size, projection.LIMIT)
        data = fs.read(parent, amount)
        if not isinstance(data, bytes) or len(data) != amount:
            raise Rejected()
        for handle, previous, directory in snapshots:
            current = fs.info(handle)
            _validate(current, directory, root_info.volume)
            if current != previous:
                raise Rejected()
        return data, info.size > projection.LIMIT
    finally:
        close_failed = False
        for handle in reversed(held):
            try:
                fs.close(handle)
            except Exception:
                close_failed = True
        if close_failed:
            raise Rejected()


def project_windows_after_cleanup(proof, *, cleanup_confirmed, filesystem=None):
    """Separate diagnostic result. `proof` must reference the caller's STILL HELD
    owned output directory handle, captured before running the original operation.
    A path or an identity recovered after closing that handle is not ownership proof.
    Caller is responsible for the proof's provenance; no ACL/ownership scan is made.
    Fake filesystem is only for isolated tests; native default runs only on Windows.
    """
    if cleanup_confirmed is not True:
        return dict(DiagnosticStatus='SkippedCleanupUnconfirmed', Files=[])
    if not isinstance(proof, RootProof) or any(type(v) is not int or v <= 0 for v in vars(proof).values()):
        return dict(DiagnosticStatus='OwnershipUnconfirmed', Files=[])
    if proof.handle >= (1 << (c.sizeof(HANDLE) * 8)) - 1 or proof.volume > 0xffffffff or proof.index > 0xffffffffffffffff:
        return dict(DiagnosticStatus='OwnershipUnconfirmed', Files=[])
    if filesystem is None and os.name != 'nt':
        return dict(DiagnosticStatus='ReaderUnavailable', Files=[])
    root, fs = None, filesystem
    result = dict(DiagnosticStatus='ProjectionError', Files=[])
    try:
        fs = fs if fs is not None else NativeWindowsFiles()
        root = fs.duplicate(proof.handle)
        info = fs.info(root)
        _validate(info, True)
        if (info.volume, info.index) != (proof.volume, proof.index) or fs.local_ntfs(root) is not True:
            raise Rejected()
        rows = []
        for kind, parts in projection.FILES:
            try:
                data, truncated = _one_file(fs, root, info, parts)
                rows.append(projection.project_bytes(kind, data, truncated))
            except Missing:
                rows.append(projection._row(kind, 'Missing'))
            except Exception:
                rows.append(projection._row(kind, 'Rejected'))
        result = dict(DiagnosticStatus='Collected', Files=rows)
    except Exception:
        result = dict(DiagnosticStatus='SafetyUnknown', Files=[])
    finally:
        if root is not None:
            try:
                fs.close(root)
            except Exception:
                result = dict(DiagnosticStatus='SafetyUnknown', Files=[])
    return result
