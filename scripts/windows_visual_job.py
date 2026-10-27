"""Own the complete validation subtree, not unrelated GitHub runner/action processes.

No native APIs are loaded at import. The explicit hosted execution gate precedes native use.
The unnamed Job disallows breakaway by default. No PID adoption, kill, or rescue proves cleanup.
"""
import ctypes
import json
import os
from pathlib import Path
import subprocess
import sys
import time

class JobOwner:
    def __init__(self,api,pid):
        self.api,self.pid=api,pid
        self.handle=api.create()
        self.assigned=False
        self.receipts=[]
        self.query_error=None
        self.close_attempted=False
        self.close_error=None
        try:
            api.assign_current(self.handle);self.assigned=True
        except BaseException as error:
            try:api.close(self.handle)
            except Exception as close:raise RuntimeError("Original assignment failed: "+str(error)+"; original handle close failed: "+str(close)) from error
            raise
    def sample(self):
        try:
            if self.close_error is not None:raise RuntimeError('Original handle close outcome unknown; no retry')
            if self.query_error is not None:raise RuntimeError('Original query failed; no retry: '+self.query_error)
            assigned,ids=self.api.members(self.handle)
            ids=list(ids)
            if assigned!=len(ids) or len(ids)>512 or len(set(ids))!=len(ids):raise RuntimeError('Incomplete Job membership')
            status='Pass_OwnValidationDescendantsExited' if ids==[self.pid] else 'UNKNOWN'
            value={'Status':status,'Assigned':assigned,'MemberIds':ids,'WrapperPid':self.pid,'Scope':'Original unnamed Job; supervisor plus every inherited validation descendant; excludes runner/checkout/upload/pwsh'}
        except Exception as error:
            self.query_error=self.query_error or type(error).__name__+': '+str(error)
            value={'Status':'UNKNOWN','Error':self.query_error,'WrapperPid':self.pid}
        self.receipts.append(value);return value
    def close_if_confirmed(self):
        if self.handle is None:return True
        if self.close_attempted:return False
        value=self.sample()
        if value['Status']!='Pass_OwnValidationDescendantsExited':return False
        self.close_attempted=True
        try:self.api.close(self.handle)
        except Exception as error:
            self.close_error=type(error).__name__+': '+str(error)
            self.receipts.append({'Status':'UNKNOWN','Error':'Original CloseHandle failed; no retry: '+self.close_error,'WrapperPid':self.pid})
            return False
        self.handle=None;return True

class WindowsJobApi:
    def __init__(self):
        if os.name!='nt':raise RuntimeError('Windows Job observation not available; no fallback')
        from ctypes import wintypes as w
        self.w=w;self.k=ctypes.WinDLL('kernel32',use_last_error=True)
        self.k.CreateJobObjectW.argtypes=[ctypes.c_void_p,w.LPCWSTR];self.k.CreateJobObjectW.restype=w.HANDLE
        self.k.GetCurrentProcess.argtypes=[];self.k.GetCurrentProcess.restype=w.HANDLE
        self.k.AssignProcessToJobObject.argtypes=[w.HANDLE,w.HANDLE];self.k.AssignProcessToJobObject.restype=w.BOOL
        self.k.QueryInformationJobObject.argtypes=[w.HANDLE,ctypes.c_int,ctypes.c_void_p,w.DWORD,ctypes.POINTER(w.DWORD)];self.k.QueryInformationJobObject.restype=w.BOOL
        self.k.CloseHandle.argtypes=[w.HANDLE];self.k.CloseHandle.restype=w.BOOL
    def create(self):
        h=self.k.CreateJobObjectW(None,None)
        if not h:raise ctypes.WinError(ctypes.get_last_error())
        return h
    def assign_current(self,h):
        if not self.k.AssignProcessToJobObject(h,self.k.GetCurrentProcess()):raise ctypes.WinError(ctypes.get_last_error())
    def members(self,h):
        # Fixed bounded buffer. ERROR_MORE_DATA or inconsistent counts remain UNKNOWN.
        buf=ctypes.create_string_buffer(8+512*ctypes.sizeof(ctypes.c_size_t));returned=self.w.DWORD()
        if not self.k.QueryInformationJobObject(h,3,buf,len(buf),ctypes.byref(returned)):raise ctypes.WinError(ctypes.get_last_error())
        counts=(self.w.DWORD*2).from_buffer(buf);assigned,count=counts[0],counts[1]
        if count>512 or assigned!=count:raise RuntimeError('Job PID list capacity/count incomplete')
        ids=(ctypes.c_size_t*count).from_buffer(buf,8)
        return assigned,list(ids)
    def close(self,h):
        if not self.k.CloseHandle(h):raise ctypes.WinError(ctypes.get_last_error())

def supervise(repo,env,owner,launch=subprocess.Popen,clock=time.monotonic,_hold=None,_publish=None):
    work=repo/'artifacts/ui-polish-windows';work_acquired=False
    report={'Kind':'OwnedValidationJobTree','Status':'UNKNOWN','SourceHead':env['GITHUB_SHA'],'WrapperPid':os.getpid(),'Phases':[],'NoKillOnClose':True,'NoBreakawayAllowed':True}
    def save():
        if not work_acquired:raise RuntimeError('Original evidence root not acquired; no write')
        report['MembershipSamples']=owner.receipts
        (work/'validation-tree.json').write_text(json.dumps(report,indent=2)+'\n')
    result=2
    try:
        work.mkdir(parents=True,exist_ok=False);work_acquired=True
        deadline=clock()+12*60
        save()
        if owner.sample()['Status']!='Pass_OwnValidationDescendantsExited':raise RuntimeError('Initial Job query unconfirmed; no child launch')
        save()
        for phase,budget in [('prepare',6*60),('run',6*60)]:
            available=min(budget,deadline-clock())
            if available<=0:raise RuntimeError('Original shared validation budget exhausted')
            row={'Phase':phase,'OriginalExitConfirmed':False,'Exit':None};report['Phases'].append(row)
            process=launch([sys.executable,'-I','-B',str(repo/'scripts/verify-ui-polish-windows.py'),phase],cwd=repo,env=env,stdin=subprocess.DEVNULL)
            row['Pid']=process.pid
            try:row['Exit']=process.wait(timeout=available);row['OriginalExitConfirmed']=True
            except subprocess.TimeoutExpired:
                row['Status']='UNKNOWN_OriginalProcessStillOwned';save()
                # Keep original process and Job references. No replacement observer or next launch.
                process.wait();row['OriginalExitConfirmed']=True;raise
            save();membership=owner.sample();save()
            if row['Exit']!=0 or membership['Status']!='Pass_OwnValidationDescendantsExited':raise RuntimeError('Phase failed or owned descendants remain; no next phase')
        report['Status']='Pass_OwnValidationSubtree_AwaitingParentWrapperExit';result=0
    except Exception as error:
        report['Error']=type(error).__name__+': '+str(error);report['ErrorType']=type(error).__name__;report['Status']='Fail';result=2
    finally:
        # One final original query/close decision; never re-close after a failed CloseHandle.
        confirmed=owner.close_if_confirmed()
        report['OriginalJobHandleClosed']=confirmed
        report['OriginalEvidenceRootAcquired']=work_acquired
        if not confirmed:report['Status']='UNKNOWN';result=2
        try:save()
        except Exception as error:
            report['EvidenceWriteError']=type(error).__name__+': '+str(error);result=2
        if work_acquired and _publish is not None:
            try:_publish(work,report)
            except Exception as error:
                report['EvidencePublicationStatus']='Fail'
                report['EvidencePublicationErrorType']=type(error).__name__;result=2
                try:save()
                except Exception as write_error:report['EvidenceWriteErrorType']=type(write_error).__name__
        if not confirmed:
            try:print('KUAIYI_VALIDATION_TREE_UNKNOWN '+json.dumps(report),flush=True)
            except Exception as error:report['DiagnosticWriteError']=type(error).__name__+': '+str(error)
            if _hold is not None:_hold()
            else:
                while True:time.sleep(1)
    return result

if __name__=='__main__':
    import importlib.util
    repo=Path(__file__).resolve().parent.parent
    spec=importlib.util.spec_from_file_location('visual_gate',repo/'scripts/verify-ui-polish-windows.py');gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)
    event=json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'));gate.context(os.environ,sys.platform,event)
    def publication(work,report):
        spec=importlib.util.spec_from_file_location('owned_visual_publication',repo/'scripts/visual_publication.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        return module.publish_owned(work,report,dict(os.environ))
    try:owner=JobOwner(WindowsJobApi(),os.getpid())
    except Exception as error:
        work=repo/'artifacts/ui-polish-windows'
        if not work.exists():
            work.mkdir(parents=True)
            report={'Status':'UNKNOWN','Stage':'OriginalJobCreateAssign','ErrorType':type(error).__name__,'ValidationChildrenLaunched':False,'SourceHead':os.environ['GITHUB_SHA'],'OriginalEvidenceRootAcquired':True,'OriginalJobHandleClosed':False}
            (work/'validation-tree.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
            try:publication(work,report)
            except Exception as publish_error:print('Visual failure evidence unconfirmed: '+type(publish_error).__name__,flush=True)
        print('KUAIYI_VALIDATION_TREE_BLOCKED '+type(error).__name__,flush=True)
        raise SystemExit(2)
    raise SystemExit(supervise(repo,dict(os.environ),owner,_publish=publication))
