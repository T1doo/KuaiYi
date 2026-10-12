"""Independent script exit-contract evidence/byte audit. No model or Windows execution."""
import hashlib,json,pathlib,subprocess,sys,zipfile
r=pathlib.Path(__file__).resolve().parents[2];s=json.loads(pathlib.Path(__file__).with_name('2026-10-12-p4-alpha-exit-summary.json').read_text());checks=[]
def check(label,value):
 checks.append(label)
 if not value:raise AssertionError(label)
def git(commit,path):return subprocess.check_output(['git','show',commit+':'+path],cwd=r)
def same(item,data):return len(data)==item['bytes'] and hashlib.sha256(data).hexdigest()==item['sha256']
check('verifier frozen bytes',same(s['verifier'],pathlib.Path(__file__).read_bytes()))
for group in ('sourceDigests','reviewDigests','evidenceFiles'):
 for item in s[group]:check(group+' '+item['path'],same(item,(r/item['path']).read_bytes()))
for commit,path in s['historicalSummaries'].items():
 old=json.loads(git(commit,path))
 for group in ('sourceDigests','reviewDigests','evidenceFiles'):
  for item in old.get(group,[]):check(commit+' '+item['path'],same(item,git(commit,item['path'])))
base=s['sourceBaseCommit'];old=json.loads(git(base,s['baselineSummary']))
for item in old['evidenceFiles']+[old['verifier']]:check('original044 evidence '+item['path'],(r/item['path']).read_bytes()==git(base,item['path']))
for item in s['originalProductionEvidence']:check('original source '+item['originalPath'],same(item,(r/item['path']).read_bytes()) and (r/item['path']).read_bytes()==git(base,item['originalPath']))
for item in s['historicalArchives']+[s['patch']]:check('archive '+item['archive'],same(item,(r/item['archive']).read_bytes()))
check('no large archive replaced',s['NoNewLargePackage'] and len(s['historicalArchives'])==32 and s['patch']['bytes']<20000)
with zipfile.ZipFile(r/s['patch']['archive']) as z:
 check('small patch inventory',set(z.namelist())=={i['name'] for i in s['patch']['files']}|{'README.txt'})
 for item in s['patch']['files']:check('patch actual '+item['source'],z.read(item['name'])==(r/item['source']).read_bytes() and hashlib.sha256(z.read(item['name'])).hexdigest()==item['sha256'])
red=json.loads((r/s['red']).read_text());green=json.loads((r/s['green']).read_text())
check('old24 failures',red['Total']==24 and red['Failed']==24 and red['Passed']==0 and sum(i['ActualExit']!=i['ExpectedExit'] for i in red['Cases'])==18)
check('actual24 corrected cases',green['Total']==24 and green['Passed']==24 and green['Failed']==0 and all(i['Pass'] and i['ExpectedExit']==i['ActualExit'] and i['ExpectedKind']==i['ActualKind'] for i in green['Cases']))
check('original assertions retained',[(i['Case'],i['Layer'],i['ExpectedExit'],i['ExpectedKind']) for i in red['Cases']]==[(i['Case'],i['Layer'],i['ExpectedExit'],i['ExpectedKind']) for i in green['Cases']])
check('three production exit layers',{i['Layer'] for i in green['Cases']}=={'launcher','desktop-wrapper','windows-suite-tail'})
check('test-only real child/noWindows',green['WindowsExecuted'] is False and green['ModelCalls']==0 and 'RealOwnedTestChild' in green['Kind'])
for path in s['recoveryFailures']:
 results=json.loads((r/path).read_text());check('original fixture/shape failures '+path,results['Failed']>0)
for key in ('preflight','wrapperPreflight'):
 report=json.loads((r/s[key]).read_text());check('actual '+key,report['Outcome']['Kind']=='PreflightOnly' and report['Outcome']['ExitCode']==0 and report['Outcome']['Status']=='Pass' and report['ProcessesStarted']==0 and report['WindowsExecution']=='NotRun')
check('actual Linux guard refusal','Windows 11 x64 is required' in (r/s['linuxGuard']).read_text())
resume=json.loads((r/s['resume']).read_text());check('one actual recovery',resume['utc'].startswith('2026-10-12T10:06:35') and resume['head']==base and resume['processes']==[] and not resume['duplicatesStarted'] and not resume['permissionsExpanded'])
for group in ('ordinaryUnchanged','alphaSeparate'):
 for item in s['lockAudit'][group]:check('lock unchanged '+item['path'],same(item,(r/item['path']).read_bytes()) and (r/item['path']).read_bytes()==git(base,item['path']))
check('all production source unchanged',all((r/p).read_bytes()==git(base,p) for p in [i['path'] for i in old['sourceDigests'] if i['path'].startswith('src/')]))
output={'allPassed':True,'checks':len(checks),'functionalExitTests':24,'oldExitFailures':18,'historicCommits':len(s['historicalSummaries']),'oldArchivesPreserved':len(s['historicalArchives']),'newLargePackage':False,'patch':s['patch']['archive'],'patchBytes':s['patch']['bytes'],'Windows':'NotRun','Models':'None','ProductionRegression':'044 baseline1836/63 retained, not rerun','ParentAcceptance':'Pending'}
if len(sys.argv)>1:pathlib.Path(sys.argv[1]).write_text(json.dumps(output,indent=2)+'\n')
print(json.dumps(output))
