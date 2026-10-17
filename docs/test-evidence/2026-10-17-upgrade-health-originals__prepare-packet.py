import pathlib,hashlib,json,collections,xml.etree.ElementTree as E,re,subprocess
A=pathlib.Path(__file__).parent
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
def names(p):return collections.Counter(x.attrib['testName'] for x in E.parse(p).getroot().findall('.//t:UnitTestResult',ns))
def results(p,outcome):return collections.Counter(x.attrib['testName'] for x in E.parse(p).getroot().findall('.//t:UnitTestResult',ns) if x.attrib['outcome']==outcome)
def method(path,name):
 s=path.read_text();start=s.index('public async Task '+name+'(');opening=s.index('{',start);depth=0;quote=None;escaped=False
 for i in range(opening,len(s)):
  c=s[i]
  if quote:
   if escaped:escaped=False
   elif c=='\\':escaped=True
   elif c==quote:quote=None
  elif c in ['"',"'"]:quote=c
  elif c=='{':depth+=1
  elif c=='}':
   depth-=1
   if depth==0:return s[start:i+1].encode()
 raise ValueError(name)
tests='tests/KuaiYi.Core.Tests/InstallationUpgradeTests.cs'
def captured(run):return A/(run+'-source__'+tests.replace('/','__')+'.txt')
pairs=[('r02-install','r03-late-green',['LateHealthTaskMustSettleBeforeRollbackOrOldApplicationCanStart']),('r05-review-red','r06-review-green',['AnotherCoordinatorCannotStartOrUpgradeOverALiveOwnedApplication','AChildThatAlreadyExitedStillMustSettleItsOriginalStderrBeforePublication']),('r07-cancellation-red','r08-cancellation-green',['BlockingHealthCancellationCallbackCannotHoldTheUpgradeCallerBeyondItsOwnedBudget'])]
redgreen=[]
for red,green,methods in pairs:
 failed=results(A/(red+'.trx'),'Failed');passed=results(A/(green+'.trx'),'Passed');assert failed==passed
 hashes={}
 for m in methods:
  before=method(captured(red),m);assert before==method(captured(green),m)==method(pathlib.Path(tests),m);hashes[m]=hashlib.sha256(before).hexdigest()
 assert not(failed-names(A/'r09-install.trx')) and not(failed-names(A/'r10-core-full.trx'))
 redgreen.append({'red':red,'green':green,'sameNames':dict(failed),'sameMethodBytesThroughFinal':hashes})
assert sum(sum(x['sameNames'].values()) for x in redgreen)==5
assert not (A/'r01-install.trx').exists();assert 'CS0819' in (A/'r01-install.log').read_text()
parent='docs/test-evidence/2026-10-17-install-transaction-originals__r06-core-full.trx'
old=names(parent);now=names(A/'r10-core-full.trx');added=now-old;assert now==old+added and sum(added.values())==27
provider=names('docs/test-evidence/2026-10-17-install-transaction-originals__r07-provider-full.trx');assert provider==names(A/'r11-provider-full.trx')
children={}
for run in ['r09-install','r10-core-full']:
 meta=json.loads((A/(run+'-executed.json')).read_text());driver='tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll';core='tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.Core.dll'
 assert meta['sha256'][core]==meta['sha256']['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.Core.dll']
 lines=[x for r in E.parse(A/(run+'.trx')).getroot().findall('.//t:UnitTestResult',ns) for x in (r.findtext('./t:Output/t:StdOut','',ns)).splitlines() if x.startswith(('PUBLIC_UPGRADE_CHILD','PUBLIC_ADMISSION_CHILD','PUBLIC_OWNER_CRASH','PUBLIC_INSTALL_CHILD'))]
 for line in lines:
  assert re.search(r'driverSha256=([A-F0-9]{64})',line).group(1).lower()==meta['sha256'][driver]
  if line.startswith('PUBLIC_UPGRADE_CHILD'):assert 'IO=Settled' in line and 'stderrBytes=0' in line
 assert sum(x.startswith('PUBLIC_OWNER_CRASH') for x in lines)==1
 children[run]={'recordedChildRows':len(lines),'upgradeRows':sum(x.startswith('PUBLIC_UPGRADE_CHILD') for x in lines),'admissionRows':sum(x.startswith('PUBLIC_ADMISSION_CHILD') for x in lines),'ownerCrashRows':1,'installationCheckpointRows':sum(x.startswith('PUBLIC_INSTALL_CHILD') for x in lines),'driverSha256':meta['sha256'][driver],'driverCoreEqualsExecutedTargetCore':True}
for run in ['r12-app-build','r13-worker-build']:
 log=(A/(run+'.log')).read_text();assert 'Build succeeded.' in log and '0 Warning(s)' in log and '0 Error(s)' in log
assert subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()=='e684fc3d3caa504186447faa25b2c7640b539688'
config={'Parent':'e684fc3d3caa504186447faa25b2c7640b539688','Branch':'dev/upgrade-health-rollback','Prefix':'2026-10-17-upgrade-health-originals__','Index':'2026-10-17-upgrade-health-rollback.json','Scope':'SPEC13.4 / P7-05 controlled owned-role upgrade, health rollback, persistent cooperative admission; not Windows installer delivery','FinalRuns':['r09-install','r10-core-full','r11-provider-full','r12-app-build','r13-worker-build'],'FullRun':'r10-core-full','ParentTrx':parent,'NewTests':27,'AllowedOldModified':['.gitattributes','docs/progress.md','src/KuaiYi.Core/Installation/InstallationStore.cs','tests/KuaiYi.Core.Tests/InstallationTransactionTests.cs','tools/KuaiYi.SettingsStoreFixture/Program.cs'],'SourceReview':'Read-only high_water_review: r09 frozen 47 sources, no remaining P1/P2; packet separate audit pending','Targets':{'r09-install':'tests/KuaiYi.Core.Tests/bin/Release/net10.0/','r10-core-full':'tests/KuaiYi.Core.Tests/bin/Release/net10.0/','r11-provider-full':'tests/KuaiYi.Provider.Tests/bin/Release/net10.0/','r12-app-build':'src/KuaiYi.App/bin/Release/','r13-worker-build':'src/KuaiYi.ImageWorker/bin/Release/'},'AdditionalValidation':{'RedGreen':redgreen,'ProviderSameNames':{'parent':2325,'final':2325,'exactSameMultiset':True},'RecordedOwnedChildren':children,'CompileAttempt':{'run':'r01-install','classification':'New test syntax CS0819; no TRX, not product behavioral red'},'IntermediateRuns':['r02-install','r03-late-green','r04-install','r05-review-red','r06-review-green','r07-cancellation-red','r08-cancellation-green']},'Notes':['Final 5 commands each froze the same 47 selected source files before execution, including fixture Program/project, Core installation and owned-process lifetime sources; no retrofilling.','All 27 added names appear in complete Core; parent 1743 retained unchanged; Provider complete names unchanged.','5 actual candidate behavioral failures: 1 late health retry, 3 cross-controller/natural-exit IO, 1 synchronous cancellation; exact test method bytes retained through green/final.','Synthetic selected-generation driver DLL and dependency closure, actual child exit/stdio IO; placeholder EXE mapping is explicitly test-only.','Real symlink file/directory and installation/data ancestors preserve identical-digest targets; 2 consecutive Recover is pointer/version/data idempotent.','No test removed or weakened; all other parent tracked bytes preserved.'],'Boundaries':['Per-phase budgets, not a whole-operation hard deadline.','No Windows adapter for actual role shutdown, normal product entry admission, visible application health, or healthy process ownership handoff. Dispose terminates only this coordinator own held children, including healthy new app.','Only roles started and held by this coordinator are verified, not all product processes across users/sessions; no unknown PID enumeration or kill.','Fresh controller preserves stale/unknown owner and pending; no PID-based recovery or generic cleanup.','NativeHost absolute generation paths, derived package metadata and registration rollback, identity upgrade, complete payload and Inno installer remain future P7 work.','Normal ShellWindow browser receiver is not wired; existing receiver is in P0 review surface.','Package digest does not establish publisher/signature trust; launch nonce is not a credential.','Atomic replacement/Flush/Exit tests do not prove power-loss or OS registration transaction atomicity; same-account malicious rename not prevented.','Old/failed versions and completed metadata retained without GC; no cumulative disk bound claim.','Actual Windows, DPAPI/ACL, capture, physical250600, model quality, fullP6/K acceptance NotRun; no register/install/sign/deploy/security/credential changes.','Skia4.153.1 four license approvals pending; source403 and previously denied GitHub Actions never retried or bypassed.','Historical image/idle/Core/WindowsJob root causes remain independently Unknown; production image reuse Off.']}
dump(A/'task.json',config)
print(json.dumps({'newTests':sum(added.values()),'redGreenFailures':5,'children':children,'sourceCounts':{r:len(json.loads((A/(r+'-executed.json')).read_text())['source']) for r in config['FinalRuns']}}))
