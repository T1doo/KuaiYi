from pathlib import Path
import importlib.util,json,subprocess,hashlib,time
r=Path('/workspace/KuaiYi');a=Path(__file__).resolve().parent
for name,path in [('sdk',r/'scripts/session-sdk.py'),('prep',r/'scripts/prepare-session-test.py')]:
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);globals()[name]=m
assert (a/'runtime-audit-coverage.json').is_file();root,env=sdk.environment(r);prep.require_signature_environment(env);project='tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj';props=['-p:SelfContained=true','-p:RuntimeFrameworkVersion=10.0.13','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:PublishReadyToRun=false','-p:EnableWindowsTargeting=true','-p:NuGetAudit=true','-p:NuGetAuditMode=all','-p:NuGetAuditLevel=low','-p:TreatWarningsAsErrors=true'];rows=[]
locks={str(p.relative_to(r)):hashlib.sha256(p.read_bytes()).hexdigest() for p in list(r.glob('src/*/packages.lock.json'))+list(r.glob('tools/*/packages.lock.json'))}
assets=json.loads((r/'tools/KuaiYi.GrowthHarness/obj/project.assets.json').read_text())
assert prep.assets_prove_zero_packages(assets,r/project)
assert assets['project']['restore']['projectPath']==str(r/project)
assert set(assets['targets'])=={'net10.0-windows','net10.0-windows/win-x64'}
libs=assets['libraries'];assert len(libs)==8
for target in assets['targets'].values():
 assert set(target)==set(libs)
 assert all(v['type']=='project' for v in target.values())
for path,expected in locks.items():
 original=subprocess.check_output(['git','show','HEAD:'+path],cwd=r)
 assert hashlib.sha256(original).hexdigest()==expected
zero={'kind':'ArtifactOnlyZeroPackageProof','libraries':8,'rootProjects':1,'targets':list(assets['targets']),'allLibraryTypes':'project','assetsSha256':hashlib.sha256((r/'tools/KuaiYi.GrowthHarness/obj/project.assets.json').read_bytes()).hexdigest(),'packageList':'Failed_SequenceContainsNoMatchingElement','productionGateChanged':False,'runtimeCoverage':json.loads((a/'runtime-audit-coverage.json').read_text())}
(a/'growth-zero-package-proof.json').write_text(json.dumps(zero,indent=2)+'\n')
cmd=[str(root/'dotnet'),'publish',project,'--no-restore','-c','Release','--self-contained','true','-o',str(a/'publish/GrowthHarness'),'-m:1','-nr:false','-p:UseSharedCompilation=false',*props];(a/'r06-1-command.json').write_text(json.dumps(cmd,indent=2)+'\n');start=time.monotonic()
with(a/'r06-1.log').open('w')as log:q=subprocess.run(cmd,cwd=r,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=90)
rows.append({'command':cmd,'exit':q.returncode,'elapsedSeconds':round(time.monotonic()-start,3)});after={p:hashlib.sha256((r/p).read_bytes()).hexdigest() for p in locks};result={'commands':rows,'coverage':zero,'lockDrift':locks!=after,'sourceHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip(),'windowsExecution':'NotRun'};(a/'growth-publish-result.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result),flush=True);raise SystemExit(q.returncode or (1 if locks!=after else 0))
