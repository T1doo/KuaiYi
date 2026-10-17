import collections,hashlib,json,pathlib,subprocess,xml.etree.ElementTree as ET
r=pathlib.Path('artifacts/display-save-stream-continuation');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
sha=lambda b:hashlib.sha256(b).hexdigest()
def load(p):return json.loads(pathlib.Path(p).read_text())
def trx(p):
 root=ET.parse(p).getroot();return {'counters':root.find('.//t:Counters',ns).attrib,'names':collections.Counter(x.attrib['testName'] for x in root.findall('.//t:UnitTestResult',ns)),'outcomes':collections.Counter(x.attrib['outcome'] for x in root.findall('.//t:UnitTestResult',ns))}
def pair(a,b,sa,sb):
 x=trx(r/(a+'.trx'));y=trx(r/(b+'.trx'));assert x['names']==y['names'];assert (r/(sa+'-DisplaySaveStreamTests.cs.txt')).read_bytes()==(r/(sb+'-DisplaySaveStreamTests.cs.txt')).read_bytes()
 def hashes(label):
  d=load(r/(label+'-executed-binding.json'));return d.get('sha256',d)
 ha,hb=hashes(sa),hashes(sb)
 return {'sameTestSourceBytes':True,'sameCaseNames':True,'redCounters':x['counters'],'greenCounters':y['counters'],'sameExecutedBinaries':{f:ha[f]==hb[f] for f in ha}}
parent='528b2ba8175b1014b51301eb653cc84d06dfd9b8';prod='src/KuaiYi.Providers/TextTranslationCoordinator.cs';test='tests/KuaiYi.Provider.Tests/DisplaySaveStreamTests.cs'
assert subprocess.check_output(['git','show',parent+':'+prod])==(r/'r01-TextTranslationCoordinator.cs.txt').read_bytes()
rg1=pair('r01-parent-red','r02-green','r01','r02');rg2=pair('r03-cas-red','r04-cas-green','r03','r04');rg3=pair('r11-ocr-red','r12-ocr-green','r11','r12');rg4=pair('r24-fallback-red','r26-fallback-green','r24','r26');control=trx(r/'r25-parent-fallback-control.trx');assert control['names']==trx(r/'r24-fallback-red.trx')['names'];assert control['outcomes']=={'Passed':2};assert (r/'r25-DisplaySaveStreamTests.cs.txt').read_bytes()==(r/'r24-DisplaySaveStreamTests.cs.txt').read_bytes();assert (r/'r25-TextTranslationCoordinator.cs.txt').read_bytes()==subprocess.check_output(['git','show',parent+':'+prod])
core=trx(r/'r14-core-final.trx');provider=trx(r/'r28-provider-final.trx');focused=trx(r/'r27-final-focused.trx');pc=trx(pathlib.Path('docs/test-evidence/2026-10-17-enable-model-originals__r06-core-full.trx'));pp=trx(pathlib.Path('docs/test-evidence/2026-10-17-enable-model-originals__r07-provider-full.trx'))
assert core['names']==pc['names'];assert provider['names']==pp['names']+focused['names'];assert all(v['outcomes']=={'Passed':sum(v['names'].values())} for v in [core,provider,focused])
frozen=load(r/'r27-final-source.json');assert all(sha(pathlib.Path(f).read_bytes())==h for f,h in frozen.items())
for label in ['r28-provider-final','r29-app-final','r30-worker-final','r27']:
 assert load(r/(label+'-source-binding.json'))==frozen
for label in ['r14-core-final','r28-provider-final','r29-app-final','r30-worker-final']:
 assert load(r/(label+'-executed-binding.json'))['exit']==0
assert load(r/'r14-core-final-source-binding.json')[prod]!=frozen[prod]
assert load(r/'r14-core-final-source-binding.json')[test]!=frozen[test]
assert (r/'r13-DisplaySaveStreamTests.cs.txt').read_text().replace('if(action=="approval")r.Current=false;','if(action=="approval")r.Local!.Clear();')==(r/'r18-DisplaySaveStreamTests.cs.txt').read_text()
original=load(r/'parent-manifest.json');changed=[f for f,h in original.items() if not pathlib.Path(f).is_file() or sha(pathlib.Path(f).read_bytes())!=h]
allowed={prod,'.gitattributes','docs/progress.md','docs/traceability.json'};assert set(changed)<=allowed
oldcore=load(r/'r06-core-full-executed-binding.json')['executedOrBuilt'];newcore=load(r/'r14-core-final-executed-binding.json')['executedOrBuilt'];assert all(newcore[f]==h for f,h in oldcore.items())
post=load(r/'r06-old-chain-postrun-read.json')['sha256'];assert post['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.SelectionReuseDiagnostic.dll']==newcore['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.SelectionReuseDiagnostic.dll']
summary={'Parent':parent,'FrozenSource':frozen,'RedGreen':{'actualSseAfterDisplaySave':rg1,'sameCapturedOwnerProductionPrimitive':rg2,'actualSseOcrPerRequestStyle':rg3,'preservedAbandonFallback':dict(rg4,parentControlCounters=control['counters'],parentProductionBytesExact=True)},'FullRegression':{'Core':core['counters'],'Provider':provider['counters'],'Focused':focused['counters'],'CoreParentExactNameMultiset':True,'ProviderEqualsParentExactNameMultisetPlusFocused55':True},'OldFailurePreservation':{'r06':trx(r/'r06-core-full.trx')['counters'],'r13':trx(r/'r13-final-focused.trx')['counters'],'r23':{'counters':trx(r/'r23-fallback-red.trx')['counters'],'classification':'OpenAI actual product fallback regression; Anthropic illegal usage while content block active caused first A incomplete, fixture failure, not valid double-protocol product red'},'r06ToR14SameActualCoreBinaries':{f:True for f in oldcore},'r06PostRunDiagnosticDllEqualsR14':True,'RootCause':'Unknown; later pass does not prove repair. No production/test change to that old chain.'},'CoreDependencyBoundary':'r14 source record precedes both Provider-only test-action and later Providers permanent-denial corrections. Whole Core dependency closure has no Providers/Provider.Tests project; all its sources/projects/locks unchanged. r14 Providers and new test source hashes are not final; applicable Core tests reused, final Provider/App rebuilt and tested on new source.','ParentTracked':len(original),'ChangedExisting':changed,'AllOtherParentTrackedExactBytes':True,'Builds':{'App':{'exit':0,'warnings':0,'errors':0},'ImageWorker':{'exit':0,'warnings':0,'errors':0}},'PreparedOnlyNotRun':['r08-app-build','r09-worker-build']}

import xml.etree.ElementTree as X
closure=set()
def visit(p):
 p=p.resolve()
 if p in closure:return
 closure.add(p)
 for ref in X.parse(p).findall('.//ProjectReference'):
  visit(p.parent/ref.attrib['Include'])
visit(pathlib.Path('tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj'))
assert len(closure)==14
assert not any(p.parts[-2] in ['KuaiYi.Providers','KuaiYi.Provider.Tests'] for p in closure)
summary['CoreProjectClosure']=[str(p.relative_to(pathlib.Path.cwd())) for p in sorted(closure)]
checkpoint=load(r/'r22-pre-fallback-2026-10-17-display-save-stream-continuation.json.txt')
assert len(checkpoint['RawEvidence'])==130
for e in checkpoint['RawEvidence']:
 assert sha(pathlib.Path(e['original']).read_bytes())==sha(pathlib.Path(e['path']).read_bytes())==e['sha256']
summary['EarlierSealedCheckpoint']={'raw':130,'stagedPaths':137,'allRawBytesStillExact':True,'outcome':'Not committed or pushed; final review paused on proven new Abandon fallback regression, then corrected and revalidated. Prior evidence preserved.'}
(r/'r31-validation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'Core':core['counters'],'Provider':provider['counters'],'Focused':focused['counters'],'ChangedExisting':changed,'ParentTracked':len(original)},ensure_ascii=False))
