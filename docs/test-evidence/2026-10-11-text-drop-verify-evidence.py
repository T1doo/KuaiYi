"""Read-only verification of current evidence and prior reviews at exact signed commits."""
import collections
import hashlib
import json
import pathlib
import struct
import subprocess
import xml.etree.ElementTree as ET
import zipfile

root = pathlib.Path.cwd()
summary = json.loads((root / 'docs/test-evidence/2026-10-11-text-drop-summary.json').read_text())


def verify(record, data):
    assert len(data) == record['bytes'], record['path']
    assert hashlib.sha256(data).hexdigest() == record['sha256'], record['path']


def git_bytes(commit, path):
    return subprocess.check_output(['git', 'show', commit + ':' + path], cwd=root)


def counters(path):
    return next(n for n in ET.parse(path).iter() if n.tag.endswith('}Counters')).attrib


def names(data):
    return collections.Counter(n.attrib['testName'] for n in ET.fromstring(data).iter() if n.tag.endswith('}UnitTestResult'))


verify(summary['verifier'], (root / summary['verifier']['path']).read_bytes())
counts = {}
for kind in ('sourceDigests', 'evidenceFiles', 'reviewDigests'):
    for record in summary[kind]:
        verify(record, (root / record['path']).read_bytes())
    counts[kind] = len(summary[kind])
for commit, name in summary['historicalSummaries'].items():
    historical = json.loads(git_bytes(commit, name))
    for kind in ('sourceDigests', 'evidenceFiles', 'reviewDigests'):
        for record in historical[kind]:
            verify(record, git_bytes(commit, record['path']))
        counts[commit[:7] + '-' + kind] = len(historical[kind])
archives = []
for record in [summary['archive']] + summary['historicalArchives']:
    path = root / record['archive']
    result = 'NotRun; archive absent from this Git checkout'
    if path.exists():
        verify(dict(record, path=record['archive']), path.read_bytes())
        result = 'Pass'
        if record is summary['archive']:
            with zipfile.ZipFile(path) as archive:
                assert len(archive.namelist()) == record['files'] == 78
                manifest = json.loads(archive.read('review-manifest.json'))
                assert manifest['Installer'] is False and manifest['FrameworkDependent'] is True
                assert manifest['NativeDLLs'] == 'NotIncluded' and manifest['WindowsRuntime'] == 'NotRun'
                for item in manifest['Files']:
                    verify({'path': item['Path'], 'bytes': item['Bytes'], 'sha256': item['Sha256']}, archive.read(item['Path']))
                for name in record['actualPe32PlusX64Hosts']:
                    data = archive.read(name)
                    offset = struct.unpack_from('<I', data, 0x3c)[0]
                    assert data[:2] == b'MZ' and data[offset:offset+4] == b'PE\0\0', name
                    assert struct.unpack_from('<H', data, offset+4)[0] == 0x8664, name
                    assert struct.unpack_from('<H', data, offset+24)[0] == 0x20b, name
                assert not any('Fixture' in name or name.endswith('OleNativeBridge.dll') for name in archive.namelist())
    archives.append({'archive': record['archive'], 'result': result})
base = summary['sourceBaseCommit']
old = json.loads(git_bytes(base, summary['historicalSummaries'][base]))
for kind in ('core', 'provider'):
    current = root / summary['offline'][kind+'Trx']
    assert counters(current) == summary['offline'][kind]
    assert int(counters(current)['total']) == int(counters(current)['passed']) > 0
    assert int(counters(current)['failed']) == int(counters(current)['notExecuted']) == 0
    original_names = names(git_bytes(base, old['offline'][kind+'Trx']))
    current_names = names(current.read_bytes())
    assert not (original_names - current_names)
    assert summary['baselineTestRetention'][kind]['before'] == sum(original_names.values())
    assert summary['baselineTestRetention'][kind]['after'] == sum(current_names.values())
for run in summary['focusedRuns'] + summary['initialFailures']:
    assert counters(root / run['trx']) == run['counters']
for run in summary['focusedRuns']:
    assert run['counters']['failed'] == run['counters']['notExecuted'] == '0'
    assert run['counters']['total'] == run['counters']['passed']
assert [int(r['counters']['failed']) for r in summary['initialFailures']] == [4, 10, 1]
assert summary['focused']['passed'] == summary['focused']['total'] == 144
assert summary['offline']['total'] == 1109
browser = ET.parse(root / summary['offline']['nodeJunit'])
cases = list(browser.iter('testcase'))
assert len(cases) == summary['offline']['nodeTotal'] == summary['offline']['nodePassed'] == 63
assert not any(browser.iter('failure')) and not any(browser.iter('error')) and not any(browser.iter('skipped'))
for record in summary['lockAudit']['files']:
    data = (root / record['path']).read_bytes()
    verify(record, data)
    assert data == git_bytes(base, record['path'])
assert len(summary['lockAudit']['files']) == summary['managedBuild']['projects'] == 26
trace = json.loads((root / 'docs/traceability.json').read_text())
baseline = json.loads(git_bytes(base, 'docs/traceability.json'))
assert len(trace['items']) == 176
assert sum(i['category'] == 'acceptance' and i['validationStatus'] == 'NotRun' for i in trace['items']) == 106
for original, current in zip(baseline['items'], trace['items'], strict=True):
    assert all(original[key] == current[key] for key in ('id', 'category', 'specLine', 'definition'))
    if current['category'] == 'acceptance':
        assert current['validationStatus'] == 'NotRun'
for name in ('SPEC.md', '快译_产品与技术开发规格_v1.0.md'):
    data = (root/name).read_bytes()
    assert hashlib.sha256(data).hexdigest() == summary['environment']['sourceSpecSha256']
    assert len(data.splitlines()) == 1826 and len(data) == 146706
assert summary['sourceBaseCommit'] == 'a870389599a6fe046d3cef62ea14a4647b58bbb1'
assert len(summary['historicalArchives']) == 15 and len(summary['historicalSummaries']) == 11
print(json.dumps({'verified': counts, 'archives': archives, 'offlineDotNet': 1109, 'offlineNode': 63,
                  'WindowsRuntime': 'NotRun', 'RemoteGitHubCi': 'NotRun', 'P0': 'Blocked',
                  'P1': 'InProgress', 'P2': 'InProgress', 'P3': 'InProgress', 'P4': 'NotIntegrated'}, indent=2))
