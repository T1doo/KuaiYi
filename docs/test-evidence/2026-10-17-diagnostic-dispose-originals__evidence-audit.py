import collections,datetime,hashlib,json,pathlib,subprocess,xml.etree.ElementTree as E
R=pathlib.Path('.'); A=R/'artifacts/diagnostic-dispose-settlement'; P='04d6831cd874c13a83ac0df4df3bc6842c3547ee'; NS={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(pathlib.Path(p).read_text())
def trx(p):
 t=E.parse(p); results=t.findall('.//t:UnitTestResult',NS);return {'path':str(p),'sha256':sha(p),'counters':dict(t.find('.//t:Counters',NS).attrib),'times':dict(t.find('.//t:Times',NS).attrib),'results':[dict(x.attrib) for x in results]}
def names(t):return collections.Counter(x['testName'] for x in t['results'])
def count(t,p,f):
 c=t['counters'];assert int(c['passed'])==p and int(c['failed'])==f and int(c['total'])==p+f and int(c['executed'])==p+f
 assert all(int(c[k])==0 for k in ['error','timeout','aborted','notExecuted','pending','inProgress'])
runs={n:trx(A/(n+'.trx')) for n in ['r01-parent-red','r02-green','r03-focused','r05-related','r06-core-full','r07-provider-full']}
for n,p,f in [('r01-parent-red',0,4),('r02-green',4,0),('r03-focused',15,0),('r05-related',66,0),('r06-core-full',1703,0),('r07-provider-full',2212,0)]:count(runs[n],p,f)
assert names(runs['r01-parent-red'])==names(runs['r02-green'])
assert sha(A/'r01-DiagnosticDisposeSettlementTests.cs.txt')==sha(A/'r02-DiagnosticDisposeSettlementTests.cs.txt')
parentHost=subprocess.check_output(['git','show',P+':tools/KuaiYi.SelectionReuseDiagnostic/ReuseDiagnosticHost.cs'])
assert hashlib.sha256(parentHost).hexdigest()==sha(A/'r01-ReuseDiagnosticHost.cs.txt')
red=E.parse(A/'r01-parent-red.trx');failures=[e.text or '' for e in red.findall('.//t:Message',NS)]
assert len(failures)==4 and all('Assert.False() Failure' in m for m in failures)
parentCore=trx(R/'docs/test-evidence/2026-10-17-display-save-originals__r14-core-final.trx');count(parentCore,1688,0)
parentProvider=trx(R/'docs/test-evidence/2026-10-17-display-save-originals__r28-provider-final.trx');count(parentProvider,2212,0)
new=collections.Counter(x['testName'] for x in runs['r06-core-full']['results'] if '.DiagnosticDisposeSettlementTests.' in x['testName'])
assert sum(new.values())==15 and names(runs['r06-core-full'])==names(parentCore)+new
assert names(runs['r07-provider-full'])==names(parentProvider)
assert names(runs['r05-related'])==new+collections.Counter(x['testName'] for x in runs['r06-core-full']['results'] if '.SelectionReuseHostTests.' in x['testName'] or '.HostTerminationTests.' in x['testName'])
target='KuaiYi.Core.Tests.SelectionReuseHostTests.Dispose_during_active_request_retires_and_settles_actual_child'
assert any(x['testName']==target and x['outcome']=='Passed' for x in runs['r06-core-full']['results'])
freeze=read(A/'final-source.json');assert all(sha(p)==s for p,s in freeze.items())
for n in ['r05-related','r06-core-full','r07-provider-full','r08-app-build','r09-worker-build']:
 assert read(A/(n+'-source-binding.json'))==freeze
 assert read(A/(n+'-executed-binding.json'))['exit']==0
 cmd=(A/(n+'-command.sh')).read_text();assert cmd.count('export DOTNET_GENERATE_ASPNET_CERTIFICATE=false')==2 and '--no-restore' in cmd
 if n.endswith('full'):assert '--filter' not in cmd
 if n.endswith('build'):
  log=(A/(n+'.log')).read_text();assert '0 Warning(s)' in log and '0 Error(s)' in log and 'Build succeeded.' in log
bindings={n:read(A/(n+'-executed-binding.json')) for n in ['r01','r02','r03','r05-related','r06-core-full','r07-provider-full','r08-app-build','r09-worker-build']}
b1=bindings['r01']['sha256'];b2=bindings['r02']['sha256'];dllComparison={p:{'red':s,'green':b2[p],'equal':s==b2[p]} for p,s in b1.items()}
assert dllComparison['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.Core.dll']['equal']
assert not dllComparison['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.Core.Tests.dll']['equal']
assert not dllComparison['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.SelectionReuseDiagnostic.dll']['equal']
manifest=read(A/'parent-manifest.json');assert len(manifest)==3703
changed=[p for p,s in manifest.items() if not pathlib.Path(p).is_file() or sha(p)!=s]
assert set(changed)<=set(['.gitattributes','docs/progress.md','tools/KuaiYi.SelectionReuseDiagnostic/ReuseDiagnosticHost.cs'])
assert sha('docs/traceability.json')==manifest['docs/traceability.json']
result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'parent':P,'runs':{n:{k:v for k,v in r.items() if k!='results'} for n,r in runs.items()},'parentCore':{k:v for k,v in parentCore.items() if k!='results'},'parentProvider':{k:v for k,v in parentProvider.items() if k!='results'},'newCaseNames':dict(new),'coreExactParentPlus15':True,'providerExactParentMultiset':True,'relatedExact15Plus51Old':True,'originalActiveDisposePassed':True,'redGreenSameFourNamesAndTestSource':True,'redActualParentHostBytes':True,'redMessages':failures,'redGreenExecutedDllComparison':dllComparison,'finalSource':freeze,'bindings':bindings,'parentTracked':len(manifest),'parentChangedAtAudit':changed,'traceabilityExactBytesUnchanged':True,'r04':read(A/'r04-command-failure.json')}
(A/'evidence-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'runs':{n:r['counters'] for n,r in runs.items()},'parentChanged':changed,'allAssertionsPassed':True},ensure_ascii=False))
