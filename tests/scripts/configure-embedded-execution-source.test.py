"""EmbeddedExecV1 exact execution runtime / shallow Git gates. No CMake or Win32."""
import hashlib
import os
import shutil
from pathlib import Path
import subprocess
import tempfile
import uuid
import unittest
import yaml

ROOT = Path(__file__).resolve().parents[2]
PWSH = os.environ.get('KUAIYI_TEST_PWSH', '/tmp/kuaiyi-pwsh/pwsh')
PARENT = 'b771f1e8442d453b3df65144f006f694a2b7a294'
PARENTTREE = '300daf380032eaeecd476f90111055075b51b231'
LOCKED = '3ae69419a1152ea9694a3a9cf11130fbae645fa3'
LOCKEDTREE = '870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b'
BRANCH = 'dev/configure-embedded-windows'
WORKFLOW = '.github/workflows/windows-configure-embedded.yml'
DEPTH = 25
ENTRY = 'tests/scripts/run-configure-only-windows.ps1'
FILES = ['.github/workflows/windows-configure-embedded.yml', 'docs/progress.md', 'docs/test-evidence/2026-10-15-configure-embedded-execution-source.json', 'docs/validation/configure-embedded-execution.md', 'tests/scripts/configure-embedded-execution-source.test.py', 'tests/scripts/run-configure-only-windows.ps1']
RUNTIME = {'tools/ci/LifecycleOperation.cs': {'Blob': 'b8b6fcf80cc763bf4289591765bb42e4bb31df37', 'Sha256': '585a20758ce699fdc0c927ddd451239008b11985134e2a529929ba28bea893a3'}, 'tests/scripts/lifecycle-ci-evidence.ps1': {'Blob': '208b2df950a05511078f5205f0b7442fabf4ca8e', 'Sha256': 'fcebcab0813a3302a40564cdbb11e9a98ca445f2bc7f356bdb5a78b88abae7ea'}, 'tests/scripts/run-configure-only-windows.ps1': {'Blob': '59cc44e75b4014ba8949b6f51a4ba1f84b05d04e', 'Sha256': 'ca859f95168861b2fae195c121ac688d609f368daebf874516d3ba5578ed493d'}}


def run(args, cwd=ROOT, env=None, check=True, data=None):
    return subprocess.run(args, cwd=cwd, env=env, input=data, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, check=check, timeout=25)


def git(*args, cwd=ROOT, **kwargs):
    return run(['git', *args], cwd=cwd, **kwargs).stdout.decode().strip()


class EmbeddedExecutionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.w = yaml.load((ROOT / WORKFLOW).read_text(), Loader=yaml.BaseLoader)
        cls.gate = cls.w['jobs']['configure-embedded-only']['steps'][1]['run']

    def test_single_isolated_workflow_and_limits(self):
        self.assertEqual(self.w['on'], {'push': {'branches': [BRANCH], 'paths': [WORKFLOW]}})
        self.assertEqual(self.w['permissions'], {'contents': 'read'})
        self.assertEqual(list(self.w['jobs']), ['configure-embedded-only'])
        job = self.w['jobs']['configure-embedded-only']
        self.assertEqual((job['runs-on'], job['timeout-minutes']), ('windows-2022', '10'))
        a, b, c = job['steps']
        self.assertEqual(a['uses'], 'actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803')
        self.assertEqual(a['with'], {'ref': '${{ github.sha }}', 'fetch-depth': str(DEPTH), 'persist-credentials': 'false', 'set-safe-directory': 'false'})
        self.assertEqual((b['timeout-minutes'], c['timeout-minutes']), ('1', '7'))
        self.assertEqual(c['shell'], 'pwsh -NoLogo -NoProfile -NonInteractive -File "{0}"')
        self.assertEqual(c['run'], "$ErrorActionPreference='Stop'\n& ./tests/scripts/run-configure-only-windows.ps1\nexit $LASTEXITCODE\n")
        for p in (ROOT / '.github/workflows').glob('*.yml'):
            if p.name != Path(WORKFLOW).name:
                self.assertEqual(p.read_bytes(), run(['git', 'show', PARENT + ':' + str(p.relative_to(ROOT))]).stdout)
                self.assertNotIn(BRANCH, yaml.load(p.read_text(), Loader=yaml.BaseLoader)['on']['push']['branches'])
        self.assertNotRegex((ROOT / WORKFLOW).read_text(), 'workflow_dispatch|schedule:|matrix:|rerun|upload-artifact|setup-python|setup-dotnet|run-lifecycle-windows-ci')

    def test_exact_three_runtime_versions_and_parent_only_entry_change(self):
        for p, pin in RUNTIME.items():
            self.assertEqual(git('hash-object', p), pin['Blob'])
            self.assertEqual(hashlib.sha256((ROOT / p).read_bytes()).hexdigest(), pin['Sha256'])
            self.assertIn("@{Path='" + p + "';Blob='" + pin['Blob'] + "';Sha256='" + pin['Sha256'] + "'}", self.gate)
        entry = 'tests/scripts/run-configure-only-windows.ps1'
        old = run(['git', 'show', PARENT + ':' + entry]).stdout
        expected = old.replace(b"ReviewedParent='a1291b99f12d46bedc96805f70a10215e53d6247'", ("ReviewedParent='" + PARENT + "'").encode())
        self.assertEqual((ROOT / entry).read_bytes(), expected)
        for p in RUNTIME:
            if p != entry:
                self.assertEqual((ROOT / p).read_bytes(), run(['git', 'show', PARENT + ':' + p]).stdout)
        self.assertEqual(git('rev-parse', PARENT + '^{tree}'), PARENTTREE)
        self.assertEqual(int(git('rev-list', '--count', LOCKED + '..' + PARENT)) + 2, DEPTH)
        self.assertIn('SHA256]::HashData($process.StandardOutput.BaseStream)', self.gate)
        self.assertIn('git hash-object', self.gate)
        self.assertIn('Compare-Object -CaseSensitive $allowed $changed', self.gate)
        self.assertIn('$git=@(Get-Command git -CommandType Application -ErrorAction Stop)[0].Source', self.gate)
        for p in FILES:
            self.assertIn("'" + p + "'", self.gate)

    def test_actual_depth_full_gate_and_counterexamples(self):
        with tempfile.TemporaryDirectory(prefix='embedded-execution-owned-') as temp:
            base = Path(temp);bare = base / 'owned.git'
            git('clone', '--bare', '--shared', str(ROOT), str(bare))
            env = os.environ.copy();env['GIT_INDEX_FILE'] = str(base / 'index')
            prefix = ['git', '--git-dir=' + str(bare), '--work-tree=' + str(ROOT)]
            run(prefix + ['read-tree', PARENT], env=env)
            run(prefix + ['add', '--', *FILES], env=env)
            tree = run(prefix + ['write-tree'], env=env).stdout.decode().strip()
            def commit(t, parent=PARENT):
                return git('-c', 'user.name=Synthetic', '-c', 'user.email=fixture@example.invalid',
                           'commit-tree', t, '-p', parent, '-m', 'Owned EmbeddedExecV1 source contract', cwd=bare)
            candidate = commit(tree)
            git('update-ref', 'refs/heads/candidate', candidate, cwd=bare)
            def check_gate(clone, sha, expected, source=None):
                script = base / 'source-gate.ps1';script.write_text(self.gate if source is None else source)
                gate_env = os.environ.copy();gate_env['GITHUB_SHA'] = sha
                r = run([PWSH, '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(script)], cwd=clone, env=gate_env, check=False)
                if expected and r.returncode != 0:
                    script.write_text((self.gate if source is None else source).replace("catch {throw 'Embedded execution source gate rejected'}", "catch {throw}"))
                    debug = run([PWSH, '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(script)], cwd=clone, env=gate_env, check=False)
                    self.fail(r.stderr.decode() + debug.stderr.decode())
                self.assertEqual(r.returncode == 0, expected, r.stderr.decode())
                if expected:
                    self.assertIn(('EmbeddedSourceVerified Version=EmbeddedExecV1 Head=' + sha + ' Tree=' + tree + ' Parent=' + PARENT).encode(), r.stdout)
            clones = {}
            for depth in (DEPTH - 1, DEPTH):
                clone = base / str(depth)
                git('clone', '--quiet', '--depth', str(depth), '--single-branch', '--branch', 'candidate', bare.as_uri(), str(clone))
                clones[depth] = clone
                check_gate(clone, candidate, depth == DEPTH)
            clone = clones[DEPTH]
            check_gate(clone, '0' * 40, False)
            # Existing repository text normalization: SHA256 pins canonical blob bytes, not CRLF checkout encoding.
            git('config', 'core.autocrlf', 'true', cwd=clone)
            entry = clone / 'tests/scripts/run-configure-only-windows.ps1';entry.unlink()
            git('checkout', '--', 'tests/scripts/run-configure-only-windows.ps1', cwd=clone)
            self.assertIn(b'\r\n', entry.read_bytes())
            check_gate(clone, candidate, True)
            entry.write_bytes(entry.read_bytes() + b'# working drift\n')
            check_gate(clone, candidate, False)
            git('reset', '--hard', 'HEAD', cwd=clone)
            check_gate(clone, candidate, False, self.gate.replace(next(iter(RUNTIME.values()))['Sha256'], '0' * 64))
            # Same valid parent and six allowed names, but the reviewed entry blob has drifted.
            for name, contents in [('tests/scripts/run-configure-only-windows.ps1', (ROOT / 'tests/scripts/run-configure-only-windows.ps1').read_bytes() + b'# committed drift\n'),
                                   ('docs/EXTRA_ACCOUNTING_CONTRACT', b'synthetic extra file\n')]:
                run(['git', '--git-dir=' + str(bare), 'read-tree', tree], env=env)
                blob = run(['git', '--git-dir=' + str(bare), 'hash-object', '-w', '--stdin'], data=contents).stdout.decode().strip()
                run(['git', '--git-dir=' + str(bare), 'update-index', '--add', '--cacheinfo', '100644,' + blob + ',' + name], env=env)
                altered_tree = run(['git', '--git-dir=' + str(bare), 'write-tree'], env=env).stdout.decode().strip()
                altered = commit(altered_tree)
                git('fetch', '--quiet', bare.as_uri(), altered, cwd=clone)
                git('checkout', '--quiet', '--detach', altered, cwd=clone)
                check_gate(clone, altered, False)
            # Path case is part of the reviewed file identity, even on case-insensitive Windows.
            run(['git', '--git-dir=' + str(bare), 'read-tree', tree], env=env)
            progress_blob = git('rev-parse', candidate + ':docs/progress.md', cwd=bare)
            run(prefix + ['update-index', '--force-remove', 'docs/progress.md'], env=env)
            run(['git', '--git-dir=' + str(bare), 'update-index', '--add', '--cacheinfo', '100644,' + progress_blob + ',docs/Progress.md'], env=env)
            case_tree = run(['git', '--git-dir=' + str(bare), 'write-tree'], env=env).stdout.decode().strip()
            case_drift = commit(case_tree)
            git('fetch', '--quiet', bare.as_uri(), case_drift, cwd=clone)
            git('checkout', '--quiet', '--detach', case_drift, cwd=clone)
            check_gate(clone, case_drift, False)
            wrong_parent = commit(tree, '118981aead5dc230351efb8ff210963d87d5522c')
            git('fetch', '--quiet', bare.as_uri(), wrong_parent, cwd=clone)
            git('checkout', '--quiet', '--detach', wrong_parent, cwd=clone)
            check_gate(clone, wrong_parent, False)

    def test_legacy_contract_still_rejects_this_version(self):
        result = run(['python3', 'tests/scripts/configure-only-source.test.py'], check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stderr.count(b'FAIL: '), 2)
        self.assertIn(b'FAIL: test_old_runner_workflows_and_native_dependencies_unchanged', result.stderr)
        self.assertIn(b'FAIL: test_entry_retains_original_configure_and_terminal_scope', result.stderr)
        destination = ROOT / 'artifacts/embedded-execution-tests' / uuid.uuid4().hex
        destination.mkdir(parents=True)
        log = destination / 'legacy-source-rejection.log'
        log.write_bytes(result.stdout + result.stderr)
        log.chmod(0o600)

    def test_baseline_only_embedded_option_and_source_identity(self):
        baseline = 'a0e8519e63d90e44560316478dc65358a18a8868'
        entry = 'tests/scripts/run-configure-only-windows.ps1'
        old = run(['git', 'show', baseline + ':' + entry]).stdout
        expected = old.replace(b"ReviewedParent='9d75368aa246b40e2bfc9517eb390384c6faf470'", ("ReviewedParent='" + PARENT + "'").encode())
        expected = expected.replace(b"'-DCMAKE_SYSTEM_VERSION=10.0.26100.0')", b"'-DCMAKE_SYSTEM_VERSION=10.0.26100.0','-DCMAKE_MSVC_DEBUG_INFORMATION_FORMAT=Embedded')")
        self.assertEqual((ROOT / entry).read_bytes(), expected)
        for name in RUNTIME:
            if name != entry:
                self.assertEqual((ROOT / name).read_bytes(), run(['git', 'show', baseline + ':' + name]).stdout)
        self.assertNotIn('MSBUILDDISABLENODEREUSE', (ROOT / entry).read_text())

    def test_accounting_v1_contract_preserved_and_rejects_new_version(self):
        name = 'tests/scripts/configure-accounting-source.test.py'
        self.assertEqual((ROOT / name).read_bytes(), run(['git', 'show', PARENT + ':' + name]).stdout)
        result = run(['python3', name], check=False)
        destination = ROOT / 'artifacts/embedded-execution-tests' / uuid.uuid4().hex
        destination.mkdir(parents=True)
        (destination / 'accounting-v1-rejection.log').write_bytes(result.stdout + result.stderr)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stderr.count(b'FAIL: '), 2)
        self.assertEqual(result.stderr.count(b'ERROR: '), 1)
        self.assertIn(b'ERROR: test_single_isolated_workflow_and_limits', result.stderr)
        self.assertIn(b'FAIL: test_actual_depth_full_gate_and_counterexamples', result.stderr)
        self.assertIn(b'FAIL: test_exact_three_runtime_versions_and_parent_only_entry_change', result.stderr)

    def test_injected_contract_with_exact_new_option(self):
        # Preserve and actually exercise the old rejection. Only an owned test
        # copy updates its exact expected command; no scenario/assertion removed.
        logdir = ROOT / 'artifacts/configure-embedded-execution-source'
        pwsh = os.environ.get('KUAIYI_TEST_PWSH', '/tmp/kuaiyi-pwsh/pwsh')
        old = run([pwsh, '-NoLogo', '-NoProfile', '-File', 'tests/scripts/configure-only-contracts.test.ps1'], check=False)
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
            result = run([pwsh, '-NoLogo', '-NoProfile', '-File', str(test)], cwd=clone, check=False)
            (logdir / 'embedded-injected-configure.log').write_bytes(result.stdout + result.stderr)
            self.assertEqual(result.returncode, 0, result.stderr.decode())
            self.assertIn(b'CONFIGURE_ONLY_CONTRACTS passed=171 failed=0', result.stdout)

    def test_original_contract_and_logs_not_rewritten(self):
        for p in ['tests/scripts/configure-only-source.test.py', '.github/workflows/windows-configure-only.yml',
                  'docs/test-evidence/2026-10-15-configure-accounting-local.json',
                  'docs/test-evidence/2026-10-15-job-member-suspended-windows-result.json']:
            self.assertEqual((ROOT / p).read_bytes(), run(['git', 'show', PARENT + ':' + p]).stdout)
        legacy = (ROOT / 'tests/scripts/configure-only-source.test.py').read_text()
        self.assertIn("PARENT = 'b4592d5d60d38cbbf186a38dbba2b1ae07b0482c'", legacy)
        self.assertNotIn('configure-accounting', legacy)


if __name__ == '__main__':
    unittest.main(verbosity=2)
