"""Offline post-cleanup diagnostic projection. Never imported by the CI safety envelope.

POSIX reads use descriptor-relative no-follow opens. Windows is fail-closed until a
Windows filesystem binding is separately implemented and verified; no Win32/process
queries are added here. Output contains only a closed schema of finite facts.
"""
import os
import re
import stat

LIMIT = 65536
FILES = (
    ('CMakeVersion', ('CMakeVersion.local.log',)),
    ('NativeConfigure', ('NativeConfigure.local.log',)),
    ('CMakeCache', ('native', 'CMakeCache.txt')),
    ('ConfigureEvents', ('native', 'CMakeFiles', 'CMakeConfigureLog.yaml')),
)


def _facts():
    return dict(CMakeVersion=None, ConfigureDone=False, GenerateDone=False,
                CompilerIdSeen=False, AbiResult='Unknown', WorkingResult='Unknown',
                VisualStudio2022=False, TryCompileEvent=False)


def _row(kind, status, truncated=False):
    return dict(Kind=kind, ReadStatus=status, Truncated=truncated, Facts=_facts())


def project_bytes(kind, data, truncated=False):
    """Same bounded parser for all platforms; raw bytes never cross output schema."""
    if kind not in {k for k, _ in FILES}:
        return _row('Unknown', 'Unknown')
    row = _row(kind, 'Unknown', bool(truncated))
    if not isinstance(data, bytes):
        return row
    if truncated or len(data) > LIMIT:
        return _row(kind, 'Truncated', True)
    try:
        text = data.decode('utf-8-sig', errors='strict')
    except UnicodeError:
        return _row(kind, 'InvalidEncoding')
    if '\x00' in text:
        return _row(kind, 'InvalidEncoding')
    facts = _facts()
    seen = set()
    known = False
    event_header = False
    for line in text.splitlines():
        if len(line) > 4096:
            return _row(kind, 'Unknown')
        key, value = None, None
        if kind == 'CMakeVersion':
            m = re.fullmatch(r'cmake version ([0-9]{1,4})\.([0-9]{1,4})\.([0-9]{1,4})', line)
            if m:
                key, value = 'CMakeVersion', '.'.join(str(int(v)) for v in m.groups())
        elif kind == 'NativeConfigure':
            if re.fullmatch(r'-- Configuring done(?: \([0-9]+(?:\.[0-9]+)?s\))?', line):
                key, value = 'ConfigureDone', True
            elif re.fullmatch(r'-- Generating done(?: \([0-9]+(?:\.[0-9]+)?s\))?', line):
                key, value = 'GenerateDone', True
            elif re.fullmatch(r'-- The CXX compiler identification is MSVC [0-9.]{1,32}', line):
                key, value = 'CompilerIdSeen', True
            else:
                m = re.fullmatch(r'-- Detecting CXX compiler ABI info - (done|failed)', line)
                if m:
                    key, value = 'AbiResult', {'done': 'Done', 'failed': 'Failed'}[m[1]]
                else:
                    m = re.fullmatch(r'-- Check for working CXX compiler: .{1,1024} - (works|broken|skipped)', line)
                    if m:
                        key, value = 'WorkingResult', {'works': 'Works', 'broken': 'Broken', 'skipped': 'Skipped'}[m[1]]
        elif kind == 'CMakeCache':
            if line == 'CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022':
                key, value = 'VisualStudio2022', True
        elif kind == 'ConfigureEvents':
            # Recognize only canonical event header+kind. Never inspect YAML values,
            # command strings, message bodies, backtraces or arbitrary YAML tags.
            if event_header and line == '    kind: "try_compile-v1"':
                facts['TryCompileEvent'] = True
                known = True
            event_header = line == '  -'
        if key is not None:
            # Duplicate recognized scalar facts are ambiguous, even if equal.
            if key in seen:
                return _row(kind, 'Unknown')
            seen.add(key)
            facts[key] = value
            known = True
    row['ReadStatus'] = 'Projected' if known else 'Unknown'
    row['Facts'] = facts
    return row


class _Rejected(Exception):
    pass


def _stamp(s):
    return s.st_dev, s.st_ino, s.st_mode, s.st_nlink, s.st_size, s.st_mtime_ns, s.st_ctime_ns


def _bound_read(root, components):
    """At most LIMIT bytes; no recursion, symlink traversal, devices or FIFOs.

    Held directory descriptors anchor every open; before/after metadata and entry
    identity checks reject observed replacement/change. This is not an atomic
    whole-filesystem snapshot and does not promise bounded syscall wall time.
    """
    handles, bindings = [], []
    try:
        if not isinstance(root, str) or len(root) > 4096 or not os.path.isabs(root):
            raise _Rejected()
        parts = root.split('/')
        if '..' in parts or '.' in parts or '\x00' in root:
            raise _Rejected()
        parts = [p for p in parts if p]
        if len(parts) > 32:
            raise _Rejected()
        flags = os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW | os.O_NONBLOCK
        fd = os.open('/', flags | os.O_DIRECTORY)
        handles.append(fd)
        for name in parts + list(components[:-1]):
            before = os.fstat(fd)
            child = os.open(name, flags | os.O_DIRECTORY, dir_fd=fd)
            handles.append(child)
            current = os.fstat(child)
            if not stat.S_ISDIR(current.st_mode):
                raise _Rejected()
            bindings.append((fd, name, child, _stamp(before), _stamp(current)))
            fd = child
        before = os.fstat(fd)
        file_fd = os.open(components[-1], flags, dir_fd=fd)
        handles.append(file_fd)
        info = os.fstat(file_fd)
        if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
            raise _Rejected()
        bindings.append((fd, components[-1], file_fd, _stamp(before), _stamp(info)))
        chunks, remaining = [], min(info.st_size, LIMIT)
        while remaining:
            chunk = os.read(file_fd, remaining)
            if not chunk:
                raise _Rejected()
            chunks.append(chunk)
            remaining -= len(chunk)
        for parent, name, held, parent_stamp, child_stamp in reversed(bindings):
            if _stamp(os.fstat(parent)) != parent_stamp or _stamp(os.fstat(held)) != child_stamp:
                raise _Rejected()
            if _stamp(os.stat(name, dir_fd=parent, follow_symlinks=False)) != child_stamp:
                raise _Rejected()
        return b''.join(chunks), info.st_size > LIMIT
    finally:
        close_failed = False
        for fd in reversed(handles):
            try:
                os.close(fd)
            except OSError:
                close_failed = True
        if close_failed:
            raise _Rejected()


def project_after_cleanup(root, *, cleanup_confirmed):
    """Return a separate diagnostic object, never modify an operation receipt.

    Caller must supply the actual final cleanup confirmation. No filesystem calls
    occur when it is absent; there is no integration/call site in LifecycleOperation
    or a workflow. Windows read adapter intentionally fails closed, not a fake read.
    """
    if cleanup_confirmed is not True:
        return dict(DiagnosticStatus='SkippedCleanupUnconfirmed', Files=[])
    if os.name != 'posix' or not all(hasattr(os, n) for n in ('O_NOFOLLOW', 'O_DIRECTORY', 'O_CLOEXEC')):
        return dict(DiagnosticStatus='ReaderUnavailable', Files=[])
    rows = []
    try:
        for kind, components in FILES:
            try:
                data, truncated = _bound_read(root, components)
                rows.append(project_bytes(kind, data, truncated))
            except FileNotFoundError:
                rows.append(_row(kind, 'Missing'))
            except (OSError, _Rejected):
                rows.append(_row(kind, 'Rejected'))
        return dict(DiagnosticStatus='Collected', Files=rows)
    except Exception:
        # Do not expose exception text or return partially collected arbitrary data.
        return dict(DiagnosticStatus='ProjectionError', Files=[])
