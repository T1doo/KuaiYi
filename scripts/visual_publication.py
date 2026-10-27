"""Owned immutable visual evidence snapshots; never product/source authority."""
import codecs
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys

MAX_FILES=512
MAX_FILE=32*1024*1024
MAX_TOTAL=192*1024*1024
MAX_META=1024*1024
VIEWS={'before-settings','after-settings','before-overlay','after-overlay'}
SECRET=re.compile(rb'(?i)(?:[?&](?:sig(?:nature)?|token|access_token|api[_-]?key|(?:x-amz|x-goog)-(?:signature|credential|security-token)|awsaccesskeyid|googleaccessid|key-pair-id)=|-----BEGIN [A-Z ]*PRIVATE KEY|gh[pousr]_[A-Za-z0-9]{20})')

# These exact empty retained files are created by the original SettingsStore and coordinator.
FIXTURE_LOCKS=set()
for side in ('before','after'):
    for theme in ('Light','Dark'):
        names=[(side+'-settings','settings-'+theme)]
        names += [(side+'-overlay','overlay-'+theme+'-'+motion+'-'+state) for motion in ('Full','Reduce') for state in ('stopped','complete')]
        for view,fixture in names:
            for name in ('settings.lock','secret-transaction.lock'):FIXTURE_LOCKS.add(view+'/raster/state/'+fixture+'/config/'+name)

def allowed_file(relative,path):
    if path.suffix in ('.json','.log','.png'):return
    if relative in FIXTURE_LOCKS and path.stat().st_size==0:return
    raise RuntimeError('Unregistered evidence file type or nonempty retained lock')

def identity(env):
    value={k:env.get(k,'') for k in ('GITHUB_SHA','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT')}
    if not re.fullmatch('[0-9a-f]{40}',value['GITHUB_SHA']) or not re.fullmatch('[1-9][0-9]{0,19}',value['GITHUB_RUN_ID']) or value['GITHUB_RUN_ATTEMPT']!='1':raise RuntimeError('Exact evidence identity required')
    return value

def plain(path,*,file=False):
    for p in (path,*path.parents):
        if p.is_symlink() or (p.exists() and getattr(p.stat(),'st_file_attributes',0)&0x400):raise RuntimeError('Linked evidence refused')
    if file:
        s=path.stat()
        if not stat.S_ISREG(s.st_mode) or s.st_nlink!=1:raise RuntimeError('Original regular evidence required')
    return path

def sha(path):
    h=hashlib.sha256()
    with plain(path,file=True).open('rb') as stream:
        for b in iter(lambda:stream.read(1024*1024),b''):h.update(b)
    return h.hexdigest()

def safe_relative(name):
    parts=name.split('/')
    if len(parts)>12 or len(name)>512:raise RuntimeError('Evidence path bound')
    if any(p in ('','.', '..') or not re.fullmatch('[A-Za-z0-9_.-]{1,160}',p) for p in parts):raise RuntimeError('Evidence path refused')
    return parts

def inventory(work):
    plain(work);files=[];directories=0
    def walk(folder):
        nonlocal directories
        directories+=1
        if directories>MAX_FILES*2:raise RuntimeError('Evidence directory bound')
        for p in sorted(folder.iterdir()):
            plain(p)
            if p.is_dir():walk(p)
            elif p.is_file():files.append(plain(p,file=True))
            else:raise RuntimeError('Unknown evidence kind')
            if len(files)>MAX_FILES:raise RuntimeError('Evidence count bound')
    for p in sorted(work.iterdir()):
        plain(p)
        if p.is_file() and p.suffix in ('.json','.log'):files.append(plain(p,file=True))
        elif p.name in VIEWS:
            if not p.is_dir():raise RuntimeError('Expected original view directory')
            walk(p)
    if len(files)>MAX_FILES:raise RuntimeError('Evidence count bound')
    for p in files:
        safe_relative(p.relative_to(work).as_posix())
        allowed_file(p.relative_to(work).as_posix(),p)
    return files

def write_json(path,value,limit=MAX_META):
    b=(json.dumps(value,separators=(',',':'))+'\n').encode('utf-8')
    if len(b)>limit:raise RuntimeError('Evidence metadata bound')
    with path.open('xb') as stream:
        if stream.write(b)!=len(b):raise OSError('Short metadata write')
        stream.flush();os.fsync(stream.fileno())
    return hashlib.sha256(b).hexdigest()

def read_json(path,limit=MAX_META):
    def unique(items):
        d={}
        for k,v in items:
            if k in d:raise RuntimeError('Duplicate evidence field')
            d[k]=v
        return d
    with plain(path,file=True).open('rb') as stream:b=stream.read(limit+1)
    if len(b)>limit:raise RuntimeError('Evidence metadata read bound')
    return json.loads(b.decode('utf-8-sig'),object_pairs_hook=unique)

def publish_owned(work,report,env):
    ids=identity(env)
    if report.get('OriginalEvidenceRootAcquired') is not True or report.get('SourceHead')!=ids['GITHUB_SHA']:raise RuntimeError('Original root acquisition required')
    plain(work);destination=work.parent/'ui-polish-publication';plain(destination);destination.mkdir(exist_ok=False)
    originals=inventory(work);records=[];total=0
    for source in originals:
        relative=source.relative_to(work).as_posix();s=source.stat()
        if s.st_size>MAX_FILE or (source.suffix=='.png' and s.st_size>2*1024*1024):raise RuntimeError('Evidence file bound')
        target=destination.joinpath(*safe_relative(relative));target.parent.mkdir(parents=True,exist_ok=True)
        h=hashlib.sha256();count=0;carry=b'';decoder=codecs.getincrementaldecoder('utf-8')('strict') if source.suffix!='.png' else None
        with source.open('rb') as inp,target.open('xb') as out:
            while True:
                b=inp.read(1024*1024)
                if not b:break
                count+=len(b);total+=len(b)
                if count>MAX_FILE or total>MAX_TOTAL:raise RuntimeError('Evidence retained-byte bound')
                if decoder is not None:
                    decoder.decode(b)
                    if SECRET.search(carry+b):raise RuntimeError('Sensitive evidence publication refused')
                    carry=(carry+b)[-256:]
                if out.write(b)!=len(b):raise OSError('Short evidence write')
                h.update(b)
            if decoder is not None:decoder.decode(b'',final=True)
            out.flush();os.fsync(out.fileno())
        after=source.stat()
        if (s.st_dev,s.st_ino,s.st_size,s.st_mtime_ns)!=(after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns) or count!=s.st_size or sha(source)!=h.hexdigest() or sha(target)!=h.hexdigest():raise RuntimeError('Original evidence changed during copy')
        records.append({'Path':relative,'Bytes':count,'Sha256':h.hexdigest()})
    if [p.relative_to(work).as_posix() for p in inventory(work)]!=[p.relative_to(work).as_posix() for p in originals]:raise RuntimeError('Original evidence set changed')
    for r in records:
        if sha(work.joinpath(*safe_relative(r['Path'])))!=r['Sha256']:raise RuntimeError('Original evidence changed before final snapshot')
    if report.get('Status') not in ('Pass_OwnValidationSubtree_AwaitingParentWrapperExit','Fail','UNKNOWN'):raise RuntimeError('Original native state differs')
    error_type=report.get('ErrorType')
    if error_type is not None and not re.fullmatch('[A-Za-z0-9_]{1,64}',error_type):raise RuntimeError('Native error type differs')
    state={'Status':report['Status'],'OriginalJobHandleClosed':report.get('OriginalJobHandleClosed') is True,'ErrorType':error_type}
    manifest={'Kind':'OwnedVisualSnapshot_NotTestPass','Identity':ids,'NativeState':state,'Files':records,'Bytes':total,'Scope':'Original root JSON/log and four complete synthetic view directories; product binaries/source trees excluded'}
    digest=write_json(destination/'manifest.json',manifest)
    write_json(destination/'READY.json',{'Kind':'OwnedVisualSnapshotReady_NotTestPass','Identity':ids,'ManifestSha256':digest,'NativeState':state,'Files':len(records),'Bytes':total},4096)
    return destination

def verify_snapshot(root,ids):
    plain(root);ready=read_json(root/'READY.json',4096)
    if set(ready)!={'Kind','Identity','ManifestSha256','NativeState','Files','Bytes'} or type(ready.get('Files')) is not int or type(ready.get('Bytes')) is not int:raise RuntimeError('Snapshot ready schema differs')
    state=ready.get('NativeState')
    if not isinstance(state,dict) or set(state)!={'Status','OriginalJobHandleClosed','ErrorType'} or state['Status'] not in ('Pass_OwnValidationSubtree_AwaitingParentWrapperExit','Fail','UNKNOWN') or type(state['OriginalJobHandleClosed']) is not bool or not(state['ErrorType'] is None or isinstance(state['ErrorType'],str) and re.fullmatch('[A-Za-z0-9_]{1,64}',state['ErrorType'])):raise RuntimeError('Native snapshot state schema differs')
    if ready.get('Kind')!='OwnedVisualSnapshotReady_NotTestPass' or ready.get('Identity')!=ids or sha(root/'manifest.json')!=ready.get('ManifestSha256'):raise RuntimeError('Snapshot identity differs')
    m=read_json(root/'manifest.json')
    if set(m)!={'Kind','Identity','NativeState','Files','Bytes','Scope'} or type(m.get('Bytes')) is not int:raise RuntimeError('Snapshot manifest schema differs')
    if m.get('Kind')!='OwnedVisualSnapshot_NotTestPass' or m.get('Identity')!=ids or m.get('NativeState')!=ready.get('NativeState'):raise RuntimeError('Snapshot manifest differs')
    files=m.get('Files');total=0;names=[]
    if not isinstance(files,list) or len(files)>MAX_FILES:raise RuntimeError('Snapshot count bound')
    for row in files:
        if set(row)!={'Path','Bytes','Sha256'}:raise RuntimeError('Snapshot row schema differs')
        p=root.joinpath(*safe_relative(row['Path']));plain(p,file=True);allowed_file(row['Path'],p);size=p.stat().st_size
        if type(row['Bytes']) is not int or size!=row['Bytes'] or size>MAX_FILE or (p.suffix=='.png' and size>2*1024*1024) or sha(p)!=row['Sha256']:raise RuntimeError('Snapshot bytes differ')
        total+=size;names.append(row['Path'])
    actual=[]
    def walk(folder):
        for p in folder.iterdir():
            plain(p)
            if p.is_dir():walk(p)
            else:
                actual.append(plain(p,file=True).relative_to(root).as_posix())
                if len(actual)>MAX_FILES+2:raise RuntimeError('Snapshot actual count bound')
    walk(root)
    if len(set(names))!=len(names) or set(actual)!=set(names)|{'manifest.json','READY.json'} or total>MAX_TOTAL or total!=m.get('Bytes') or total!=ready.get('Bytes') or len(files)!=ready.get('Files'):raise RuntimeError('Complete snapshot set differs')
    return ready

def qualify(repo,env,output):
    ids=identity(env);parent=repo/'artifacts/ui-polish-visual-parent';claim=read_json(parent/'namespace.json',4096)
    if claim!={'Kind':'OwnedVisualNamespaceClaim','Identity':ids,'NativeAndPublicationRootsWereAbsent':True}:raise RuntimeError('Current original namespace claim required')
    root=repo/'artifacts/ui-polish-visual-parent-publication';plain(root);root.mkdir(exist_ok=False)
    for name in ('namespace.json','parent-exit.json'):
        p=parent/name
        if not p.exists():continue
        value=read_json(p,4096)
        if name=='parent-exit.json':
            expected={'Kind','Identity','OriginalForegroundCommandReturned','NativeExitCode','ErrorType'}
            if set(value)!=expected or value.get('Kind')!='OriginalVisualParentReturn' or value.get('Identity')!=ids or type(value.get('OriginalForegroundCommandReturned')) is not bool or not (value['NativeExitCode'] is None or type(value['NativeExitCode']) is int) or not (value['ErrorType'] is None or isinstance(value['ErrorType'],str) and re.fullmatch('[A-Za-z0-9_]{1,64}',value['ErrorType'])):raise RuntimeError('Original parent receipt differs')
        write_json(root/name,value,4096)
    with output.open('a',encoding='utf-8') as stream:stream.write('parent=true\n')
    verify_snapshot(repo/'artifacts/ui-polish-publication',ids)
    with output.open('a',encoding='utf-8') as stream:stream.write('visual=true\n')
    return 0

if __name__=='__main__':
    if sys.argv[1:]!=['--qualify']:raise SystemExit('Only the explicit future artifact qualification entry is accepted')
    try:code=qualify(Path(__file__).resolve().parent.parent,dict(os.environ),Path(os.environ['GITHUB_OUTPUT']))
    except Exception as error:print('Visual evidence qualification failed: '+type(error).__name__);code=2
    raise SystemExit(code)
