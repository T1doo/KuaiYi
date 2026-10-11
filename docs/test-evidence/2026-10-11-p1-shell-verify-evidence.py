"""Read-only freeze verification; old review files always read at their signed Git commit."""
import hashlib
import json
import pathlib
import subprocess
import zipfile

root = pathlib.Path.cwd()
summary = json.loads((root / 'docs/test-evidence/2026-10-11-p1-shell-summary.json').read_text())

def verify(record, data):
    assert len(data) == record['bytes'], record['path']
    assert hashlib.sha256(data).hexdigest() == record['sha256'], record['path']

verify(summary['verifier'], (root / summary['verifier']['path']).read_bytes())
counts = {}
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
        if record is summary['archive']:
            with zipfile.ZipFile(path) as archive:
                manifest = json.loads(archive.read('review-manifest.json'))
                for item in manifest['Files']:
                    verify({'path': item['Path'], 'bytes': item['Bytes'], 'sha256': item['Sha256']}, archive.read(item['Path']))
    else:
        result = 'NotRun; artifact absent from this Git checkout'
    archives.append({'archive': record['archive'], 'result': result})
trace = json.loads((root / 'docs/traceability.json').read_text())
assert len(trace['items']) == 176
assert len([i for i in trace['items'] if i['category'] == 'acceptance' and i['validationStatus'] == 'NotRun']) == 106
print(json.dumps({'verified': counts, 'archives': archives, 'WindowsRuntime': 'NotRun', 'P0': 'Blocked', 'P1': 'InProgress'}, indent=2))
