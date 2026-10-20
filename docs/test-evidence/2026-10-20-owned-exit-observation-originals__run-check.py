from pathlib import Path
import datetime,hashlib,importlib.util,json,subprocess,sys,zipfile
sys.dont_write_bytecode=True
repo=Path('/workspace/KuaiYi'); output=repo/'artifacts/owned-exit-observation'; label=sys.argv[1]; expression=sys.argv[2]
assert label in ('r01-original-wait-red','r02-original-wait-green','r03-affected-regression','r04-fixture-settlement-green')
spec=importlib.util.spec_from_file_location('safe_launcher',repo/'scripts/session-sdk.py'); launcher=importlib.util.module_from_spec(spec);spec.loader.exec_module(launcher);sdk,env=launcher.environment(repo)
files=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','src','tests','tools','scripts/session-sdk.py','global.json','Directory.Build.props','Directory.Packages.props','NuGet.Config'],cwd=repo,text=True).splitlines()
files=[name for name in files if Path(name).suffix in ('.cs','.csproj','.props','.targets','.json','.png')]
snapshot=output/(label+'-sources.zip'); hashes={}
with zipfile.ZipFile(snapshot,'x',compression=zipfile.ZIP_DEFLATED) as archive:
 for name in files:
  data=(repo/name).read_bytes(); hashes[name]=hashlib.sha256(data).hexdigest();archive.writestr(name,data)
argv=[str(sdk/'dotnet'),'test','tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj','--no-restore','-c','Release','-m:1','-nr:false','-p:UseSharedCompilation=false','--logger','trx','--results-directory',str(output/label)]
if expression!='ALL':argv+=['--filter',expression]
start=datetime.datetime.now(datetime.timezone.utc).isoformat()
with (output/(label+'.log')).open('xb') as stream:
 try:result=subprocess.run(argv,cwd=repo,env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=180);code=result.returncode
 except subprocess.TimeoutExpired:code='TimedOut_CleanupNotConfirmed'
end=datetime.datetime.now(datetime.timezone.utc).isoformat()
record={'StartUtc':start,'EndUtc':end,'Argv':argv,'Exit':code,'SafeSdk':'10.0.402','SafeHost':'10.0.13','Platform':'Linux','SourceCommitBase':subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip(),'Sources':hashes,'Snapshot':snapshot.name,'SnapshotSha256':hashlib.sha256(snapshot.read_bytes()).hexdigest(),'LogSha256':hashlib.sha256((output/(label+'.log')).read_bytes()).hexdigest(),'WindowsExecution':'NotRun','NoNetworkRestoreRequested':True}
(output/(label+'.json')).write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps({k:v for k,v in record.items() if k!='Sources'},indent=2))
sys.exit(0 if code==0 else 1)
