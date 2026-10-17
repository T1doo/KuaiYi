import pathlib,json,hashlib,collections,xml.etree.ElementTree as E,re,subprocess
A=pathlib.Path(__file__).parent;ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
def results(p,outcome=None):return collections.Counter(x.attrib['testName'] for x in E.parse(p).getroot().findall('.//t:UnitTestResult',ns) if outcome is None or x.attrib['outcome']==outcome)
def method(path,name):
 s=path.read_text();start=s.index('public async Task '+name+'(');opening=s.index('{',start);depth=0;quote=None;escaped=False
 for i in range(opening,len(s)):
  c=s[i]
  if quote:
   if escaped:escaped=False
   elif c=='\\':escaped=True
   elif c==quote:quote=None
  elif c in ['"',"\'"]:quote=c
  elif c=='{':depth+=1
  elif c=='}':
   depth-=1
   if depth==0:return s[start:i+1].encode()
 raise ValueError(name)
tests='tests/KuaiYi.Core.Tests/InstallationHandoffTests.cs'
def captured(run):return A/(run+'-source__'+tests.replace('/','__')+'.txt')
red='r06-contract-red';green='r12-install-green';name='MissingLeasePathIsNeverAcceptedAsProofOfReceiverOwnership'
body=method(captured(red),name);finalbody=method(pathlib.Path(tests),name); greenbody=method(captured(green),name); assert greenbody==finalbody; assert body.replace(b'new Controller(r,"deleted-lock")',b'new Controller(r,"deleted-lock",output)')==finalbody
assert results(A/(red+'.trx'),'Failed')['KuaiYi.Core.Tests.InstallationHandoffTests.'+name]==1
assert results(A/(green+'.trx'),'Passed')['KuaiYi.Core.Tests.InstallationHandoffTests.'+name]==1
parent='docs/test-evidence/2026-10-17-upgrade-health-originals__r10-core-full.trx'
old=results(parent);now=results(A/'r13-core-full.trx');added=now-old;assert now==old+added and sum(added.values())==17
provider=results('docs/test-evidence/2026-10-17-upgrade-health-originals__r11-provider-full.trx');assert provider==results(A/'r14-provider-full.trx')
children={}
for run in ['r12-install-green','r13-core-full']:
 meta=json.loads((A/(run+'-executed.json')).read_text());driver='tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll';core='tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.Core.dll'
 assert meta['sha256'][core]==meta['sha256']['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.Core.dll']
 rows=[line for test in E.parse(A/(run+'.trx')).getroot().findall('.//t:UnitTestResult',ns) for line in test.findtext('./t:Output/t:StdOut','',ns).splitlines()]
 binds=[x for x in rows if x.startswith('PUBLIC_CONTROLLER_ACTUAL_EXIT=')]
 assert len(binds)==16
 for line in binds:
  assert re.search(r'fixtureSha256=([A-F0-9]{64})',line)[1].lower()==meta['sha256'][driver]
  assert re.search(r'coreSha256=([A-F0-9]{64})',line)[1].lower()==meta['sha256'][core]
 selected=[x for x in rows if x.startswith('PUBLIC_SELECTED_HEALTH|')]
 assert selected and all(x.split('|')[2].lower()==meta['sha256'][driver] for x in selected)
 children[run]={'actualControllerBindings':len(binds),'selectedHealthRowsIncludingDuplicateEvidence':len(selected),'fixtureSha256':meta['sha256'][driver],'fixtureCoreEqualsTargetCore':True,'note':'Controller is root-held actual Process; runtime grandchild is never opened by PID. STOP_SELF+pipeEOF+rooted lease OS release are separate cleanup observations, not a root-held grandchild WaitForExit.'}
for run in ['r15-app-build','r16-worker-build']:
 log=(A/(run+'.log')).read_text();assert 'Build succeeded.' in log and '0 Warning(s)' in log and '0 Error(s)' in log
config={'Parent':'0137420b56c75500ba03cce36bbb5f57e2f9d7dd','Branch':'dev/runtime-ownership-handoff','Prefix':'2026-10-17-runtime-handoff-originals__','Index':'2026-10-17-runtime-ownership-handoff.json','Scope':'SPEC13.4 / P7 controlled selected-generation runtime ownership handoff; portable synthetic execution only','FinalRuns':['r12-install-green','r13-core-full','r14-provider-full','r15-app-build','r16-worker-build'],'FullRun':'r13-core-full','ParentTrx':parent,'NewTests':17,'AllowedOldModified':['.gitattributes','docs/progress.md','src/KuaiYi.Core/Installation/InstallationAdmission.cs','src/KuaiYi.Core/Installation/InstallationStore.cs','src/KuaiYi.Core/Installation/InstallationUpgradeCoordinator.cs','src/KuaiYi.Core/OwnedHostTermination.cs','src/KuaiYi.Core/OwnedProcessSession.cs','tests/KuaiYi.Core.Tests/InstallationUpgradeTests.cs','tools/KuaiYi.SettingsStoreFixture/Program.cs'],'SourceReview':'Read-only high_water_review approved final r12 85P, frozen 50-source and DLL binding, no remaining P1/P2; packet separate audit pending','Targets':{'r12-install-green':'tests/KuaiYi.Core.Tests/bin/Release/net10.0/','r13-core-full':'tests/KuaiYi.Core.Tests/bin/Release/net10.0/','r14-provider-full':'tests/KuaiYi.Provider.Tests/bin/Release/net10.0/','r15-app-build':'src/KuaiYi.App/bin/Release/','r16-worker-build':'src/KuaiYi.ImageWorker/bin/Release/'},'AdditionalValidation':{'RedGreen':[{'red':red,'green':green,'sameName':name,'redMethodSha256':hashlib.sha256(body).hexdigest(),'greenFinalMethodSha256':hashlib.sha256(finalbody).hexdigest(),'methodDifference':'Only diagnostic ITestOutputHelper constructor argument added; assertions and behavior contract unchanged'}],'LegacyEntryComparison':{'red':red,'green':green,'method':'HealthyReceiverLivesAfterActualControllerExitAndHasAnExplicitSelfCleanupRecipient','classification':'Explicit new entry comparison: controller mode legacy changed to healthy. Not same-method-bytes product bug red/green.'},'ProviderSameNames':{'parent':2325,'final':2325,'exactSameMultiset':True},'RecordedOwnedChildren':children,'AttemptClassifications':{'r01-build':'Successful core/fixture build before complete fixture','r02-fixture-build':'Successful fixture build','r03-legacy-red':'xUnit2031 analyzer compile failure, no TRX; not behavioral red','r04-legacy-red':'Test pipe AutoFlush before connection failure; not product red','r05-legacy-red':'Legacy contract assertion masked by broken-pipe cleanup; stdout retained, not clean formal red','r06-contract-red':'Two visible actual failures: legacy entry comparison and missing-lock false commit','r07-handoff':'11P2F: original control retry and fixture window blocked','r08-handoff':'12P2F: fixture Console.In async call synchronously blocked independent pipe; fixed by Task.Run','r12-install-green':'16P final feature cases'}},'Notes':['Five final commands froze the same 50 selected source files before execution; no retrofilling.','All parent 1770 Core names retained plus 16 handoff cases and 1 original lifetime-stop join case; Provider names unchanged.','One authority: exact-byte Committed ledger binds owner/receiver/launch/version/current/upgrade. CommitAttempted uncertainty never authorizes Kill or rollback.','Beforecommit: exact own child actual exit, original task and IO settlement precede same-owner lease reclaim/CAS rollback. Aftercommit: live control readers cancelled and original tasks/cancellation work joined; transferred state does not claim exit.','Synthetic receiver explicitly owns cleanup via private pipe and fixture-only 40s bound; controller actual exit then PING and STOP_SELF exercised.','Receiver Abort holds execution lease until OS exit, including Prepared-to-Committed race.','All older tests and parent raw evidence retained unchanged.'],'Boundaries':['No actual Windows role exit, product entry admission, visible application health, Job, handoff adapter, install/registration/signing/identity/NativeHost registration changes.','Healthy runtime and upgrade/owner/handoff records remain after self-exit; no automatic restart, stale-owner recovery, receiver adoption or completed-marker cleanup.','Only controller-started exact Process children are controlled. No PID lookup, kill or process enumeration.','No Windows installer/full payload/derived metadata/registration rollback/login startup/uninstall/K-group acceptance.','No publisher trust from hash, no credential authority from launch correlation, no same-account malicious rename protection or power-loss durability claim.','Per-phase budgets are not a whole-operation hard deadline; historical image/idle/Core/WindowsJob root causes independently Unknown.','Runtime STOP_SELF is receiver self-cleanup with pipe EOF and OS lease release; no root-held grandchild actual WaitForExit claim.','Production image/classification reuse Off; source/password/integrity/cancel/cleanup/250600 gates unchanged; actual WPF keyboard/Windows/DPAPIACL/physical250600/live model/fullP6 acceptance NotRun.','No main/force/deploy/cost/real credentials/permission changes/new package/license acceptance. Skia4.153.1 four licenses pending; source403 and denied GitHub Actions never retried or bypassed.']}
stop='RetryJoinsTheOriginalLifetimeStopTaskAfterActualExitBeforeRestoringTheOldGeneration'; stopt='tests/KuaiYi.Core.Tests/InstallationUpgradeTests.cs'; stopred='r11-stop-task-red'; stopgreen='r12-install-green'
b=method(A/(stopred+'-source__'+stopt.replace('/','__')+'.txt'),stop);assert b==method(A/(stopgreen+'-source__'+stopt.replace('/','__')+'.txt'),stop)==method(pathlib.Path(stopt),stop)
assert results(A/(stopred+'.trx'),'Failed')['KuaiYi.Core.Tests.InstallationUpgradeTests.'+stop]==1
assert results(A/(stopgreen+'.trx'),'Passed')['KuaiYi.Core.Tests.InstallationUpgradeTests.'+stop]==1
config['AdditionalValidation']['RedGreen'].append({'red':stopred,'green':stopgreen,'sameName':stop,'sameMethodBytesThroughFinal':hashlib.sha256(b).hexdigest()})
config['AdditionalValidation']['AttemptClassifications'].update({'r09-handoff':'16P feature before later stop-task correction','r10-core-full':'1785P1F old LateActualStartOwner Retry L151 Busy, OSexit/IO visible; exact cause Unknown','r11-stop-task-red':'Controlled stop task held after actual exit: Retry incorrectly completed early, 1F','r12-install-green':'Final related 85P=parent68+handoff16+stopJoin1'})
oldpath=A/'parent-InstallationUpgradeTests.cs.txt';oldpath.write_bytes(subprocess.check_output(['git','show','0137420b56c75500ba03cce36bbb5f57e2f9d7dd:'+stopt]))
oldmethods=re.findall(r'public async Task (\w+)\(',oldpath.read_text());assert all(method(oldpath,m)==method(pathlib.Path(stopt),m) for m in oldmethods)
config['AdditionalValidation']['OldUpgradeMethodsPreserved']={'count':len(oldmethods),'names':oldmethods,'exactSameMethodBytes':True}
dump(A/'task.json',config)
print(json.dumps({'newTests':sum(added.values()),'finalSources':{r:len(json.loads((A/(r+'-executed.json')).read_text())['source']) for r in config['FinalRuns']},'children':children}))
