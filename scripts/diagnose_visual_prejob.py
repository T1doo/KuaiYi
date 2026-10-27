"""Default-closed future diagnostic. Stops before Job/native/UI construction."""
import ast
import hashlib
import importlib.util
import json
import locale
import os
from pathlib import Path
import re
import sys

SOURCES={
    'scripts/windows_visual_job.py':'2ef2b8730fbeb717730d9d1161e892a315e73761b57840c27df2aaf0550d16ea',
    'scripts/verify-ui-polish-windows.py':'ad62e0e912b8cdbea39a2bcc4975943a34e3fda60fec3402b4b41312a83aab7b',
}
MAX_SOURCE=256*1024
MAX_EVENT=4*1024*1024
MAX_RECEIPT=4096

def load_source(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module

def diagnose(repo,env,platform,*,loader=load_source,encoding=None):
    # This directory never claims the original supervisor work root.
    root=repo/'artifacts/ui-polish-bootstrap'
    report={'Kind':'PreJobDiagnosticOnly','Status':'Fail','Stage':'ReceiptAcquire','DeclaredSourceSha':env.get('GITHUB_SHA') if re.fullmatch('[0-9a-f]{40}',env.get('GITHUB_SHA','')) else 'Invalid','GitSourceCommitVerified':False,'NativeJobCreated':False,'WpfStarted':False,'ExplicitRegistryCalls':False,'ChildCommandsStarted':False}
    stream=None;result=2
    def save():
        raw=json.dumps(report,separators=(',',':'))+'\n'
        if len(raw.encode('utf-8'))>MAX_RECEIPT:raise RuntimeError('Receipt bound')
        stream.seek(0);stream.write(raw);stream.truncate();stream.flush()
    def stage(name):report['Stage']=name;save()
    try:
        for path in (root,*root.parents):
            if path.is_symlink() or (path.exists() and getattr(path.stat(),'st_file_attributes',0)&0x400):raise RuntimeError('Linked diagnostic root')
        root.mkdir(parents=True,exist_ok=False)
        stream=(root/'prejob.json').open('x+',encoding='utf-8');save()
        stage('Runtime')
        codec=encoding or locale.getpreferredencoding(False)
        report['Runtime']={'PythonVersion':list(sys.version_info[:3]),'Isolated':bool(sys.flags.isolated),'Utf8Mode':bool(sys.flags.utf8_mode),'DefaultEncoding':codec if re.fullmatch('[A-Za-z0-9_.-]{1,40}',codec) else 'Unrecognized'}
        stage('SourceParse')
        report['Sources']=[]
        for relative,expected in SOURCES.items():
            with (repo/relative).open('rb') as source:raw=source.read(MAX_SOURCE+1)
            if len(raw)>MAX_SOURCE:raise RuntimeError('Source bound')
            actual=hashlib.sha256(raw).hexdigest()
            if actual!=expected:raise RuntimeError('Reviewed source bytes differ')
            ast.parse(raw,filename=relative)
            report['Sources'].append({'Path':relative,'Bytes':len(raw),'Sha256':actual})
        stage('ModuleImport')
        # Imports run with diagnostic module names, never __main__; no owner is constructed.
        loader('prejob_owned_module',repo/'scripts/windows_visual_job.py')
        gate=loader('prejob_gate_module',repo/'scripts/verify-ui-polish-windows.py')
        stage('EventDecode')
        with Path(env['GITHUB_EVENT_PATH']).open('rb') as source:raw=source.read(MAX_EVENT+1)
        if len(raw)>MAX_EVENT:raise RuntimeError('Event bound')
        report['EventBytes']=len(raw)
        try:raw.decode(codec);report['LegacyDefaultDecode']='Accepted'
        except UnicodeError:report['LegacyDefaultDecode']='UnicodeError'
        event=json.loads(raw.decode('utf-8'))
        stage('OriginalContextGate');gate.context(env,platform,event)
        report.update(Status='Pass_PreJobOnly_NotNativeOrWpfQualification',Stage='StoppedBeforeJobConstruction')
        save();result=0
    except BaseException as error:
        report.update(Status='Fail',ErrorType=type(error).__name__)
        if stream is not None:
            try:save()
            except BaseException as write_error:report['ReceiptWriteError']=type(write_error).__name__
    finally:
        if stream is not None:
            try:stream.close()
            except BaseException as close_error:report.update(Status='Fail',ReceiptCloseError=type(close_error).__name__);result=2
    if result!=0:
        safe={key:report[key] for key in ('Kind','Status','Stage','ErrorType','ReceiptWriteError','ReceiptCloseError') if key in report}
        print(json.dumps(safe,separators=(',',':')))
    return result

if __name__=='__main__':
    if sys.argv[1:]!=['--diagnose-prejob']:
        print('Pre-Job diagnostic is disabled without its explicit one-attempt entry.')
        raise SystemExit(2)
    raise SystemExit(diagnose(Path(__file__).resolve().parent.parent,dict(os.environ),sys.platform))
