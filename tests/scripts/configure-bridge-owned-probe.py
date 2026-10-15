"""Windows fixture observer. All content stays in the private owned directory.

The reported counters describe actual calls through the production bridge and
file adapter; values, handles, paths and errors are never printed.
"""
import ctypes as c
import json
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools' / 'ci'))
import lifecycle_configure_collect as bridge_module
import lifecycle_configure_windows as files_module


class CountedBridge(bridge_module.SourceHandles):
    def __init__(self):
        super().__init__()
        self.receives = 0
        self.closed = 0

    def receive(self, pid, handle):
        self.receives += 1
        return super().receive(pid, handle)

    def close(self, handle):
        super().close(handle)
        self.closed += 1


class CountedFiles(files_module.NativeWindowsFiles):
    def __init__(self):
        super().__init__()
        self.reads = []
        self.owned = set()

    def duplicate(self, handle):
        result = super().duplicate(handle)
        self.owned.add(result)
        return result

    def open_child(self, parent, name, directory):
        result = super().open_child(parent, name, directory)
        self.owned.add(result)
        return result

    def read(self, handle, count):
        result = super().read(handle, count)
        self.reads.append(len(result))
        return result

    def close(self, handle):
        super().close(handle)
        self.owned.discard(handle)


def observe(pid, proof, *, bridge=None, filesystem=None):
    bridge = bridge if bridge is not None else CountedBridge()
    filesystem = filesystem if filesystem is not None else CountedFiles()
    diagnostic = bridge_module.collect(pid, proof, cleanup_confirmed=True,
                                       bridge=bridge, filesystem=filesystem)
    return dict(Diagnostic=diagnostic, BridgeReceives=bridge.receives,
                BridgeCloses=bridge.closed, ReadCalls=len(filesystem.reads),
                ReadBytes=sum(filesystem.reads), AdapterHandlesOpen=len(filesystem.owned))


def main(arguments):
    result = dict(Diagnostic=bridge_module.empty('OwnershipUnconfirmed'),
                  BridgeReceives=0, BridgeCloses=0, ReadCalls=0,
                  ReadBytes=0, AdapterHandlesOpen=0)
    try:
        if (os.name == 'nt' and sys.version_info >= (3, 8) and c.sizeof(files_module.HANDLE) == 8 and
                len(arguments) == 5 and arguments[0] == 'confirmed' and
                all(value.isascii() and value.isdecimal() and len(value) <= 20 for value in arguments[1:])):
            pid, handle, volume, index = map(int, arguments[1:])
            result = observe(pid, files_module.RootProof(handle, volume, index))
        elif os.name != 'nt':
            result['Diagnostic'] = bridge_module.empty('ReaderUnavailable')
    except Exception:
        result = dict(Diagnostic=bridge_module.empty('SafetyUnknown'),
                      BridgeReceives=0, BridgeCloses=0, ReadCalls=0,
                      ReadBytes=0, AdapterHandlesOpen=0)
    print(json.dumps(result, ensure_ascii=True, separators=(',', ':')))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
