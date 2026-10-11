"""Read-only verification, from repo root. Missing local archives are reported, never passed."""
import hashlib
import json
import pathlib
import subprocess

root = pathlib.Path.cwd()
summary_path = root / 'docs/test-evidence/2026-10-11-ole-lifetime-audit-summary.json'
summary = json.loads(summary_path.read_text())

def verify(record, data):
    assert len(data) == record['bytes'], record['path']
    assert hashlib.sha256(data).hexdigest() == record['sha256'], record['path']

counts = {}
for key in ('sourceDigests', 'evidenceFiles', 'reviewDigests'):
    for record in summary[key]:
        verify(record, (root / record['path']).read_bytes())
    counts[key] = len(summary[key])

baseline = json.loads((root / 'docs/test-evidence/2026-10-11-ole-presentation-summary.json').read_text())
for record in baseline['sourceDigests']:
    data = subprocess.check_output(['git', 'show', summary['sourceBaseCommit'] + ':' + record['path']], cwd=root)
    verify(record, data)
for record in baseline['evidenceFiles']:
    verify(record, (root / record['path']).read_bytes())
counts['baselineSourceDigests'] = len(baseline['sourceDigests'])
counts['baselineEvidenceFiles'] = len(baseline['evidenceFiles'])

archives = []
for record in [summary['archive']] + summary['historicalArchives']:
    path = root / record['archive']
    if path.exists():
        verify(dict(record, path=record['archive']), path.read_bytes())
        archives.append({'archive': record['archive'], 'result': 'Pass'})
    else:
        archives.append({'archive': record['archive'], 'result': 'NotRun; local artifact absent from Git checkout'})

print(json.dumps({'verified': counts, 'archives': archives, 'WindowsRuntime': 'NotRun', 'P0': 'Blocked'}, indent=2))
