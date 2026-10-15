"""EmbeddedV1 exact local source contract. Git/Python only; never configures CMake."""
import hashlib
import json
import os
import shutil
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
PARENT = 'a1291b99f12d46bedc96805f70a10215e53d6247'
PARENT_TREE = 'ea3d47bbb448d8b97eefc7ac8233a7051b121f05'
LOCKED = '3ae69419a1152ea9694a3a9cf11130fbae645fa3'
LOCKED_TREE = '870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b'
DEPTH = 24
ENTRY = 'tests/scripts/run-configure-only-windows.ps1'
CONTRACT = 'docs/test-evidence/2026-10-15-configure-embedded-source.json'
FILES = ['docs/progress.md', CONTRACT, 'docs/validation/configure-embedded-static.md',
         'tests/scripts/configure-embedded-source.test.py', ENTRY]
RUNTIME = {
    'tools/ci/LifecycleOperation.cs': ('b8b6fcf80cc763bf4289591765bb42e4bb31df37', '585a20758ce699fdc0c927ddd451239008b11985134e2a529929ba28bea893a3'),
    'tests/scripts/lifecycle-ci-evidence.ps1': ('208b2df950a05511078f5205f0b7442fabf4ca8e', 'fcebcab0813a3302a40564cdbb11e9a98ca445f2bc7f356bdb5a78b88abae7ea'),
    ENTRY: ('541609f5b76ceb998930d7aa1c43049f83a8b33e', '22a881d2c2436dc92633fd777d2f5bd3a8c40cdaac5753e549cecc0dd0431c75')}


def command(args, cwd=ROOT, env=None, data=None, check=True):
    return subprocess.run(args, cwd=cwd, env=env, input=data, check=check,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20)


def git(*args, cwd=ROOT):
    return command(['git', *args], cwd=cwd).stdout.decode().strip()


def verify(root, expected):
    """Read-only gate; expected event SHA required. Fail closed with finite output."""
    try:
        if len(expected) != 40 or any(c not in '0123456789abcdef' for c in expected):
            return False
        if git('rev-parse', 'HEAD', cwd=root) != expected:
            return False
        if git('rev-list', '--parents', '-n', '1', 'HEAD', cwd=root).split() != [expected, PARENT]:
            return False
        if git('rev-parse', PARENT + '^{tree}', cwd=root) != PARENT_TREE:
            return False
        git('merge-base', '--is-ancestor', LOCKED, 'HEAD', cwd=root)
        if git('rev-parse', LOCKED + '^{tree}', cwd=root) != LOCKED_TREE:
            return False
        if sorted(git('diff', '--name-only', PARENT, 'HEAD', '--', cwd=root).splitlines()) != sorted(FILES):
            return False
        for name, (blob, digest) in RUNTIME.items():
            if git('rev-parse', 'HEAD:' + name, cwd=root) != blob:
                return False
            if git('hash-object', '--path=' + name, '--', name, cwd=root) != blob:
                return False
            raw = command(['git', 'show', 'HEAD:' + name], cwd=root).stdout
            if hashlib.sha256(raw).hexdigest() != digest:
                return False
        return True
    except (OSError, ValueError, subprocess.SubprocessError):
        return False


class EmbeddedSourceTests(unittest.TestCase):
    def test_exact_single_configuration_change_and_provenance(self):
        old = command(['git', 'show', PARENT + ':' + ENTRY]).stdout
        expected = old.replace(b"ReviewedParent='9d75368aa246b40e2bfc9517eb390384c6faf470'",
                               ("ReviewedParent='" + PARENT + "'").encode())
        expected = expected.replace(b"'-DCMAKE_SYSTEM_VERSION=10.0.26100.0')",
                                    b"'-DCMAKE_SYSTEM_VERSION=10.0.26100.0','-DCMAKE_MSVC_DEBUG_INFORMATION_FORMAT=Embedded')")
        self.assertEqual((ROOT / ENTRY).read_bytes(), expected)
        self.assertEqual(expected.count(b'-DCMAKE_MSVC_DEBUG_INFORMATION_FORMAT=Embedded'), 1)
        for name in RUNTIME:
            blob, digest = RUNTIME[name]
            self.assertEqual(git('hash-object', name), blob)
            self.assertEqual(hashlib.sha256((ROOT / name).read_bytes()).hexdigest(), digest)
            if name != ENTRY:
                self.assertEqual((ROOT / name).read_bytes(), command(['git', 'show', PARENT + ':' + name]).stdout)

    def test_policy_prerequisite_and_no_product_or_workflow_change(self):
        name = 'tools/KuaiYi.ReuseLifecycleNative/CMakeLists.txt'
        raw = (ROOT / name).read_bytes()
        self.assertEqual(raw, command(['git', 'show', PARENT + ':' + name]).stdout)
        self.assertTrue(raw.startswith(b'cmake_minimum_required(VERSION 3.28)\nproject('))
        workflows = list((ROOT / '.github/workflows').glob('*.yml'))
        self.assertEqual(len(workflows), 7)
        for path in workflows:
            self.assertEqual(path.read_bytes(), command(['git', 'show', PARENT + ':' + str(path.relative_to(ROOT))]).stdout)
            self.assertNotIn(b'dev/configure-embedded-local', path.read_bytes())
        self.assertEqual(int(git('rev-list', '--count', LOCKED + '..' + PARENT)) + 2, DEPTH)
        self.assertEqual(git('rev-parse', PARENT + '^{tree}'), PARENT_TREE)

    def test_independent_contract_matches_exact_runtime(self):
        c = json.loads((ROOT / CONTRACT).read_text())
        self.assertEqual((c['Parent'], c['ParentTree'], c['ShallowCandidateDepth']), (PARENT, PARENT_TREE, DEPTH))
        self.assertEqual(c['Files'], FILES)
        self.assertEqual(c['RuntimeCanonicalVersions'], {n: {'Blob': b, 'Sha256': s} for n, (b, s) in RUNTIME.items()})
        self.assertEqual(c['CMakeCommit'], '859ca5c4d7396a011a462878179bc173e1283731')
        self.assertEqual((c['Windows'], c['Push'], c['RootCause']), ('NotRun', False, 'Unknown'))

    def test_injected_contract_with_exact_new_option(self):
        # Preserve and actually exercise the old rejection. Only an owned test
        # copy updates its exact expected command; no scenario/assertion removed.
        logdir = ROOT / 'artifacts/configure-embedded-static'
        pwsh = os.environ.get('KUAIYI_TEST_PWSH', '/tmp/kuaiyi-pwsh/pwsh')
        old = command([pwsh, '-NoLogo', '-NoProfile', '-File', 'tests/scripts/configure-only-contracts.test.ps1'], check=False)
        (logdir / 'old-configure-rejection.log').write_bytes(old.stdout + old.stderr)
        self.assertNotEqual(old.returncode, 0)
        self.assertIn(b'Configure-only contract failed', old.stderr)
        with tempfile.TemporaryDirectory(prefix='embedded-injected-owned-') as temp:
            clone = Path(temp) / 'repo'
            git('clone', '--quiet', '--shared', str(ROOT), str(clone))
            shutil.copyfile(ROOT / ENTRY, clone / ENTRY)
            test = clone / 'tests/scripts/configure-only-contracts.test.ps1'
            before = test.read_text()
            token = "Check ($runner.Contains($originalConfigure.Replace('Invoke-BoundedLifecycleOperation','Invoke-ConfigureOnlyOperation')))"
            replacement = "Check ($runner.Contains($originalConfigure.Replace('Invoke-BoundedLifecycleOperation','Invoke-ConfigureOnlyOperation').Replace(\"'-DCMAKE_SYSTEM_VERSION=10.0.26100.0')\",\"'-DCMAKE_SYSTEM_VERSION=10.0.26100.0','-DCMAKE_MSVC_DEBUG_INFORMATION_FORMAT=Embedded')\")))"
            self.assertEqual(before.count(token), 1)
            test.write_text(before.replace(token, replacement))
            result = command([pwsh, '-NoLogo', '-NoProfile', '-File', str(test)], cwd=clone, check=False)
            (logdir / 'embedded-injected-configure.log').write_bytes(result.stdout + result.stderr)
            self.assertEqual(result.returncode, 0, result.stderr.decode())
            self.assertIn(b'CONFIGURE_ONLY_CONTRACTS passed=171 failed=0', result.stdout)

    def test_real_shallow_gate_and_rejections(self):
        with tempfile.TemporaryDirectory(prefix='embedded-source-owned-') as temp:
            base = Path(temp);bare = base / 'owned.git'
            git('clone', '--bare', '--shared', str(ROOT), str(bare))
            env = os.environ.copy();env['GIT_INDEX_FILE'] = str(base / 'index')
            prefix = ['git', '--git-dir=' + str(bare), '--work-tree=' + str(ROOT)]
            command(prefix + ['read-tree', PARENT], env=env)
            command(prefix + ['add', '--', *FILES], env=env)
            tree = command(prefix + ['write-tree'], env=env).stdout.decode().strip()
            def commit(t, parent=PARENT):
                return git('-c', 'user.name=Synthetic', '-c', 'user.email=fixture@example.invalid',
                           'commit-tree', t, '-p', parent, '-m', 'Owned EmbeddedV1 source', cwd=bare)
            candidate = commit(tree);git('update-ref', 'refs/heads/candidate', candidate, cwd=bare)
            for depth in (DEPTH - 1, DEPTH):
                clone = base / str(depth)
                git('clone', '--quiet', '--depth', str(depth), '--single-branch', '--branch', 'candidate', bare.as_uri(), str(clone))
                self.assertEqual(verify(clone, candidate), depth == DEPTH)
            self.assertFalse(verify(clone, '0' * 40))
            git('config', 'core.autocrlf', 'true', cwd=clone)
            entry = clone / ENTRY;entry.unlink();git('checkout', '--', ENTRY, cwd=clone)
            self.assertIn(b'\r\n', entry.read_bytes());self.assertTrue(verify(clone, candidate))
            entry.write_bytes(entry.read_bytes() + b'# drift\n');self.assertFalse(verify(clone, candidate))
            git('reset', '--hard', 'HEAD', cwd=clone)
            for name, data in [(ENTRY, (ROOT / ENTRY).read_bytes().replace(b'FORMAT=Embedded', b'FORMAT=ProgramDatabase')),
                               ('docs/EXTRA_EMBEDDED_SOURCE', b'extra\n')]:
                command(prefix + ['read-tree', tree], env=env)
                blob = command(['git', '--git-dir=' + str(bare), 'hash-object', '-w', '--stdin'], data=data).stdout.decode().strip()
                command(prefix + ['update-index', '--add', '--cacheinfo', '100644,' + blob + ',' + name], env=env)
                altered_tree = command(prefix + ['write-tree'], env=env).stdout.decode().strip()
                altered = commit(altered_tree);git('fetch', '--quiet', bare.as_uri(), altered, cwd=clone)
                git('checkout', '--quiet', '--detach', altered, cwd=clone);self.assertFalse(verify(clone, altered))
            command(prefix + ['read-tree', tree], env=env)
            blob = git('rev-parse', candidate + ':docs/progress.md', cwd=bare)
            command(prefix + ['update-index', '--force-remove', 'docs/progress.md'], env=env)
            command(prefix + ['update-index', '--add', '--cacheinfo', '100644,' + blob + ',docs/Progress.md'], env=env)
            altered_tree = command(prefix + ['write-tree'], env=env).stdout.decode().strip()
            altered = commit(altered_tree);git('fetch', '--quiet', bare.as_uri(), altered, cwd=clone)
            git('checkout', '--quiet', '--detach', altered, cwd=clone);self.assertFalse(verify(clone, altered))
            wrong = commit(tree, 'a0e8519e63d90e44560316478dc65358a18a8868')
            git('fetch', '--quiet', bare.as_uri(), wrong, cwd=clone)
            git('checkout', '--quiet', '--detach', wrong, cwd=clone);self.assertFalse(verify(clone, wrong))


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--verify-source':
        ok = verify(Path.cwd(), sys.argv[2])
        print('EmbeddedSourceVerified' if ok else 'EmbeddedSourceRejected')
        sys.exit(0 if ok else 1)
    unittest.main(verbosity=2)
