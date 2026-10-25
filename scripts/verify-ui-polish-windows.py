"""One explicitly approved public Windows visual attempt; no retries or product install."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import contextlib
import unicodedata
import time
import urllib.request

PARENT='11f13f5ebc4fcb812d98586c5f594ca3a2633990'
SIDES={'before':'e4d57e77426ab235c7219b375debd76bc072e00b','after':'419082f9863abf109a3d9ee7274dde59697bb85a'}
BRANCH='refs/heads/dev/ui-polish-visual-r02'
FILES={'.github/workflows/windows-ui-polish.yml','scripts/verify-ui-polish-windows.py','tools/KuaiYi.GrowthHarness/UiPolishContract.cs','docs/progress.md','docs/validation/ui-polish-windows-r02-preparation.md'}
REPO=Path(__file__).resolve().parent.parent
WORK=REPO/'artifacts/ui-polish-windows'

def load(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()

MAX_FILES=20000
MAX_BLOB=64*1024*1024
MAX_TOTAL=1024*1024*1024

def tree_entries(raw):
 if not raw or not raw.endswith(b'\0'):raise RuntimeError('Incomplete NUL tree')
 entries=[];seen=set()
 for row in raw[:-1].split(b'\0'):
  meta,name=row.split(b'\t',1);mode,kind,oid=meta.split(b' ')
  if mode not in (b'100644',b'100755') or kind!=b'blob' or not re.fullmatch(b'[0-9a-f]{40}',oid):raise RuntimeError('Only original regular blob source')
  path=name.decode('utf-8',errors='strict');parts=path.split('/')
  # Windows destination policy; paths are decoded only after NUL framing.
  if any(not x or x in ('.','..') or x[-1] in ' .' or any(ord(c)<32 or ord(c)==127 or c in '<>:"\\|?*' for c in x) or x.split('.')[0].upper() in {'CON','PRN','AUX','NUL',*[f'COM{i}' for i in (*range(1,10),'¹','²','³')],*[f'LPT{i}' for i in (*range(1,10),'¹','²','³')]} for x in parts):raise RuntimeError('Windows source path refused')
  if unicodedata.normalize('NFC',path)!=path:raise RuntimeError('Noncanonical source path refused')
  key=path.casefold()
  if key in seen:raise RuntimeError('Duplicate Windows source path')
  seen.add(key);entries.append((path,oid.decode()))
  if len(entries)>MAX_FILES:raise RuntimeError('Source file count bound')
 keys=set(seen)
 if any('/'.join(p.split('/')[:i]).casefold() in keys for p,_ in entries for i in range(1,len(p.split('/')))):raise RuntimeError('Source file/directory collision')
 return entries

def blob_sizes(raw,entries):
 rows=raw.splitlines()
 if len(rows)!=len(entries):raise RuntimeError('Blob inventory count differs')
 sizes=[];total=0
 for row,(_,oid) in zip(rows,entries):
  fields=row.split(b' ')
  if len(fields)!=3 or fields[:2]!=[oid.encode(),b'blob'] or not fields[2].isdigit():raise RuntimeError('Blob inventory source/type differs')
  size=int(fields[2]);sizes.append(size);total+=size
  if size>MAX_BLOB or total>MAX_TOTAL:raise RuntimeError('Source size bound')
 return sizes

def write_blobs(pack,root,entries,sizes):
 if len(entries)!=len(sizes):raise RuntimeError('Source inventory differs')
 for ancestor in (root,*root.parents):
  if ancestor.is_symlink() or (ancestor.exists() and getattr(ancestor.stat(),'st_file_attributes',0)&0x400):raise RuntimeError('Linked source destination')
 root.mkdir(exist_ok=False)
 manifest={}
 with pack.open('rb') as stream:
  for (name,oid),size in zip(entries,sizes):
   if stream.readline(128)!=f'{oid} blob {size}\n'.encode():raise RuntimeError('Original blob header differs')
   path=root/name;path.parent.mkdir(parents=True,exist_ok=True)
   sha1=hashlib.sha1(f'blob {size}\0'.encode());sha256=hashlib.sha256();remaining=size
   with path.open('xb') as out:
    while remaining:
     block=stream.read(min(remaining,1024*1024))
     if not block:raise RuntimeError('Original blob truncated')
     out.write(block);sha1.update(block);sha256.update(block);remaining-=len(block)
   if stream.read(1)!=b'\n' or sha1.hexdigest()!=oid:raise RuntimeError('Git blob differs')
   manifest[name]=sha256.hexdigest()
  if stream.read(1):raise RuntimeError('Unexpected trailing blob data')
 return manifest

def context(env,platform,event):
 required={'GITHUB_ACTIONS':'true','GITHUB_REPOSITORY':'T1doo/KuaiYi','GITHUB_REF':BRANCH,'GITHUB_EVENT_NAME':'push','GITHUB_RUN_ATTEMPT':'1','RUNNER_OS':'Windows'}
 if platform!='win32' or any(env.get(k)!=v for k,v in required.items()) or event.get('repository',{}).get('private') is not False:raise RuntimeError('Exact public first-attempt Windows context required')
 if not re.fullmatch('[0-9a-f]{40}',env.get('GITHUB_SHA','')):raise RuntimeError('Full delivery SHA required')

def verify_result(directory,side,view,app_hash):
 path=directory/'ui-polish-result.json'
 if path.stat().st_size>512*1024:raise RuntimeError('Result bound')
 result=json.loads(path.read_text(encoding='utf-8'))
 if any(result.get(k)!=v for k,v in {'Status':'Pass_LimitedWpfRaster','Side':side,'View':view,'ProductHead':SIDES[side],'AppDllSha256':app_hash,'TimedOut':False,'CloseConfirmed':True,'ResourcesSettled':True,'StateRetained':True,'Failures':[]}.items()):raise RuntimeError('Actual source/resource result refused')
 scenes=result['Scenes'];expected=8 if view=='settings' else 18
 if len(scenes)!=expected or len({s['File'] for s in scenes})!=expected:raise RuntimeError('Complete unique scene collection required')
 total=0
 for s in scenes:
  name=s['File']
  if not re.fullmatch('[A-Za-z0-9-]+\\.png',name) or s.get('StateCoherent') is not True or s.get('Side')!=side or s.get('ProductHead')!=SIDES[side] or s.get('AppDllSha256')!=app_hash:raise RuntimeError('Scene identity mismatch')
  p=directory/name
  if p.is_symlink() or p.stat().st_size>2*1024*1024:raise RuntimeError('PNG bound/link')
  b=p.read_bytes();total+=len(b)
  if len(b)!=s['Bytes'] or hashlib.sha256(b).hexdigest()!=s['Sha256'] or b[:8]!=b'\x89PNG\r\n\x1a\n' or int.from_bytes(b[16:20],'big')!=s['PixelsX'] or int.from_bytes(b[20:24],'big')!=s['PixelsY']:raise RuntimeError('Actual PNG mismatch')
 if total>16*1024*1024 or {p.name for p in directory.glob('*.png')}!={s['File'] for s in scenes}:raise RuntimeError('PNG collection/aggregate differs')
 if view=='overlay':
  trays=[x for x in result['Retirements'] if 'TrayRemoved' in x]
  tasks=[x for x in result['Retirements'] if 'AllOriginalTasksCompleted' in x]
  if len(tasks)!=8 or any(x.get('AllOriginalTasksCompleted') is not True or x.get('Runs')!=1 or x.get('Connectors')!=1 or x.get('Attempts')!=1 for x in tasks):raise RuntimeError('Original fixture task settlement differs')
  if len(trays)!=8 or any(x!={'OwnWindowClosed':True,'TrayDisposed':True,'TrayRemoved':True} for x in trays):raise RuntimeError('Original tray cleanup not confirmed')
 return result

def main(phase):
 event=json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'));context(os.environ,sys.platform,event)
 helper=load('visual_original_helpers',REPO/'scripts/verify-session-runtime-windows.py')
 old=load('visual_original_layout_utilities',REPO/'scripts/verify-settings-layout-windows.py')
 policy=helper.load_policy(REPO);policy.require_signature_environment(os.environ)
 report={'Phase':phase,'Status':'Blocked','DeliverySha':os.environ['GITHUB_SHA'],'ProductHeads':SIDES,'Commands':[],'ModelCalls':0,'RegistryWrites':0,'SystemDpiThemeMotionChanges':0}
 deadline=time.monotonic()+(8*60 if phase=='prepare' else 6*60)
 def save(name,value):
  (WORK/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
 def run(argv,name,timeout=120,*,binary=False,input_path=None,limit=4*1024*1024):
  remaining=deadline-time.monotonic()
  if remaining<=0:raise RuntimeError('Shared phase budget exhausted')
  log=WORK/(phase+'-'+name+('.bin' if binary else '.log'));row={'Name':name,'Argv':argv,'StartUnix':time.time(),'Exit':None};report['Commands'].append(row)
  try:
   with contextlib.ExitStack() as stack:
    out=stack.enter_context(log.open('xb'))
    source=stack.enter_context(input_path.open('rb')) if input_path is not None else subprocess.DEVNULL
    error=stack.enter_context(log.with_suffix('.stderr.log').open('xb')) if binary else subprocess.STDOUT
    p=subprocess.run(argv,cwd=REPO,env=env,stdin=source,stdout=out,stderr=error,timeout=min(timeout,remaining))
   row['Exit']=p.returncode
  except subprocess.TimeoutExpired:
   row['Cleanup']='UNKNOWN_OriginalCommandTreeNotConfirmed';raise
  finally:
   row['EndUnix']=time.time()
   if log.exists():row.update(Bytes=log.stat().st_size,Sha256=digest(log))
  if log.stat().st_size>limit or p.returncode:raise RuntimeError('Command failed/bound: '+name)
  return log if binary else log.read_text(encoding='utf-8-sig')
 env=dict(os.environ)
 if phase=='prepare':WORK.mkdir(parents=True,exist_ok=False)
 elif phase!='run' or not WORK.is_dir():raise RuntimeError('Original prepared directory required')
 if any(helper.linked(p) for p in (WORK,*WORK.parents)):raise RuntimeError('Linked evidence root')
 try:
  head=run(['git','rev-parse','HEAD'],'head');parents=run(['git','rev-list','--parents','-n','1','HEAD'],'parent').split()
  changed=set(run(['git','diff','--name-only',PARENT,'HEAD'],'scope').splitlines())
  if head.strip()!=env['GITHUB_SHA'] or parents!=[env['GITHUB_SHA'],PARENT] or changed!=FILES or run(['git','status','--porcelain=v1'],'clean').strip():raise RuntimeError('Reviewed parent/scope/clean binding refused')
  if phase=='prepare':
   run(['pwsh','-NoProfile','-NonInteractive','-File',str(REPO/'scripts/probe-settings-desktop.ps1'),'-Output',str(WORK/'desktop-facts.json')],'desktop',45)
   report['RegistryRead']='Original DesktopOnly preflight reads HKLM EnableLUA; no registry writes'
   contract=json.loads((REPO/'packaging/session-test.json').read_text());reviewed=json.loads((REPO/'packaging/session-runtime-audit/runtime-pack-integrity.json').read_text())['Packages']
   locked=json.loads((REPO/'packaging/session-runtime-audit/packages.lock.json').read_text())['dependencies']['net10.0']
   if set(reviewed)!=set(policy.RUNTIME_IDS) or any(reviewed[k]['Version']!=contract['runtime'] or reviewed[k]['NuGetContentHash']!=locked[k]['contentHash'] or reviewed[k]['Source']!=policy.NUGET_SOURCE for k in reviewed):raise RuntimeError('Runtime pins differ')
   verifier=shutil.which('nuget.exe')
   if not verifier:raise RuntimeError('Original preinstalled NuGet verifier missing')
   verifier,report['VerifierSha256']=helper.resolve_verifier(verifier,env,REPO,report)
   helper.require_verifier_version(run([verifier,'help','-ForceEnglishOutput','-NonInteractive'],'verifier',30))
   installer=load('visual_sdk_installer',REPO/'scripts/install-session-sdk-windows.py');opener=urllib.request.build_opener(installer.NoRedirect())
   for identity in policy.RUNTIME_IDS:
    pin=reviewed[identity];name=identity.lower();p=WORK/(name+'.nupkg');url=f"https://api.nuget.org/v3-flatcontainer/{name}/{contract['runtime']}/{name}.{contract['runtime']}.nupkg"
    count=0
    with opener.open(url,timeout=45) as response,p.open('xb') as f:
     while b:=response.read(1024*1024):
      count+=len(b)
      if count>50*1024*1024 or time.monotonic()>deadline:raise RuntimeError('Official download bound')
      f.write(b)
    helper.package_identity(p,identity,contract['runtime'],pin['ArchiveSha512'])
    text=run([verifier,'verify','-All',str(p),'-ForceEnglishOutput','-NonInteractive','-Verbosity','normal'],'signature-'+identity)
    helper.signature_gate(policy,text,identity,contract['runtime'],pin['NuGetContentHash'])
   sdk=helper.install_sdk(REPO,contract,WORK,deadline,report);policy.require_safe_sdk(contract,sdk/'dotnet.exe')
   env.update(DOTNET_ROOT=str(sdk),DOTNET_ROOT_X64=str(sdk),DOTNET_HOST_PATH=str(sdk/'dotnet.exe'),PATH=str(sdk)+os.pathsep+env.get('PATH',''),DOTNET_CLI_HOME=str(WORK/'cli'),NUGET_PACKAGES=str(WORK/'packages'),NUGET_HTTP_CACHE_PATH=str(WORK/'http'))
   dotnet=str(sdk/'dotnet.exe');props=['-p:EnableWindowsTargeting=true','-p:RuntimeFrameworkVersion='+contract['runtime']];config=['--configfile',str(REPO/'NuGet.Config'),*helper.AUDIT]
   if run([dotnet,'--version'],'sdk-version',30).strip()!=contract['sdk']:raise RuntimeError('SDK differs')
   auditproject='packaging/session-runtime-audit/session-runtime-audit.csproj'
   run([dotnet,'restore',auditproject,'--locked-mode',*config,*props],'runtime-restore')
   inventory=json.loads(run([dotnet,'list',auditproject,'package','--include-transitive','--format','json','--no-restore'],'inventory'))
   audit=json.loads(run([dotnet,'list',auditproject,'package','--vulnerable','--include-transitive','--format','json','--no-restore','--source',policy.NUGET_SOURCE],'audit'))
   policy.require_no_advisories(audit,REPO/auditproject,inventory,contract['runtime'])
   prepared={'DeliverySha':env['GITHUB_SHA'],'Sdk':str(sdk),'Bundles':{},'Inputs':{}}
   for side,sha in SIDES.items():
    run(['git','fetch','--no-tags','--depth=1','origin',sha],'fetch-'+side,45)
    tree=run(['git','ls-tree','-rz',sha],'tree-'+side,30,binary=True)
    entries=tree_entries(tree.read_bytes())
    ids=WORK/(side+'-object-ids.txt')
    with ids.open('xb') as f:f.write(b''.join(oid.encode()+b'\n' for _,oid in entries))
    checked=run(['git','cat-file','--batch-check'],'sizes-'+side,30,binary=True,input_path=ids)
    sizes=blob_sizes(checked.read_bytes(),entries)
    packed=run(['git','cat-file','--batch'],'blobs-'+side,60,binary=True,input_path=ids,limit=MAX_TOTAL+len(entries)*128)
    root=WORK/('source-'+side);manifest=write_blobs(packed,root,entries,sizes)
    save(side+'-source-manifest.json',manifest)
    build=WORK/('build-'+side);project='tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj'
    extra=['--artifacts-path',str(build),'-p:KuaiYiVisualProductRoot='+str(root),'-p:SourceRevisionId='+sha]
    run([dotnet,'restore',project,'--locked-mode',*config,*props,*extra],side+'-restore',120)
    run([dotnet,'build',project,'--no-restore','-c','Release','-m:1','-nr:false','-p:UseSharedCompilation=false',*helper.AUDIT,*props,*extra],side+'-build',180)
    # The restored closure contains only the original eight project references.
    assets=json.loads(next((build/'obj/KuaiYi.GrowthHarness').rglob('project.assets.json')).read_text(encoding='utf-8-sig'))
    if set(assets['libraries'])!={x+'/1.0.0' for x in old.PROJECTS} or any(x['type']!='project' for x in assets['libraries'].values()):raise RuntimeError('Project-only closure differs')
    target=build/'bin/KuaiYi.GrowthHarness/release_win-x64';files={str(p.relative_to(target)):digest(p) for p in target.rglob('*') if p.is_file()}
    prepared['Bundles'][side]={'Target':str(target),'Files':files,'AppSha256':digest(target/'KuaiYi.App.dll'),'Root':str(root)}
    if any(digest(root/n)!=h for n,h in manifest.items()):raise RuntimeError('Original source mutated during build')
   paths=run(['git','ls-files','src','tools/KuaiYi.GrowthHarness','scripts','packaging','global.json','Directory.Build.props','Directory.Packages.props','NuGet.Config'],'inputs').splitlines()
   prepared['Inputs']={p:digest(REPO/p) for p in paths};save('prepared.json',prepared)
   report['Status']='Pass_Prepared_NotExecuted'
  else:
   prepared=json.loads((WORK/'prepared.json').read_text())
   if prepared['DeliverySha']!=env['GITHUB_SHA']:raise RuntimeError('Prepared delivery differs')
   env.update(DOTNET_ROOT=prepared['Sdk'],DOTNET_ROOT_X64=prepared['Sdk'],KUAIYI_UI_POLISH_ALLOW_WINDOWS='1')
   runner=load('visual_original_process_owner',REPO/'scripts/overlay_stage_runner.py')
   class VisualOwner(runner.OverlayProcessOwner):
    def __init__(self,*args,side,view,app_hash,**kwargs):
     self.side,self.view,self.app_hash=side,view,app_hash
     super().__init__(*args,**kwargs)
    def _observe_state(self):
     if runner.plain_directory(self.directory)!=self.identity:raise RuntimeError('Original log directory replaced')
     self.state_observation='Retained_Intentional'
    def _verify_result(self):
     actual=verify_result(self.directory/'raster',self.side,self.view,self.app_hash)
     if 'KuaiYi tray cleanup:' in (self.directory/'stderr.log').read_text(encoding='utf-8'):raise RuntimeError('Original tray cleanup diagnostic')
     p=self.directory/'raster/ui-polish-result.json';self.result={'Actual':actual,'Bytes':p.stat().st_size,'Sha256':digest(p)}
    def snapshot(self):
     r=super().snapshot();r.pop('StateDirectoryAbsenceConfirmed');r.pop('CleanupConfirmed')
     settled=self.done.is_set() and not self.original.is_alive() and all(r['Operations'].values()) and r['RescueSettled']
     io=len(r['Streams'])==2 and all(s['Eof'] and s['Closed'] and s['LogClosed'] for s in r['Streams'].values())
     passed=settled and io and not r['Errors'] and not r['DeadlineExpired'] and r['ExitCode']==0 and self.result is not None
     r.update(Status='Pass_LimitedProcess' if passed else 'Failed' if settled else 'Unconfirmed',ResourcesSettled=passed,StateRetained=True)
     return r
   report['Processes']=[];owners=[];paired={}
   for side in SIDES:
    bundle=prepared['Bundles'][side];target=Path(bundle['Target']);root=Path(bundle['Root']);manifest=json.loads((WORK/(side+'-source-manifest.json')).read_text())
    if any(digest(root/n)!=h for n,h in manifest.items()) or any(digest(REPO/p)!=h for p,h in prepared['Inputs'].items()) or {str(p.relative_to(target)):digest(p) for p in target.rglob('*') if p.is_file()}!=bundle['Files']:raise RuntimeError('Source/bundle bytes changed before visual run')
    for view in ('settings','overlay'):
     if deadline-time.monotonic()<65:raise RuntimeError('Insufficient shared visual budget; no launch')
     logs=WORK/(side+'-'+view);logs.mkdir();raster=logs/'raster';raster.mkdir()
     # Base owner requires an empty log directory; prepare child raster only inside its launch callback.
     raster.rmdir()
     def launch(argv,**kwargs):
      raster.mkdir(exist_ok=False)
      return subprocess.Popen(argv,**kwargs)
     owner=VisualOwner([str(target/'KuaiYi.GrowthHarness.exe'),'--ci-ui-polish',str(raster),side,view],logs,env['GITHUB_SHA'],env,side=side,view=view,app_hash=bundle['AppSha256'],_launch=launch)
     owners.append(owner)
     receipt=owner.observe();save(side+'-'+view+'-observation.json',receipt)
     if receipt['Status']=='Unconfirmed':print('KUAIYI_VISUAL_UNKNOWN '+json.dumps(receipt),flush=True)
     owner.join_originals();receipt=owner.snapshot();save(side+'-'+view+'-settled.json',receipt);report['Processes'].append(receipt)
     if receipt['Status']!='Pass_LimitedProcess':raise RuntimeError('Original visual process failed; no next launch')
     paired[(side,view)]=receipt['OriginalResult']['Actual']['Scenes']
    if {str(p.relative_to(target)):digest(p) for p in target.rglob('*') if p.is_file()}!=bundle['Files'] or any(digest(root/n)!=h for n,h in manifest.items()):raise RuntimeError('Source/bundle bytes changed after visual run')
   for view in ('settings','overlay'):
    a={s['Name']:s for s in paired[('before',view)]};b={s['Name']:s for s in paired[('after',view)]}
    if set(a)!=set(b):raise RuntimeError('Comparison scene identities differ')
    keys=['Theme','Motion','Status','RequestedWidthDip','RequestedHeightDip','DpiX','DpiY','FontSize']
    for name in a:
     if any(a[name][k]!=b[name][k] for k in keys):raise RuntimeError('Comparison conditions differ: '+name)
     if a[name]['Target'] is not None:
      for k in ('Type','IsEnabled','IsKeyboardFocused'):
       if a[name]['Target'][k]!=b[name]['Target'][k]:raise RuntimeError('Actual control comparison differs: '+name)
   if any(digest(REPO/p)!=h for p,h in prepared['Inputs'].items()):raise RuntimeError('Delivery inputs changed after visual run')
   report.update(Status='Pass_LimitedRasterCollection',PixelQuality='PendingHumanReview_NotAutomaticallyPassed')
 except BaseException as error:
  report.update(Error=type(error).__name__+': '+str(error));raise
 finally:
  save(phase+'-result.json',report)
  evidence=WORK/'evidence';evidence.mkdir(exist_ok=True);files=[];total=0
  candidates=list(WORK.glob('*.json'))+list(WORK.glob('*.log'))
  for side in SIDES:
   for view in ('settings','overlay'):
    d=WORK/(side+'-'+view)
    if d.exists():candidates.extend(p for p in d.rglob('*') if p.is_file())
  for p in candidates:
   if any(helper.linked(x) for x in (p,*p.parents)):raise RuntimeError('Linked evidence retained, upload refused')
   total+=p.stat().st_size
   if total>192*1024*1024:raise RuntimeError('Complete evidence bound exceeded; no selective success')
   dest=evidence/p.relative_to(WORK);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
   files.append({'Path':str(p.relative_to(WORK)),'Bytes':p.stat().st_size,'Sha256':digest(p)})
  (evidence/(phase+'-manifest.json')).write_text(json.dumps(files,indent=2)+'\n')
  # Upload the complete owned evidence tree via the separately pinned always() artifact step.
  print('KUAIYI_UI_VISUAL '+json.dumps({'Phase':phase,'Status':report['Status'],'Error':report.get('Error')}),flush=True)

if __name__=='__main__':
 if len(sys.argv)!=2 or sys.argv[1] not in ('prepare','run'):raise SystemExit('Only prepare/run')
 main(sys.argv[1])
