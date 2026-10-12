"""Independent byte/test retention audit; never runs a model or a Windows host."""
import collections,hashlib,json,pathlib,struct,subprocess,sys,xml.etree.ElementTree as ET,zipfile
root=pathlib.Path(__file__).resolve().parents[2]
summary=json.loads(pathlib.Path(__file__).with_name('2026-10-12-p4-completion-races-summary.json').read_text())
checks=[]
def record(label,ok):
 checks.append({'check':label,'pass':bool(ok)})
 if not ok:raise AssertionError(label)
def match(item,data):return len(data)==item['bytes'] and hashlib.sha256(data).hexdigest()==item['sha256']
def git(commit,path):return subprocess.check_output(['git','show',commit+':'+path],cwd=root)
def counters(path):return next(i.attrib for i in ET.parse(root/path).iter() if i.tag.endswith('}Counters'))
def names(data):return collections.Counter(i.attrib['testName'] for i in ET.fromstring(data).iter() if i.tag.endswith('}UnitTestResult'))
for group in ('sourceDigests','evidenceFiles','reviewDigests'):
 for item in summary[group]:record(group+' '+item['path'],match(item,(root/item['path']).read_bytes()))
record('verifier bytes',match(summary['verifier'],pathlib.Path(__file__).read_bytes()))
for commit,path in summary['historicalSummaries'].items():
 historic=json.loads(git(commit,path))
 for group in ('sourceDigests','evidenceFiles','reviewDigests'):
  for item in historic.get(group,[]):record(commit+' '+item['path'],match(item,git(commit,item['path'])))
base=summary['sourceBaseCommit'];baseline=json.loads(git(base,summary['baselineSummary']))
for item in baseline['evidenceFiles']+[baseline['verifier']]:
 record('original ccf evidence unchanged '+item['path'],(root/item['path']).read_bytes()==git(base,item['path']))
record('original ccf summary unchanged',(root/summary['baselineSummary']).read_bytes()==git(base,summary['baselineSummary']))
restoration=json.loads((root/summary['currentArchiveRestorationEvidence']).read_text())
record('temporary archive overwrite disclosed and restored',restoration['restoredExactBytes'] is True and restoration['restoredBytes']==baseline['archive']['bytes'] and restoration['restoredSha256']==baseline['archive']['sha256'] and restoration['overwrittenSha256']!=baseline['archive']['sha256'])
originalTests=(root/summary['completionRaceEvidence']['originalTestBody']).read_text()
finalTests=(root/summary['completionRaceEvidence']['productionTest']).read_text()
start='    [Theory][InlineData("headers")]';last='    [Theory][InlineData("connect")]'
record('original41 assertion bodies exact',originalTests[originalTests.index(start):].rsplit('\n}',1)[0].strip()==finalTests[finalTests.index(start):finalTests.index(last)].strip())
for item in summary['originalProductionEvidence']:
 record('original production '+item['originalPath'],match(item,(root/item['path']).read_bytes()) and (root/item['path']).read_bytes()==git(base,item['originalPath']))
for item in summary['historicalArchives']+[summary['archive']]:
 record('archive '+item['archive'],match(item,(root/item['archive']).read_bytes()))
with zipfile.ZipFile(root/summary['archive']['archive']) as z:
 manifest=json.loads(z.read('review-manifest.json'))
 record('complete manifest inventory',set(z.namelist())=={i['Path'] for i in manifest['Files']}|{'review-manifest.json'})
 for item in manifest['Files']:record('published '+item['Path'],match(dict(item,bytes=item['Bytes'],sha256=item['Sha256']),z.read(item['Path'])))
 for name in summary['archive']['actualPe32PlusX64Hosts']:
  data=z.read(name);offset=struct.unpack_from('<I',data,0x3c)[0]
  record('PE '+name,data[:2]==b'MZ' and data[offset:offset+4]==b'PE\0\0' and struct.unpack_from('<H',data,offset+4)[0]==0x8664 and struct.unpack_from('<H',data,offset+24)[0]==0x20b)
 for name,path in [('app/KuaiYi.App.dll','src/KuaiYi.App/bin/Release/net10.0-windows/win-x64/KuaiYi.App.dll'),('app/KuaiYi.Providers.dll','src/KuaiYi.Providers/bin/Release/net10.0/KuaiYi.Providers.dll')]:record('current build '+name,z.read(name)==(root/path).read_bytes())
 deps=json.loads(z.read('app/KuaiYi.App.deps.json'))
 record('production provider/no test transport',any('KuaiYi.Providers' in name for name in deps['libraries']) and not any('MockProvider' in name or '.Tests' in name for name in deps['libraries']))
 record('isolated fixture','test-provider/KuaiYi.MockProvider.dll' in z.namelist() and 'app/KuaiYi.MockProvider.dll' not in z.namelist())
 record('staging not installer',manifest['Installer'] is False and manifest['WindowsRuntime']=='NotRun' and manifest['ModelCalls']=='None' and manifest['FrameworkDependent'] is True)
for kind,expected in [('core',1315),('provider',397)]:
 before=names(git(base,baseline['offline'][kind+'Trx']));after=names((root/summary['offline'][kind+'Trx']).read_bytes());c=counters(summary['offline'][kind+'Trx'])
 record('all baseline tests '+kind,not before-after and sum(after.values())==expected)
 record('full pass '+kind,int(c['total'])==expected and c['passed']==c['total'] and c['failed']=='0' and c['notExecuted']=='0')
focused=counters(summary['focused']['trx']);record('focused94/94',focused['total']=='94' and focused['passed']=='94' and focused['failed']=='0' and focused['notExecuted']=='0')
focusedNames=names((root/summary['focused']['trx']).read_bytes())
record('44 new actual race cases',sum(n for name,n in focusedNames.items() if '.CompletionBoundaryRaceTests.' in name)==44)
for item in summary['initialFailures']:
 c=counters(item['trx']);record('original red '+item['trx'],int(c['failed'])==item['failed'] and int(c['total'])==item['total'])
 red=names((root/item['trx']).read_bytes());record('same red names retained '+item['trx'],not red-focusedNames)
for item in summary['lockAudit']['files']:
 data=(root/item['path']).read_bytes();record('unchanged lock '+item['path'],match(item,data) and data==git(base,item['path']))
record('all 27 locks',len(summary['lockAudit']['files'])==27)
for item in summary['originalTemplatesAudit']:record('unchanged prompt '+item['path'],match(item,(root/item['path']).read_bytes()) and (root/item['path']).read_bytes()==git(base,item['path']))
record('Node63 actual',len(ET.parse(root/summary['offline']['browserXml']).findall('.//testcase'))==63 and not ET.parse(root/summary['offline']['browserXml']).findall('.//failure'))
trace=json.loads((root/'docs/traceability.json').read_text());whole=[i for i in trace['items'] if i['category']=='acceptance']
record('whole106 still NotRun',len(trace['items'])==176 and len(whole)==106 and all(i['validationStatus']=='NotRun' for i in whole))
record('unaltered spec',(root/'SPEC.md').read_bytes()==git(base,'SPEC.md') and (root/'SPEC.md').read_bytes()==(root/'快译_产品与技术开发规格_v1.0.md').read_bytes())
build=(root/summary['managedBuild']['log']).read_text();record('actual full CI/log', 'Built scope Managed with SDK 10.0.401.' in build and 'Actual local offline CI completed.' in build and '    0 Warning(s)' in build and '    0 Error(s)' in build and 'Failed!' not in build)
output={'allPassed':True,'checks':len(checks),'historicalSummaries':len(summary['historicalSummaries']),'archives':len(summary['historicalArchives'])+1,'tests':1712,'newRaceCases':44,'focused':94,'Node':63,'archive':summary['archive'],'Windows':'NotRun','RemoteCi':'NotRun','ModelCalls':'None','ParentAcceptance':'Pending'}
if len(sys.argv)>1:pathlib.Path(sys.argv[1]).write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(output,ensure_ascii=False))
