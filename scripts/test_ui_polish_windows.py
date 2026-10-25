import importlib.util
from pathlib import Path
import unittest
import tempfile,json,base64,hashlib
spec=importlib.util.spec_from_file_location('ui_visual',Path(__file__).with_name('verify-ui-polish-windows.py'))
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class Contract(unittest.TestCase):
 def valid(self):return {'GITHUB_ACTIONS':'true','GITHUB_REPOSITORY':'T1doo/KuaiYi','GITHUB_REF':v.BRANCH,'GITHUB_EVENT_NAME':'push','GITHUB_RUN_ATTEMPT':'1','RUNNER_OS':'Windows','GITHUB_SHA':'a'*40}
 def test_exact_context(self):v.context(self.valid(),'win32',{'repository':{'private':False}})
 def test_old_branch_cannot_launch_new_window(self):
  e=self.valid();e['GITHUB_REF']='refs/heads/dev/p7-fixed-signoff'
  with self.assertRaises(RuntimeError):v.context(e,'win32',{'repository':{'private':False}})
 def test_rerun_refused(self):
  e=self.valid();e['GITHUB_RUN_ATTEMPT']='2'
  with self.assertRaises(RuntimeError):v.context(e,'win32',{'repository':{'private':False}})
 def test_linux_and_private_refused(self):
  for platform,event in [('linux',{'repository':{'private':False}}),('win32',{'repository':{'private':True}})]:
   with self.assertRaises(RuntimeError):v.context(self.valid(),platform,event)
class ResultContract(unittest.TestCase):
 def sample(self,root,view='settings'):
  raw=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a1Z0AAAAASUVORK5CYII=')
  scenes=[]
  for i in range(8 if view=='settings' else 18):
   name='scene-'+str(i)+'.png';(root/name).write_bytes(raw)
   scenes.append({'File':name,'StateCoherent':True,'Side':'before','ProductHead':v.SIDES['before'],'AppDllSha256':'a'*64,'Bytes':len(raw),'Sha256':hashlib.sha256(raw).hexdigest(),'PixelsX':1,'PixelsY':1})
  r={'Status':'Pass_LimitedWpfRaster','Side':'before','View':view,'ProductHead':v.SIDES['before'],'AppDllSha256':'a'*64,'TimedOut':False,'CloseConfirmed':True,'ResourcesSettled':True,'StateRetained':True,'Failures':[],'Scenes':scenes,'Retirements':[]}
  if view=='overlay':r['Retirements']=[{'OwnWindowClosed':True,'TrayDisposed':True,'TrayRemoved':True}]*8+[{'AllOriginalTasksCompleted':True,'Runs':1,'Connectors':1,'Attempts':1}]*8
  return r
 def check(self,change,view='settings',reject=True):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);r=self.sample(root,view);change(r);(root/'ui-polish-result.json').write_text(json.dumps(r))
   if reject:
    with self.assertRaises(RuntimeError):v.verify_result(root,'before',view,'a'*64)
   else:v.verify_result(root,'before',view,'a'*64)
 def test_complete_collection(self):self.check(lambda r:None,reject=False)
 def test_wrong_side(self):self.check(lambda r:r.update(Side='after'))
 def test_wrong_dll(self):self.check(lambda r:r.update(AppDllSha256='b'*64))
 def test_missing_scene(self):self.check(lambda r:r['Scenes'].pop())
 def test_unknown_cleanup(self):self.check(lambda r:r.update(ResourcesSettled=False))
 def test_unremoved_original_tray(self):self.check(lambda r:r['Retirements'][0].update(TrayRemoved=False),view='overlay')
 def test_unsettled_connector(self):self.check(lambda r:r['Retirements'][8].update(AllOriginalTasksCompleted=False),view='overlay')
 def test_changed_png_hash(self):self.check(lambda r:r['Scenes'][0].update(Sha256='f'*64))
 def test_incoherent_frame(self):self.check(lambda r:r['Scenes'][0].update(StateCoherent=False))
if __name__=='__main__':unittest.main()
