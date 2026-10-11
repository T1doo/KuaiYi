"""Read-only current evidence checks; historical sources are read from their exact Git commits."""
import hashlib
import json
import pathlib
import subprocess

root = pathlib.Path.cwd()
summary = json.loads((root / 'docs/test-evidence/2026-10-11-settings-bindings-summary.json').read_text())

def verify(record, data):
    assert len(data) == record['bytes'], record['path']
    assert hashlib.sha256(data).hexdigest() == record['sha256'], record['path']

counts = {}
verify(summary['verifier'], (root / summary['verifier']['path']).read_bytes())
for kind in ('sourceDigests', 'evidenceFiles', 'reviewDigests'):
    for record in summary[kind]:
        verify(record, (root / record['path']).read_bytes())
    counts[kind] = len(summary[kind])

for commit, name in summary['historicalSummaries'].items():
    historical = json.loads(subprocess.check_output(['git', 'show', commit + ':' + name], cwd=root))
    for kind in ('sourceDigests', 'evidenceFiles', 'reviewDigests'):
        for record in historical[kind]:
            verify(record, subprocess.check_output(['git', 'show', commit + ':' + record['path']], cwd=root))
        counts[commit[:7] + '-' + kind] = len(historical[kind])

archives = []
for record in [summary['archive']] + summary['historicalArchives']:
    path = root / record['archive']
    if path.exists():
        verify(dict(record, path=record['archive']), path.read_bytes())
        result = 'Pass'
    else:
        result = 'NotRun; local artifact absent from Git checkout'
    archives.append({'archive': record['archive'], 'result': result})

print(json.dumps({'verified': counts, 'archives': archives, 'WindowsRuntime': 'NotRun', 'P0': 'Blocked', 'P1': 'InProgress'}, indent=2))
