"""Independent source/history/lock/artifact audit. Does not execute a Windows host."""
import collections,hashlib,json,pathlib,re,subprocess,sys,xml.etree.ElementTree as E
r=pathlib.Path(__file__).resolve().parents[2];s=json.loads(pathlib.Path(__file__).with_name('2026-10-12-p4-text-alpha-summary.json').read_text());checks=[]
def check(label,ok):
 checks.append(label)
 if not ok:raise AssertionError(label)
def git(commit,path):return subprocess.check_output(['git','show',commit+':'+path],cwd=r)
def matches(item,data):return len(data)==item['bytes'] and hashlib.sha256(data).hexdigest()==item['sha256']
def names(data):return collections.Counter(i.attrib['testName'] for i in E.fromstring(data).iter() if i.tag.endswith('}UnitTestResult'))
check('verifier bytes',matches(s['verifier'],pathlib.Path(__file__).read_bytes()))
for group in ('sourceDigests','reviewDigests','evidenceFiles'):
 for item in s[group]:check(group+' '+item['path'],matches(item,(r/item['path']).read_bytes()))
for commit,path in s['historicalSummaries'].items():
 old=json.loads(git(commit,path))
 for group in ('sourceDigests','reviewDigests','evidenceFiles'):
  for item in old.get(group,[]):check(commit+' '+item['path'],matches(item,git(commit,item['path'])))
base=s['sourceBaseCommit'];old=json.loads(git(base,s['baselineSummary']))
for item in old['evidenceFiles']+[old['verifier']]:check('original dc70 evidence unchanged '+item['path'],(r/item['path']).read_bytes()==git(base,item['path']))
check('original dc70 summary unchanged',(r/s['baselineSummary']).read_bytes()==git(base,s['baselineSummary']))
for item in s['historicalArchives']+s['otherAlphaArchives']+[s['archive']]:check('archive '+item['archive'],matches(item,(r/item['archive']).read_bytes()))
for item in s['lockAudit']['ordinaryUnchanged']:check('ordinary lock '+item['path'],matches(item,(r/item['path']).read_bytes()) and (r/item['path']).read_bytes()==git(base,item['path']))
for item in s['lockAudit']['alphaSeparate']:
 check('alpha lock '+item['path'],matches(item,(r/item['path']).read_bytes()))
 name=pathlib.Path(item['path']).name.removesuffix('.packages.lock.json');before=next(i for i in s['lockAudit']['ordinaryUnchanged'] if pathlib.Path(i['path']).parent.name==name)
 original=json.loads((r/before['path']).read_bytes())['dependencies'];new=json.loads((r/item['path']).read_bytes())['dependencies']
 check('original NuGet '+name,all(new[framework][key]==value for framework,entries in original.items() for key,value in entries.items() if value['type']!='Project'))
check('27 ordinary and13 separate Alpha locks',len(s['lockAudit']['ordinaryUnchanged'])==27 and len(s['lockAudit']['alphaSeparate'])==13)
for kind,count in [('core',1315),('provider',521)]:
 data=(r/s['offline'][kind+'Trx']).read_bytes();counts=next(i.attrib for i in E.fromstring(data).iter() if i.tag.endswith('}Counters'))
 check('all original '+kind,names(data)==names(git(base,old['offline'][kind+'Trx'])))
 check('actual full '+kind,int(counts['total'])==count and counts['passed']==counts['total'] and counts['failed']=='0' and counts['notExecuted']=='0')
browser=E.parse(r/s['offline']['browserXml']);check('Node63 actual',len(browser.findall('.//testcase'))==63 and not browser.findall('.//failure'))
proof=json.loads((r/s['resume']).read_text());check('actual recovery retained',proof['utc'].startswith('2026-10-12T05:13:07') and proof['head']==base and proof['processes']==[] and not proof['duplicateStarted'] and not proof['permissionsExpanded'])
first=(r/s['firstRestoreFailure']).read_text();check('first real locked restore refusal retained','NU1004' in first)
validation=json.loads((r/s['packageRefusalExperiments']).read_text());check('9 actual validation/refusal cases',validation['allPassed'] and validation['casesCount']==9 and all(i['pass'] for i in validation['cases']))
guard=(r/s['windowsGuard']).read_text();check('actual Windows guard refusal','requires Windows 11 x64' in guard)
result=subprocess.check_output([sys.executable,str(r/'scripts/verify-text-alpha.py'),str(r/s['archive']['bundle']),'--archive',str(r/s['archive']['archive'])],cwd=r,text=True);artifact=json.loads(result)
check('actual independent published1199 files',artifact['allPassed'] and artifact['files']==1199 and artifact['checks']==6060 and artifact['roles']==6 and artifact['SelfContained'] and artifact['WindowsExecution']=='NotRun')
trace=json.loads((r/'docs/traceability.json').read_text());whole=[i for i in trace['items'] if i['category']=='acceptance'];tasks={i['id'] for i in trace['items'] if i['category']=='task' and re.match(r'P[0-4]-',i['id'])};review=(r/'docs/p4-text-alpha-review.md').read_text();rows=re.findall(r'^\| (P[0-4]-\d\d) \|',review,re.M)
check('complete30 original task comparison',len(rows)==30 and set(rows)==tasks)
check('whole106 remainNotRun',len(trace['items'])==176 and len(whole)==106 and all(i['validationStatus']=='NotRun' for i in whole))
check('unchanged spec',(r/'SPEC.md').read_bytes()==git(base,'SPEC.md') and (r/'SPEC.md').read_bytes()==(r/'快译_产品与技术开发规格_v1.0.md').read_bytes())
check('all original builtin prompts',all((r/f'assets/prompts/{name}@1.txt').read_bytes()==git(base,f'assets/prompts/{name}@1.txt') for name in ('base','faithful','plain','professional','explain','regenerate')))
output={'allPassed':True,'checks':len(checks),'artifactChecks':artifact['checks'],'historicCommits':len(s['historicalSummaries']),'oldArchives':len(s['historicalArchives']),'newAlphaArchives':len(s['otherAlphaArchives'])+1,'Dotnet':1836,'Node':63,'ordinaryLocks':27,'alphaLocks':13,'SelfContained':True,'files':1199,'Windows':'NotRun','Installer':False,'NativeProductDLLs':'NotIncluded','Models':'None','ParentAcceptance':'Pending','archive':s['archive']}
if len(sys.argv)>1:pathlib.Path(sys.argv[1]).write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(output,ensure_ascii=False))
