"""Real owned Git/source gates; no Windows native fixture or CMake execution."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

import yaml

ROOT = Path(__file__).resolve().parents[2]
PARENT = 'bb0fae011046daaff2f0cdfe9b7a7f00de7d1919'
TREE = '062ac230125b8da99c95a88b0cde75f658094470'
HELPER = '227378ffa3a59e80c65da7fc43c0cffcd21d70f7'
WORKFLOW = '.github/workflows/windows-job-member-fixture.yml'
BRANCH = 'dev/job-member-suspended-fixture'
FILES = ['.github/workflows/windows-job-member-fixture.yml', 'docs/progress.md', 'docs/test-evidence/2026-10-15-job-member-suspended-offline.json', 'docs/validation/job-member-suspended-plan.md', 'tests/scripts/job-member-fixture.test.py', 'tests/scripts/job-member-fixture-contracts.test.ps1', 'tests/scripts/JobMemberWindowsFixture.cs']
PWSH = os.environ.get('KUAIYI_TEST_PWSH', '/tmp/kuaiyi-pwsh/pwsh')


def run(args, cwd=ROOT, env=None, check=True):
    return subprocess.run(args, cwd=cwd, env=env, check=check, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, text=True, timeout=25)


class JobMemberFixtureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = yaml.load((ROOT / WORKFLOW).read_text(), Loader=yaml.BaseLoader)

    def test_single_push_job_and_exact_source_limits(self):
        w = self.workflow
        self.assertEqual(w['on'], {'push': {'branches': [BRANCH], 'paths': [WORKFLOW]}})
        self.assertEqual(w['permissions'], {'contents': 'read'})
        self.assertEqual(list(w['jobs']), ['owned-job-members-only'])
        job = w['jobs']['owned-job-members-only']
        self.assertEqual((job['runs-on'], job['timeout-minutes']), ('windows-2022', '5'))
        self.assertEqual(len(job['steps']), 3)
        checkout, source, fixture = job['steps']
        self.assertEqual(checkout['uses'], 'actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803')
        self.assertEqual(checkout['with'], {'ref': '${{ github.sha }}', 'fetch-depth': '2', 'persist-credentials': 'false', 'set-safe-directory': 'false'})
        self.assertEqual(source['timeout-minutes'], '1')
        self.assertEqual(fixture['timeout-minutes'], '2')
        self.assertEqual(fixture['shell'], 'pwsh -NoLogo -NoProfile -NonInteractive -File "{0}"')
        for locked in (PARENT, TREE, HELPER):
            self.assertIn(locked, source['run'])
        for path in FILES:
            self.assertIn("'" + path + "'", source['run'])
        self.assertIn('exit $LASTEXITCODE', fixture['run'])
        self.assertNotRegex((ROOT / WORKFLOW).read_text(), 'workflow_dispatch|schedule:|matrix:|rerun|upload-artifact|setup-python|setup-dotnet|run-configure-only|run-lifecycle')

    def test_only_new_workflow_matches_new_branch_and_path(self):
        matching = []
        for path in (ROOT / '.github/workflows').glob('*.yml'):
            w = yaml.load(path.read_text(), Loader=yaml.BaseLoader)
            trigger = w['on']['push']
            if BRANCH in trigger['branches'] and WORKFLOW in trigger['paths']:
                matching.append(path.name)
            if path.name != 'windows-job-member-fixture.yml':
                self.assertNotIn(BRANCH, trigger['branches'])
        self.assertEqual(matching, ['windows-job-member-fixture.yml'])
        self.assertEqual(len(list((ROOT / '.github/workflows').glob('*.yml'))), 6)

    def test_original_helper_runners_and_workflows_byte_identical(self):
        paths = ['tools/ci/JobMemberDiagnostic.cs', 'tools/ci/LifecycleOperation.cs',
                 'tests/scripts/run-configure-only-windows.ps1', 'tests/scripts/run-lifecycle-windows-ci.ps1']
        paths += ['.github/workflows/' + name for name in ('windows-contracts.yml', 'windows-lifecycle-validation.yml',
                  'windows-configure-files.yml', 'windows-configure-bridge.yml', 'windows-configure-only.yml')]
        for path in paths:
            self.assertEqual((ROOT / path).read_bytes(), subprocess.check_output(['git', 'show', PARENT + ':' + path], cwd=ROOT), path)
        self.assertEqual(run(['git', 'rev-parse', PARENT + ':tools/ci/JobMemberDiagnostic.cs']).stdout.strip(), HELPER)

    def test_real_shallow_source_gate_depth_and_tamper_rejection(self):
        source = self.workflow['jobs']['owned-job-members-only']['steps'][1]['run']
        with tempfile.TemporaryDirectory(prefix='job-member-owned-git-') as temp:
            base = Path(temp)
            bare = base / 'owned.git'
            run(['git', 'clone', '--bare', '--shared', str(ROOT), str(bare)])
            env = os.environ.copy()
            env['GIT_INDEX_FILE'] = str(base / 'owned-index')
            prefix = ['git', '--git-dir=' + str(bare), '--work-tree=' + str(ROOT)]
            run(prefix + ['read-tree', PARENT], env=env)
            run(prefix + ['add', '--', *FILES], env=env)
            tree = run(prefix + ['write-tree'], env=env).stdout.strip()
            candidate = run(['git', '-c', 'user.name=Synthetic', '-c', 'user.email=fixture@example.invalid',
                             'commit-tree', tree, '-p', PARENT, '-m', 'Owned source gate fixture'], cwd=bare).stdout.strip()
            run(['git', 'update-ref', 'refs/heads/candidate', candidate], cwd=bare)
            for depth in (1, 2):
                clone = base / str(depth)
                run(['git', 'clone', '--quiet', '--depth', str(depth), '--single-branch', '--branch', 'candidate', bare.as_uri(), str(clone)])
                script = clone / 'owned-source-gate.ps1'
                script.write_text(source)
                gate_env = os.environ.copy()
                gate_env['GITHUB_SHA'] = candidate
                result = run([PWSH, '-NoLogo', '-NoProfile', '-File', str(script)], cwd=clone, env=gate_env, check=False)
                if depth == 1:
                    self.assertNotEqual(result.returncode, 0)
                else:
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertIn('JobMemberSourceVerified Head=' + candidate + ' Tree=' + tree + ' Parent=' + PARENT, result.stdout)
                    script.write_text(source.replace(HELPER, '0' * 40))
                    rejected = run([PWSH, '-NoProfile', '-File', str(script)], cwd=clone, env=gate_env, check=False)
                    self.assertNotEqual(rejected.returncode, 0)

    def test_no_original_lifecycle_or_external_scope_entry(self):
        fixture = (ROOT / 'tests/scripts/JobMemberWindowsFixture.cs').read_text()
        entry = (ROOT / 'tests/scripts/run-job-member-windows.ps1').read_text()
        self.assertIn('new Child[9]', fixture)
        self.assertIn('i<8?0:1', fixture)
        self.assertIn('SubstitutedOwnedNonMember', fixture)
        self.assertIn('NineBoundInitiallySuspended', fixture)
        self.assertNotRegex(fixture, r'api[.]Resume|extern.*ResumeThread|public bool Resume')
        self.assertNotIn('api.Active(jobs[0])==8&&api.Active(jobs[1])', fixture)
        self.assertIn('bool publish=Status=="Pass"&&CleanupConfirmed&&!BudgetExpired', fixture)
        self.assertIn('cleanup.ElapsedMilliseconds>=5000', fixture)
        self.assertNotRegex(fixture + entry, r'KuaiYiLifecycleOperation|BREAKAWAY|AdjustTokenPrivileges|GetProcesses|Toolhelp|OpenJobObject|DuplicateHandle|cmake|dotnet restore|--build|--supervise')


if __name__ == '__main__':
    unittest.main(verbosity=2)
