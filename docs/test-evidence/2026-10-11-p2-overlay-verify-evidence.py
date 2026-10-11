"""Read-only digest verification; historical reviews are read at their signed commits."""
import hashlib
import json
import pathlib
import struct
import subprocess
import xml.etree.ElementTree as ET
import zipfile

root = pathlib.Path.cwd()
summary = json.loads((root / 'docs/test-evidence/2026-10-11-p2-overlay-summary.json').read_text())


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
                assert len(archive.namelist()) == record['files']
                for item in manifest['Files']:
                    verify({'path': item['Path'], 'bytes': item['Bytes'], 'sha256': item['Sha256']}, archive.read(item['Path']))
                for name in record['actualPe32PlusX64Hosts']:
                    data = archive.read(name)
                    offset = struct.unpack_from('<I', data, 0x3c)[0]
                    assert data[:2] == b'MZ' and data[offset:offset+4] == b'PE\0\0', name
                    assert struct.unpack_from('<H', data, offset+4)[0] == 0x8664, name
                    assert struct.unpack_from('<H', data, offset+24)[0] == 0x20b, name
    else:
        result = 'NotRun; artifact absent from this Git checkout'
    archives.append({'archive': record['archive'], 'result': result})
for kind in ('core', 'provider'):
    document = ET.parse(root / summary['offline'][kind+'Trx'])
    counter = next(node for node in document.iter() if node.tag.endswith('}Counters'))
    assert counter.attrib == summary['offline'][kind]
    assert int(counter.get('total')) == int(counter.get('passed')) > 0
    assert int(counter.get('notExecuted')) == int(counter.get('failed')) == 0
document = ET.parse(root / summary['offline']['nodeJunit'])
cases = list(document.iter('testcase'))
assert len(cases) == summary['offline']['nodeTotal'] == summary['offline']['nodePassed']
assert not any(document.iter('failure')) and not any(document.iter('error')) and not any(document.iter('skipped'))
trace = json.loads((root / 'docs/traceability.json').read_text())
assert len(trace['items']) == 176
assert len([i for i in trace['items'] if i['category'] == 'acceptance' and i['validationStatus'] == 'NotRun']) == 106
print(json.dumps({'verified': counts, 'archives': archives, 'offlineDotNet': summary['offline']['total'],
                  'offlineNode': len(cases), 'WindowsRuntime': 'NotRun', 'RemoteGitHubCi': 'NotRun',
                  'P0': 'Blocked', 'P1': 'InProgress', 'P2': 'InProgress'}, indent=2))
