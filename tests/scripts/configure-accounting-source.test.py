"""AccountingV1 exact runtime version / real shallow Git gates. No CMake or Win32."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
import uuid
import unittest
import yaml

ROOT = Path(__file__).resolve().parents[2]
PWSH = os.environ.get('KUAIYI_TEST_PWSH', '/tmp/kuaiyi-pwsh/pwsh')
PARENT = '9d75368aa246b40e2bfc9517eb390384c6faf470'
PARENTTREE = '2f0d8993206519bf42991055ee0abda92627db3b'
LOCKED = '3ae69419a1152ea9694a3a9cf11130fbae645fa3'
LOCKEDTREE = '870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b'
BRANCH = 'dev/configure-accounting-windows'
WORKFLOW = '.github/workflows/windows-configure-accounting.yml'
DEPTH = 22
FILES = ['.github/workflows/windows-configure-accounting.yml', 'docs/progress.md', 'docs/test-evidence/2026-10-15-configure-accounting-source.json', 'docs/validation/configure-accounting-next-run.md', 'tests/scripts/configure-accounting-source.test.py', 'tests/scripts/run-configure-only-windows.ps1']
RUNTIME = {'tools/ci/LifecycleOperation.cs': {'Blob': 'b8b6fcf80cc763bf4289591765bb42e4bb31df37', 'Sha256': '585a20758ce699fdc0c927ddd451239008b11985134e2a529929ba28bea893a3'}, 'tests/scripts/lifecycle-ci-evidence.ps1': {'Blob': '208b2df950a05511078f5205f0b7442fabf4ca8e', 'Sha256': 'fcebcab0813a3302a40564cdbb11e9a98ca445f2bc7f356bdb5a78b88abae7ea'}, 'tests/scripts/run-configure-only-windows.ps1': {'Blob': '71e742f1642090c8690a3b44b5b704687bdb442d', 'Sha256': 'a04683f16bb55d453821440071d41b2994594f0580e2cc0f23cb7e31c9848efc'}}


def run(args, cwd=ROOT, env=None, check=True, data=None):
    return subprocess.run(args, cwd=cwd, env=env, input=data, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, check=check, timeout=25)


def git(*args, cwd=ROOT, **kwargs):
    return run(['git', *args], cwd=cwd, **kwargs).stdout.decode().strip()


class AccountingVersionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.w = yaml.load((ROOT / WORKFLOW).read_text(), Loader=yaml.BaseLoader)
        cls.gate = cls.w['jobs']['configure-accounting-only']['steps'][1]['run']

    def test_single_isolated_workflow_and_limits(self):
        self.assertEqual(self.w['on'], {'push': {'branches': [BRANCH], 'paths': [WORKFLOW]}})
        self.assertEqual(self.w['permissions'], {'contents': 'read'})
        self.assertEqual(list(self.w['jobs']), ['configure-accounting-only'])
        job = self.w['jobs']['configure-accounting-only']
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
        expected = old.replace(b"ReviewedParent='b4592d5d60d38cbbf186a38dbba2b1ae07b0482c'", ("ReviewedParent='" + PARENT + "'").encode())
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
        with tempfile.TemporaryDirectory(prefix='accounting-version-owned-') as temp:
            base = Path(temp);bare = base / 'owned.git'
            git('clone', '--bare', '--shared', str(ROOT), str(bare))
            env = os.environ.copy();env['GIT_INDEX_FILE'] = str(base / 'index')
            prefix = ['git', '--git-dir=' + str(bare), '--work-tree=' + str(ROOT)]
            run(prefix + ['read-tree', PARENT], env=env)
            run(prefix + ['add', '--', *FILES], env=env)
            tree = run(prefix + ['write-tree'], env=env).stdout.decode().strip()
            def commit(t, parent=PARENT):
                return git('-c', 'user.name=Synthetic', '-c', 'user.email=fixture@example.invalid',
                           'commit-tree', t, '-p', parent, '-m', 'Owned AccountingV1 source contract', cwd=bare)
            candidate = commit(tree)
            git('update-ref', 'refs/heads/candidate', candidate, cwd=bare)
            def check_gate(clone, sha, expected, source=None):
                script = base / 'source-gate.ps1';script.write_text(self.gate if source is None else source)
                gate_env = os.environ.copy();gate_env['GITHUB_SHA'] = sha
                r = run([PWSH, '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(script)], cwd=clone, env=gate_env, check=False)
                if expected and r.returncode != 0:
                    script.write_text((self.gate if source is None else source).replace("catch {throw 'Accounting source gate rejected'}", "catch {throw}"))
                    debug = run([PWSH, '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(script)], cwd=clone, env=gate_env, check=False)
                    self.fail(r.stderr.decode() + debug.stderr.decode())
                self.assertEqual(r.returncode == 0, expected, r.stderr.decode())
                if expected:
                    self.assertIn(('AccountingSourceVerified Version=AccountingV1 Head=' + sha + ' Tree=' + tree + ' Parent=' + PARENT).encode(), r.stdout)
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
        destination = ROOT / 'artifacts/accounting-version-tests' / uuid.uuid4().hex
        destination.mkdir(parents=True)
        log = destination / 'legacy-source-rejection.log'
        log.write_bytes(result.stdout + result.stderr)
        log.chmod(0o600)

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
