import pathlib,json,hashlib,subprocess,collections,xml.etree.ElementTree as E,datetime,shutil
A=pathlib.Path('artifacts/held-context-test-cleanup'); D=pathlib.Path('docs/test-evidence'); prefix='2026-10-17-held-context-originals__'; parent='2219c3b10d69c31af6d8726744b32b042b9125b5'; ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(p,v):pathlib.Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
def tree(p):return E.parse(p).getroot()
def names(p):return collections.Counter(x.attrib['testName'] for x in tree(p).findall('.//t:UnitTestResult',ns))
def counters(p):return tree(p).find('.//t:Counters',ns).attrib
runs=['r02-held-context','r03-provider-full']; frozen=json.loads((A/(runs[0]+'-source.json')).read_text()); target='tests/KuaiYi.Provider.Tests/bin/Release/net10.0/'
assert len(frozen)==32
observations={}
for name in runs:
 meta=json.loads((A/(name+'-executed.json')).read_text()); assert meta['exit']==0 and meta['source']==frozen
 for p,h in frozen.items():assert sha(p)==h==sha(A/(name+'-source__'+p.replace('/','__')+'.txt'))
 observations[name]={p:h for p,h in meta['sha256'].items() if p.startswith(target)};assert len(observations[name])==5
 for p,h in observations[name].items():assert sha(p)==h
assert observations[runs[0]]==observations[runs[1]]
oldnames=names(D/'2026-10-17-screenshot-recapture-originals__r07-provider-full.trx'); currentnames=names(A/'r03-provider-full.trx'); added=currentnames-oldnames
assert sum(oldnames.values())==2298 and sum(added.values())==2 and currentnames==oldnames+added and all('HeldCaptureFinally' in n for n in added)
assert '--filter' not in (A/'r03-provider-full-command.sh').read_text()
manifest=json.loads((A/'parent-manifest.json').read_text()); allowed=['.gitattributes','docs/progress.md','tests/KuaiYi.Provider.Tests/ScreenshotRecaptureRecoveryTests.cs']
assert len(manifest)==4800
for p,h in manifest.items():
 if p not in allowed:assert sha(p)==h,p
oldindexes=['2026-10-17-production-screenshot-translation.json','2026-10-17-screenshot-lease-await-timeout.json','2026-10-17-screenshot-recapture-recovery.json']; oldraw={}
for index in oldindexes:
 for p,record in json.loads((D/index).read_text())['RawEvidence'].items():
  assert sha(p)==record['sha256'];oldraw[p]=record['sha256']
assert len(oldraw)==1003
oldprefix='2026-10-17-screenshot-lease-await-originals__'; budget_names=names(D/(oldprefix+'r01-focused.trx')); assert sum(budget_names.values())==4; needle='IdleDeadlineKeepsPartialTextAndIgnoresNoContentActivity(protocol: AnthropicMessages, activity: "empty")'; idle_runs={}; oldrefs={}
for name in ['r02-provider-full','r03-old-idle-observation','r04-provider-full-observation']:
 path=D/(oldprefix+name+'.trx');root=tree(path); results=root.findall('.//t:UnitTestResult',ns);match=[r for r in results if r.attrib['testName'].endswith(needle)];assert len(match)==1;case=match[0]
 start=datetime.datetime.fromisoformat(case.attrib['startTime']);end=datetime.datetime.fromisoformat(case.attrib['endTime']);overlap=[r for r in results if r is not case and datetime.datetime.fromisoformat(r.attrib['startTime'])<end and datetime.datetime.fromisoformat(r.attrib['endTime'])>start]
 meta_path=D/(oldprefix+name+'-executed.json');meta=json.loads(meta_path.read_text());failure=case.find('t:Output/t:ErrorInfo',ns)
 idle_runs[name]={'counters':counters(path),'case':case.attrib,'failure':{x.tag.split('}')[-1]:x.text for x in failure} if failure is not None else None,'startEndWallMs':(end-start).total_seconds()*1000,'overlappingOtherCases':len(overlap),'overlappingNewBudgetCases':sum(r.attrib['testName'] in budget_names for r in overlap),'execution':{k:meta[k] for k in ['startUtc','endUtc','exit']}}
 for suffix in ['.trx','.log','-executed.json']:
  p=D/(oldprefix+name+suffix);oldrefs[str(p)]={'sha256':sha(p),'bytes':p.stat().st_size}
old_dlls={}
for name in ['r01-focused','r02-provider-full','r03-old-idle-observation','r04-provider-full-observation']:
 meta=json.loads((D/(oldprefix+name+'-executed.json')).read_text()); old_dlls[name]={p:h for p,h in meta['sha256'].items() if p.startswith(target)}
assert len(old_dlls['r02-provider-full'])==5 and all(v==old_dlls['r02-provider-full'] for v in old_dlls.values())
sourcepaths=['tests/KuaiYi.Provider.Tests/PhaseDeadlineTests.cs','tests/KuaiYi.Provider.Tests/PhaseClock.cs','tests/KuaiYi.Provider.Tests/PhaseHttpFixture.cs','src/KuaiYi.Providers/ProviderClient.cs','src/KuaiYi.Providers/ProviderPhaseDeadline.cs','src/KuaiYi.Providers/ProtocolAdapters.cs','src/KuaiYi.Providers/SseReader.cs']; stable={}
for p in sourcepaths:
 h=sha(p);assert hashlib.sha256(subprocess.check_output(['git','show','c775db07384e37f2a4bb48db8b32aefeae3d752e:'+p])).hexdigest()==h;stable[p]=h
erratum_path=D/(oldprefix+'analysis.json');erratum={'originalPath':str(erratum_path),'originalSha256':sha(erratum_path),'field':'sameDllObservation','incorrectProseMember':'NativeHost','actualMapMember':'ImageWorker','correctFive':['Core','ImageWorker','Imaging','Provider.Tests','Providers'],'originalBytesPreserved':True,'shaEqualityUnaffected':True};oldrefs[str(erratum_path)]={'sha256':sha(erratum_path),'bytes':erratum_path.stat().st_size}
idle={'Scope':'Read-only existing source/log/TRX analysis; no new idle diagnostic executions, no production/old idle test/clock edits. New full Provider run belongs to separate test-cleanup regression.','HistoricalCause':'Unknown','ExistingRuns':idle_runs,'TargetDllsSameFourOldRuns':old_dlls,'SevenSourcesIdenticalToC775':stable,'Confirmed':['Historical failure is Job.IsCompleted after a real500ms wait; later reason/PUBLIC/request-count assertions were not reached.','Write ack is server flush only; Started is accepted headers only.','Anthropic message_start is effective; empty text is not effective.','WaitDeadline(2) does not identify which protocol event anchored the active timer. Normal timer Dispose sets Due=long.MaxValue.','Same DLL passing observations do not diagnose or repair historical failure.'],'ReachableButUnprovenSchedule':['fake0: client processes message_start, creates active idle2; PUBLIC not consumed yet.','Test WaitDeadline2 returns for that active message_start timer; test advances fake clock to1.','Client consumes PUBLIC atfake1 and resets actual idle expiry to3. Empty delta does not reset it.','Test advances only tofake2 then expects termination; correct production request is not expired, matching Job incomplete shape. No historical per-event trace proves this schedule occurred.'],'MissingEvidence':['timer identity/due snapshots','last-effective timestamp','client event processing order','runtime stacks','GC/threadpool/CPU timing'],'OverlapInterpretation':'Concurrent case windows are correlation only; cannot assign scheduling cause. No order-change claim. No contemporaneous new budget case is shown to overlap target.','Erratum':erratum,'ProposedFutureMinimalTestFixNotApplied':'Per-Rig completion signal after actual PUBLIC TextDelta enqueue; await that before first fake advance. Keep phase2s/real500ms/production unchanged; add controlled message_start then delayed PUBLIC test. Requires separate parent scope, not implemented here.','RawReferences':oldrefs}
dump(A/'idle-existing-evidence.json',idle)
review='''Read-only independent review /root/high_water_review, 2026-10-17. Transcript receipt, not a tool execution.\nFinal source/test applicability: noP1/P2. Only ScreenshotRecaptureRecoveryTests changes; original guard 10 assertion segments retained verbatim. WithHeldCapture always drains in outerfinally and nestedfinally awaits same originalTask6s. Controlled actual assertion failures false/true yield nativeCalls1/0, same task successful, Capture suspension absent, A/crop/HTTP2 preserved. r02=3P; r03 nofilter2300P/0F/0skip exactly parent2298 names+new2;64 source bindings and each run5 Provider target DLL current hashes match. r01 is xUnit2020 compile rejection without TRX, not red test. Idle schedule is reachable inference, historical causeUnknown; normal disposed timerDueMax. Old prose NativeHost is erratum only, actual map containsImageWorker; old raw bytes must remain. No SDK/network/writes by reviewer. Final sealed packet review separately pending.\n'''
(A/'read-only-source-review.txt').write_text(review)
analysis={'SourceParent':parent,'Branch':'dev/held-context-test-cleanup','Scope':'Test-only held SynchronizationContext cleanup; original production and idle fixtures unchanged.','FrozenSource':frozen,'FinalSourceBindings':64,'Runs':{name:{'counters':counters(A/(name+'.trx')),'trx':str(D/(prefix+name+'.trx'))} for name in runs},'RelevantProviderTargetDlls':observations,'DllScopeNote':'run.py observes other pre-existing selected DLL files; only command target closure ran. Do not treat unrelated observations as executed tests/builds.','NameComparison':{'parent':2298,'new':2,'final':2300,'exactMultisetParentPlusNew':True,'newNames':dict(added)},'CompileAttempt':json.loads((A/'r01-classification.json').read_text()),'NoProductionRedGreen':'No production source changed. New tests intentionally catch actual precondition assertion failures and verify original failure preserved plus cleanup completion; compile rejection is not red-test evidence.','ParentClosureEvidence':{'Core1703':'Reused unchanged2219 parent r08; not rerun here.','AppWorker0WarningsErrors':'Reused unchanged2219 parent r09/r10; not rebuilt here.','index':'docs/test-evidence/2026-10-17-screenshot-recapture-recovery.json'},'Preservation':{'parentTracked':4800,'oldModifiedAfterDocs':allowed,'allOtherParentSameBytes':4797,'priorRawUnchanged':1003,'productionProjectsLocksSpecTraceOldIdleUnchanged':True,'noDeletes':True},'Review':{'Source':'Independent read-only high_water_review noP1/P2, read-only-source-review.txt','Packet':'Pending separate final packet audit'},'Boundaries':['Only existing SDK10.0.401 --no-restore, certificate generation disabled before/after activation.','No fresh idle diagnosis reruns; historical image/Core/WindowsJob causes independentlyUnknown.','Image/classification reuseOff; no model/API/cost/deploy/main/force/new-package/license/credential/security changes.','Skia license pending,403 and Actions prior denial not retried or bypassed.','Windows/WPF visible keyboard/focus/real capture/DPAPIACL/physical250600/model/fullP6 acceptanceNotRun.','Unknown processes/locks/data/logs preserved.']}
dump(A/'analysis.json',analysis)
raw={}
for p in sorted(A.iterdir()):
 if p.is_file():
  destination=D/(prefix+p.name);shutil.copyfile(p,destination);raw[str(destination)]={'sha256':sha(destination),'bytes':destination.stat().st_size,'artifact':str(p)}
index=analysis|{'RawEvidenceCount':len(raw),'RawEvidence':raw};dump(D/'2026-10-17-held-context-test-cleanup.json',index)
idleindex=idle|{'AnalysisOriginal':{'path':str(D/(prefix+'idle-existing-evidence.json')),'sha256':sha(D/(prefix+'idle-existing-evidence.json'))},'SeparateFixtureEvidence':'docs/test-evidence/2026-10-17-held-context-test-cleanup.json'};dump(D/'2026-10-17-idle-timeout-readonly-investigation.json',idleindex)
attrs=pathlib.Path('.gitattributes');text=attrs.read_text();missing=[p+' -text' for p in raw if p+' -text' not in text.splitlines()]
if missing:attrs.write_text(text+'\n# Held-context cleanup exact original bytes\n'+'\n'.join(missing)+'\n')
print(json.dumps({'raw':len(raw),'parent':4800,'otherParentExact':4797,'priorRawExact':1003,'sourceBindings':64,'provider':2300,'newNames':list(added),'idle':{k:{'outcome':v['case']['outcome'],'duration':v['case']['duration'],'wallMs':v['startEndWallMs'],'overlap':v['overlappingOtherCases']} for k,v in idle_runs.items()}},ensure_ascii=False))
