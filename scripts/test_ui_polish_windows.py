import importlib.util
from pathlib import Path
import unittest
import tempfile,json,base64,hashlib
import subprocess,io,tarfile,os
from unittest.mock import patch,Mock
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

class RawSource(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.hold=tempfile.TemporaryDirectory();cls.base=Path(cls.hold.name);cls.repo=cls.base/'git';cls.repo.mkdir()
  cls.data={'.gitattributes':b'* text=auto\npayload.dat -text\n','space name.txt':b'original LF\nsecond\n','文档.txt':'合成Unicode\n'.encode(),'payload.dat':b'\x00\xff\r\nbody\n\x00'}
  cls.git('init','-q');cls.git('config','core.autocrlf','false')
  for name,data in cls.data.items():(cls.repo/name).write_bytes(data)
  cls.git('add','.');cls.git('-c','user.name=Synthetic','-c','user.email=synthetic@example.invalid','commit','-qm','Synthetic source')
  cls.sha=cls.git('rev-parse','HEAD').decode().strip();cls.raw=cls.git('ls-tree','-rz',cls.sha)
 @classmethod
 def tearDownClass(cls):cls.hold.cleanup()
 @classmethod
 def git(cls,*args,input=None):return subprocess.check_output(['git',*args],cwd=cls.repo,input=input,stderr=subprocess.PIPE)
 def entries(self):return v.tree_entries(self.raw)
 def pack(self,entries):
  ids=b''.join(oid.encode()+b'\n' for _,oid in entries)
  return self.git('cat-file','--batch',input=ids),v.blob_sizes(self.git('cat-file','--batch-check',input=ids),entries)
 def write(self,entries,pack,sizes):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/'objects';p.write_bytes(pack);root=Path(d)/'source';result=v.write_blobs(p,root,entries,sizes)
   return result,{n:(root/n).read_bytes() for n,_ in entries}
 def test_raw_bytes_preserved_despite_archive_eol_conversion(self):
  archive=self.git('-c','core.autocrlf=true','archive',self.sha,'space name.txt')
  with tarfile.open(fileobj=io.BytesIO(archive)) as t:old=t.extractfile('space name.txt').read()
  if os.environ.get('KUAIYI_OLD_RED')=='1':self.assertEqual(self.data['space name.txt'],old);return
  self.assertNotEqual(old,self.data['space name.txt']);e=self.entries();p,s=self.pack(e);manifest,files=self.write(e,p,s)
  self.assertEqual(self.data,files);self.assertEqual({n:hashlib.sha256(b).hexdigest() for n,b in self.data.items()},manifest)
 def test_unicode_and_spaces_not_git_quoted(self):
  old={r.split(b'\t',1)[1] for r in self.git('-c','core.quotePath=true','ls-tree','-r',self.sha).splitlines()}
  if os.environ.get('KUAIYI_OLD_RED')=='1':self.assertIn('文档.txt'.encode(),old);return
  self.assertNotIn('文档.txt'.encode(),old);self.assertEqual(set(self.data),{n for n,_ in self.entries()})
 def test_windows_path_policy(self):
  for name in ['line\nname.txt','tab\tname','bad\x7fname','../escape','/abs','C:/drive','a\\b','a//b','a/./b','a/../b','ads:key','name.','name ','CON.txt','aux','LPT9.log','COM¹.txt','COM²','COM³.dat','LPT¹','LPT².log','LPT³']:
   with self.subTest(name=name),self.assertRaises(RuntimeError):v.tree_entries(b'100644 blob '+b'a'*40+b'\t'+name.encode()+b'\0')
 def test_newline_is_nul_framed_then_refused(self):
  # Git really accepts this name; the Windows export policy must not split it into two paths.
  obj=self.git('hash-object','-w','--stdin',input=b'public synthetic newline').strip()
  raw=b'100644 blob '+obj+b'\tline\nname.txt\0'
  with self.assertRaisesRegex(RuntimeError,'Windows source path'):v.tree_entries(raw)
 def test_duplicate_and_prefix_targets_refused(self):
  for names in [('A.txt','a.txt'),('a','a/b')]:
   raw=b''.join(b'100644 blob '+b'a'*40+b'\t'+n.encode()+b'\0' for n in names)
   with self.assertRaises(RuntimeError):v.tree_entries(raw)
 def test_modes_framing_and_utf8_refused(self):
  for raw in [b'',self.raw[:-1],b'120000 blob '+b'a'*40+b'\tlink\0',b'160000 commit '+b'a'*40+b'\tsub\0',b'100644 blob '+b'a'*40+b'\tbad\xff\0']:
   with self.assertRaises((RuntimeError,ValueError)):v.tree_entries(raw)
 def test_count_and_size_limits(self):
  with patch.object(v,'MAX_FILES',1),self.assertRaises(RuntimeError):self.entries()
  e=self.entries();ids=b''.join(oid.encode()+b'\n' for _,oid in e);raw=self.git('cat-file','--batch-check',input=ids)
  with patch.object(v,'MAX_BLOB',1),self.assertRaises(RuntimeError):v.blob_sizes(raw,e)
  with patch.object(v,'MAX_TOTAL',1),self.assertRaises(RuntimeError):v.blob_sizes(raw,e)
 def test_inventory_source_type_count_fail_closed(self):
  e=self.entries()
  for raw in [b'',(b'a'*40+b' blob 1\n')*len(e),(e[0][1].encode()+b' tree 0\n')*len(e)]:
   with self.assertRaises(RuntimeError):v.blob_sizes(raw,e)
 def test_truncated_extra_corrupt_and_source_mismatch(self):
  e=self.entries();p,s=self.pack(e)
  bad=[p[:-2],p+b'extra',p.replace(b'blob',b'tree',1),b'b'*40+p[40:],p.replace(self.data['payload.dat'],b'X'*len(self.data['payload.dat']))]
  for packed in bad:
   with self.assertRaises(RuntimeError):self.write(e,packed,s)
 def test_existing_root_and_symlink_rejected_without_overwrite(self):
  e=self.entries();packed,s=self.pack(e)
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/'pack';p.write_bytes(packed);root=Path(d)/'source';root.mkdir();(root/'keep').write_bytes(b'keep')
   with self.assertRaises(FileExistsError):v.write_blobs(p,root,e,s)
   self.assertEqual(b'keep',(root/'keep').read_bytes())
   link=Path(d)/'link';link.symlink_to(root,target_is_directory=True)
   with self.assertRaises(RuntimeError):v.write_blobs(p,link,e,s)
class CommandFailure(unittest.TestCase):
 def test_nonzero_git_stops_before_export_or_platform_operations(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);event=root/'event.json';event.write_text('{}');work=root/'work'
   helper=Mock();helper.linked.return_value=False
   def child(argv,**kwargs):
    kwargs['stdout'].write(b'synthetic git failure\n');return subprocess.CompletedProcess(argv,7)
   with patch.object(v,'WORK',work),patch.object(v,'context'),patch.object(v,'load',return_value=helper),patch.dict(os.environ,{'GITHUB_EVENT_PATH':str(event),'GITHUB_SHA':'a'*40}),patch.object(v.subprocess,'run',side_effect=child) as command:
    with self.assertRaisesRegex(RuntimeError,'Command failed/bound: head'):v.main('prepare')
   self.assertEqual(1,command.call_count);self.assertEqual(['git','rev-parse','HEAD'],command.call_args.args[0])
   report=json.loads((work/'prepare-result.json').read_text());self.assertEqual(7,report['Commands'][0]['Exit']);self.assertEqual('Blocked',report['Status'])
   self.assertFalse((work/'source-before').exists());self.assertTrue((work/'prepare-head.log').exists())
if __name__=='__main__':unittest.main()
