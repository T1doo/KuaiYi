import sys,pathlib,subprocess,json,datetime,hashlib,os,zipfile
repo=pathlib.Path.cwd();work=repo/'artifacts/session-sdk-initialization';name=sys.argv[1];args=sys.argv[2:]
if not name.replace('-','').replace('_','').isalnum() or (work/(name+'-executed.json')).exists() or (work/(name+'.log')).exists():raise RuntimeError('Run name conflict/invalid; retain originals')
import importlib.util
loader=importlib.util.spec_from_file_location('session_sdk',repo/'scripts/session-sdk.py');setup=importlib.util.module_from_spec(loader);loader.loader.exec_module(setup)
sdk,env=setup.environment(repo,dict(os.environ,NUGET_PACKAGES='/workspace/.kuaiyi-setup/cli-home/.nuget/packages'))
sys.dont_write_bytecode=True
import importlib.util
spec=importlib.util.spec_from_file_location('prep',repo/'scripts/prepare-session-test.py');prep=importlib.util.module_from_spec(spec);spec.loader.exec_module(prep)
old=os.environ.copy();os.environ.update(env)
try:prep.require_safe_sdk(json.loads((repo/'packaging/session-test.json').read_text()))
finally:os.environ.clear();os.environ.update(old)
paths=[]
for base in ['src','tests','tools','packaging']:
 for p in (repo/base).rglob('*'):
  if p.is_file() and not set(p.parts).intersection({'bin','obj','.git'}) and p.suffix in {'.cs','.csproj','.props','.targets','.json'}:paths.append(p)
for name2 in ['global.json','NuGet.Config','Directory.Build.props','Directory.Packages.props','scripts/prepare-session-test.py','scripts/test_prepare_session_test.py','scripts/session-sdk.py','scripts/test_session_sdk.py']:paths.append(repo/name2)
paths=sorted(set(paths));source={p.relative_to(repo).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths};sourcekey=hashlib.sha256(json.dumps(source,sort_keys=True).encode()).hexdigest();snapshot=work/('sources-'+sourcekey+'.zip')
if not snapshot.exists():
 with zipfile.ZipFile(snapshot,'x',compression=zipfile.ZIP_DEFLATED) as z:
  for p in paths:z.write(p,p.relative_to(repo).as_posix())
command=[str(sdk/'dotnet'),*args] if args[0]!='python3' else args
start=datetime.datetime.now(datetime.timezone.utc).isoformat()
with (work/(name+'.log')).open('xb') as stream:result=subprocess.run(command,cwd=repo,env=env,stdout=stream,stderr=subprocess.STDOUT)
after={p.relative_to(repo).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths};outputs={}
for base in ['src','tests','tools']:
 for p in (repo/base).rglob('KuaiYi*.dll'):
  if 'bin' in p.parts and 'Release' in p.parts:outputs[p.relative_to(repo).as_posix()]=hashlib.sha256(p.read_bytes()).hexdigest()
log=work/(name+'.log');record={'StartUtc':start,'EndUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'Argv':command,'Exit':result.returncode,'SafeSdk':'10.0.402','SafeHost':'10.0.13','NonSensitiveEnvironment':{k:env.get(k) for k in ['HOME','DOTNET_ROOT','DOTNET_HOST_PATH','DOTNET_CLI_HOME','XDG_RUNTIME_DIR','XDG_DATA_HOME','NUGET_PACKAGES','DOTNET_GENERATE_ASPNET_CERTIFICATE','EnableWindowsTargeting','NUGET_CERT_REVOCATION_MODE','DOTNET_NUGET_SIGNATURE_VERIFICATION']},'SourceSnapshot':snapshot.name,'SourceSnapshotSha256':hashlib.sha256(snapshot.read_bytes()).hexdigest(),'Sources':source,'SourceUnchanged':source==after,'Log':log.name,'LogSha256':hashlib.sha256(log.read_bytes()).hexdigest(),'OutputDllsSha256':outputs};(work/(name+'-executed.json')).write_text(json.dumps(record,indent=2)+'\n');print(json.dumps({'Run':name,'Exit':result.returncode,'SourceUnchanged':source==after}));sys.exit(result.returncode)
