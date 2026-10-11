"""Read-only evidence verification from repository root; local absent archives are NotRun."""
import hashlib
import json
import pathlib
import subprocess

root = pathlib.Path.cwd()
summary = json.loads((root / 'docs/test-evidence/2026-10-11-settings-summary.json').read_text())

def verify(record, data):
    assert len(data) == record['bytes'], record['path']
    assert hashlib.sha256(data).hexdigest() == record['sha256'], record['path']

counts = {}
for kind in ('sourceDigests', 'evidenceFiles', 'reviewDigests'):
    for record in summary[kind]:
        verify(record, (root / record['path']).read_bytes())
    counts[kind] = len(summary[kind])

baseline = json.loads((root / 'docs/test-evidence/2026-10-11-ole-lifetime-audit-summary.json').read_text())
for kind in ('sourceDigests', 'evidenceFiles', 'reviewDigests'):
    for record in baseline[kind]:
        data = subprocess.check_output(['git', 'show', summary['sourceBaseCommit'] + ':' + record['path']], cwd=root)
        verify(record, data)
    counts['baseline-' + kind] = len(baseline[kind])

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
