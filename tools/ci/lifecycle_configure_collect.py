"""Post-rescue collector. Parent retains its pre-configure lease throughout Run.

The incoming handle belongs to the explicit source process, never this Python
process. Only DuplicateHandle's returned target handle is passed to the adapter.
No process enumeration, privilege adjustment, services, retries or raw output.
"""
import ctypes as c
import json
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import lifecycle_configure_windows as w


def empty(status):
    return dict(DiagnosticStatus=status, Files=[])


class SourceHandles:
    def __init__(self):
        if os.name != 'nt':
            raise w.Rejected()
        self.k = c.WinDLL('kernel32', use_last_error=True)
        for fn, args, result in (
            (self.k.OpenProcess, [w.U32, c.c_int32, w.U32], w.HANDLE),
            (self.k.DuplicateHandle, [w.HANDLE, w.HANDLE, w.HANDLE, c.POINTER(w.HANDLE), w.U32, c.c_int32, w.U32], c.c_int32),
            (self.k.CloseHandle, [w.HANDLE], c.c_int32),
        ):
            fn.argtypes, fn.restype = args, result

    def receive(self, source_pid, source_handle):
        source, target = None, w.HANDLE()
        try:
            source = self.k.OpenProcess(0x40, False, source_pid)  # PROCESS_DUP_HANDLE only
            if not source:
                raise w.Rejected()
            if not self.k.DuplicateHandle(w.HANDLE(source), w.HANDLE(source_handle),
                                          w.HANDLE(-1), c.byref(target), 0, False, 2):
                raise w.Rejected()
            if not target.value:
                raise w.Rejected()
        except Exception:
            if target.value:
                owned, target.value = target.value, None
                self.close(owned)
            raise
        finally:
            if source and not self.k.CloseHandle(w.HANDLE(source)):
                if target.value:
                    owned, target.value = target.value, None
                    self.close(owned)
                raise w.Rejected()
        return target.value

    def close(self, handle):
        if not self.k.CloseHandle(w.HANDLE(handle)):
            raise w.Rejected()


def collect(source_pid, proof, *, cleanup_confirmed, bridge=None, filesystem=None):
    # No process/file API, or even binding construction, before the actual gate.
    if cleanup_confirmed is not True:
        return empty('SkippedCleanupUnconfirmed')
    if (type(source_pid) is not int or not 0 < source_pid <= 0xffffffff or
            not isinstance(proof, w.RootProof) or
            any(type(v) is not int or v <= 0 for v in vars(proof).values()) or
            proof.handle >= (1 << (c.sizeof(w.HANDLE)*8))-1 or
            proof.volume > 0xffffffff or proof.index > 0xffffffffffffffff):
        return empty('OwnershipUnconfirmed')
    root, result = None, empty('SafetyUnknown')
    try:
        bridge = bridge if bridge is not None else SourceHandles()
        root = bridge.receive(source_pid, proof.handle)
        if type(root) is not int or not 0 < root < (1 << (c.sizeof(w.HANDLE)*8))-1:
            raise w.Rejected()
        received = w.RootProof(root, proof.volume, proof.index)
        result = w.project_windows_after_cleanup(received, cleanup_confirmed=True, filesystem=filesystem)
    except Exception:
        result = empty('SafetyUnknown')
    finally:
        if root is not None:
            try:
                bridge.close(root)
            except Exception:
                result = empty('SafetyUnknown')
    return result


def main(arguments):
    # Private numeric transport only. CLI cannot accept a root path or file list.
    result = empty('OwnershipUnconfirmed')
    try:
        if sys.version_info < (3, 8) or c.sizeof(w.HANDLE) != 8:
            result = empty('ReaderUnavailable')
        elif arguments == ['unconfirmed']:
            result = empty('SkippedCleanupUnconfirmed')
        elif (len(arguments) == 5 and arguments[0] == 'confirmed' and
              all(value.isascii() and value.isdecimal() and len(value) <= 20 for value in arguments[1:])):
            pid, handle, volume, index = map(int, arguments[1:])
            result = collect(pid, w.RootProof(handle, volume, index), cleanup_confirmed=True)
    except Exception:
        result = empty('ProjectionError')
    print(json.dumps(result, ensure_ascii=True, separators=(',', ':')))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
