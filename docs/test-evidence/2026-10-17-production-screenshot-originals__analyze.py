import pathlib,hashlib,json,xml.etree.ElementTree as ET,collections,subprocess,datetime
A=pathlib.Path('artifacts/production-screenshot-translation');N={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def trx(p):
 r=ET.parse(p);return {'path':str(p),'sha256':sha(p),'counters':r.find('.//t:Counters',N).attrib,'names':collections.Counter(e.attrib['testName'] for e in r.findall('.//t:UnitTestResult',N))}
def meta(n):return json.loads((A/(n+'-executed.json')).read_text())
final=['r17-focused-final','r18-provider-full','r19-core-full','r20-app-build','r21-worker-build'];frozen=meta(final[0])['source'];assert len(frozen)==21
for n in final:
 m=meta(n);assert m['exit']==0 and m['source']==frozen
for p,h in frozen.items():assert sha(p)==h
runs={n:trx(A/(n+'.trx')) for n in final[:3]}
prefix='docs/test-evidence/2026-10-17-diagnostic-dispose-originals__'
parentProvider=trx(prefix+'r07-provider-full.trx');parentCore=trx(prefix+'r06-core-full.trx');assert runs[final[1]]['names']==parentProvider['names']+runs[final[0]]['names'];assert runs[final[2]]['names']==parentCore['names']
rg=[]
for red,green,test in [('r04-finally-red','r05-finally-green','ProductionScreenshotTranslationTests.cs'),('r06-clear-red','r07-clear-green','ProductionScreenshotTranslationTests.cs'),('r11-budget-red','r12-budget-green','ProductionScreenshotTranslationTests.cs'),('r15-fallback-red','r16-fallback-green','ProductionScreenshotShellTests.cs')]:
 a,b=meta(red),meta(green);key='tests/KuaiYi.Provider.Tests/'+test;assert a['source'][key]==b['source'][key];rt,gt=trx(A/(red+'.trx')),trx(A/(green+'.trx'));assert rt['names']==gt['names'];rg.append({'red':rt,'green':gt,'sameTestFile':key,'sameTestSha256':a['source'][key],'sameNames':True,'productionSourceDifferences':[p for p in set(a['source'])&set(b['source']) if a['source'][p]!=b['source'][p]]})
parent=json.loads((A/'parent-manifest.json').read_text());changed={p:{'parent':h,'current':sha(p) if pathlib.Path(p).is_file() else None} for p,h in parent.items() if not pathlib.Path(p).is_file() or sha(p)!=h};assert len(changed)==13
assert all(p.startswith('src/KuaiYi.App/') or p=='tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj' for p in changed)
for p in changed:assert p in frozen
builds={}
for n in final[3:]:
 s=(A/(n+'.log')).read_text();assert 'Build succeeded.' in s and '0 Warning(s)' in s and '0 Error(s)' in s
 builds[n]={'exit':0,'warnings':0,'errors':0,'logSha256':sha(A/(n+'.log'))}
# Every execution metadata DLL map is an observation of existing files. Only each target closure was executed or built.
relevant={}
for n in final:
 m=meta(n);root='tests/KuaiYi.Provider.Tests/' if 'focused' in n or 'provider' in n else 'tests/KuaiYi.Core.Tests/' if 'core' in n else 'src/KuaiYi.App/' if 'app' in n else 'src/KuaiYi.ImageWorker/'
 relevant[n]={p:h for p,h in m['sha256'].items() if p.startswith(root)}
ret={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceParent':subprocess.check_output(['git','rev-parse','HEAD']).decode().strip(),'frozenSource':frozen,'finalRuns':runs,'builds':builds,'relevantTargetDllObservations':relevant,'redGreen':rg,'parentComparison':{'providerParent':parentProvider,'coreParent':parentCore,'providerEqualsParentPlusFocusedNames':True,'coreEqualsParentNames':True},'preservationBeforeDocs':{'parentTracked':len(parent),'modified':changed,'unchanged':len(parent)-len(changed),'noDeleted':True},'dllScopeNote':'run.py records all existing selected DLL paths after every run, including unrelated stale outputs; only the per-target closure was executed/built. Other DLL observations never prove those targets ran.'}
(A/'final-analysis.json').write_text(json.dumps(ret,indent=2,ensure_ascii=False)+'\n');print(json.dumps({'sourceFiles':len(frozen),'provider':runs['r18-provider-full']['counters'],'core':runs['r19-core-full']['counters'],'oldModified':len(changed),'oldPreserved':len(parent)-len(changed),'redGreenPairs':len(rg),'builds':builds}))
