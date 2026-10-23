"""One bounded native payload build on the existing public standard Windows CI; no installation."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid

PARENT = '7312d1f630c1b5ad3697810460db8b4f37fe1bbc'
FILES = {'.github/workflows/windows-warm-components.yml', 'scripts/build-payload-native-windows.py',
         'scripts/build-payload-native-windows.ps1', 'docs/validation/native-payload-build.md', 'docs/progress.md'}


def source_gate(repo, env, event):
    required = {'GITHUB_ACTIONS': 'true', 'GITHUB_REPOSITORY': 'T1doo/KuaiYi',
                'GITHUB_REF': 'refs/heads/dev/p7-fixed-signoff', 'GITHUB_EVENT_NAME': 'push',
                'GITHUB_RUN_ATTEMPT': '1', 'RUNNER_OS': 'Windows'}
    if sys.platform != 'win32' or any(env.get(k) != v for k, v in required.items()) or event.get('repository', {}).get('private') is not False:
        raise RuntimeError('Exact public one-attempt standard Windows context required')
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=repo, text=True, timeout=15).strip()
    head = git('rev-parse', 'HEAD')
    if head != env.get('GITHUB_SHA') or git('rev-list', '--parents', '-n', '1', 'HEAD').split() != [head, PARENT]:
        raise RuntimeError('Exact reviewed parent required')
    if set(git('diff', '--name-only', PARENT, head).splitlines()) != FILES or git('status', '--porcelain=v1'):
        raise RuntimeError('Exact clean source scope required')
    return head


def main():
    repo = Path(__file__).resolve().parent.parent
    spec = importlib.util.spec_from_file_location('original_windows_evidence', repo / 'scripts/verify-session-runtime-windows.py')
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    report = {'Kind': 'FourNativePayloadBuild', 'Status': 'Blocked', 'Commands': [], 'WindowsAppAcceptance': 'NotRun',
              'ProductionPins': 'Unavailable', 'ModelCalls': 0, 'Cleanup': 'NotStarted'}
    work = None
    code = 2
    try:
        with open(os.environ['GITHUB_EVENT_PATH'], encoding='utf-8') as f:
            event = json.load(f)
        report['SourceHead'] = source_gate(repo, os.environ, event)
        work = repo / 'artifacts/native-payload-windows' / uuid.uuid4().hex
        if any(helper.linked(p) for p in (work, *work.parents)):
            raise RuntimeError('Linked output refused')
        work.mkdir(parents=True, exist_ok=False)
        command = ['pwsh', '-NoProfile', '-NonInteractive', '-File', str(repo / 'scripts/build-payload-native-windows.ps1'), '-Output', str(work)]
        report['Commands'].append({'Name': 'native-build', 'Argv': command, 'Log': 'native-build.log', 'Exit': None})
        with (work / 'native-build.log').open('xb') as output:
            result = subprocess.run(command, cwd=repo, stdout=output, stderr=subprocess.STDOUT, timeout=12 * 60)
        report['Commands'][0]['Exit'] = result.returncode
        report['Cleanup'] = 'OriginalPowerShellExited_NotDesktopLifecycleAcceptance'
        report['Status'] = 'Pass' if result.returncode == 0 else 'Fail'
        code = result.returncode
    except subprocess.TimeoutExpired:
        report.update(Status='Blocked', Reason='NativeDeadlineExceeded', Cleanup='OriginalChildTreeExitUnconfirmed_NoSuccessClaim')
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as error:
        report.update(Status='Blocked', Reason=str(error))
    finally:
        if work is not None and work.is_dir():
            # Exact fixture outputs only, bounded by the original emitter. No artifact/cache upload.
            for path in sorted(work.iterdir()):
                if path.is_file() and path.name != 'native-build.log' and (path.suffix in ('.log', '.json', '.xml') or path.name in {
                        'KuaiYi.CaptureNative.dll', 'KuaiYi.OcrNative.dll', 'KuaiYi.OleNativeBridge.dll', 'KuaiYi.ShellExtension.dll'}):
                    report['Commands'].append({'Name': path.name, 'Log': path.name, 'EvidenceOnly': True})
            helper.emit_evidence(work, report)
            if report['EvidenceStatus'] != 'EmittedToJobLog':
                report['Status'] = 'Blocked'
                code = 2
        print('KUAIYI_NATIVE_PAYLOAD ' + json.dumps(report), flush=True)
    return code


if __name__ == '__main__':
    raise SystemExit(main())
