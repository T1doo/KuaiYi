import importlib.util,json,tempfile,unittest,hashlib
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('visual_pub',Path(__file__).with_name('visual_publication.py'));v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
ENV={'GITHUB_SHA':'a'*40,'GITHUB_RUN_ID':'12345','GITHUB_RUN_ATTEMPT':'1'}
REPORT={'SourceHead':'a'*40,'OriginalEvidenceRootAcquired':True,'OriginalJobHandleClosed':True,'Status':'Pass_OwnValidationSubtree_AwaitingParentWrapperExit'}
class PublicationTests(unittest.TestCase):
 def fixture(self,root):
  work=root/'artifacts/ui-polish-windows';work.mkdir(parents=True);(work/'validation-tree.json').write_text(json.dumps(REPORT));(work/'prepare-result.json').write_text('{"Status":"Pass_Prepared_NotExecuted"}')
  view=work/'before-settings/raster';view.mkdir(parents=True);(view/'Light-wide.png').write_bytes(b'\x89PNG\r\n\x1a\nraw-fixture');(view/'ui-polish-result.json').write_text('{"SyntheticSource":true}')
  return work
 def claim(self,root):
  parent=root/'artifacts/ui-polish-visual-parent';parent.mkdir();v.write_json(parent/'namespace.json',{'Kind':'OwnedVisualNamespaceClaim','Identity':v.identity(ENV),'NativeAndPublicationRootsWereAbsent':True},4096)
  return parent
 def test_complete_owned_snapshot_and_parent_projection(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);pub=v.publish_owned(work,REPORT,ENV);ready=v.verify_snapshot(pub,v.identity(ENV));self.assertEqual(4,ready['Files']);self.assertEqual(REPORT['Status'],ready['NativeState']['Status'])
   self.claim(root);out=root/'output';self.assertEqual(0,v.qualify(root,ENV,out));self.assertEqual('parent=true\nvisual=true\n',out.read_text())
 def test_known_empty_fixture_locks_are_preserved(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);state=work/'before-settings/raster/state/settings-Light/config';state.mkdir(parents=True)
   for name in ['settings.lock','secret-transaction.lock']:(state/name).write_bytes(b'')
   pub=v.publish_owned(work,REPORT,ENV);ready=v.verify_snapshot(pub,v.identity(ENV));self.assertEqual(6,ready['Files'])
   for name in ['settings.lock','secret-transaction.lock']:self.assertEqual(b'',(pub/'before-settings/raster/state/settings-Light/config'/name).read_bytes())
 def test_nonempty_or_unknown_fixture_lock_refused(self):
  for name,raw in [('settings.lock',b'not empty'),('unknown.lock',b'')]:
   with self.subTest(name=name),tempfile.TemporaryDirectory() as d:
    root=Path(d);work=self.fixture(root);state=work/'before-settings/raster/state/settings-Light/config';state.mkdir(parents=True);(state/name).write_bytes(raw)
    with self.assertRaises(RuntimeError):v.publish_owned(work,REPORT,ENV)
    self.assertFalse((root/'artifacts/ui-polish-publication/READY.json').exists())
 def test_unknown_snapshot_never_certifies_cleanup_or_test(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);r=dict(REPORT,Status='UNKNOWN',OriginalJobHandleClosed=False);ready=v.verify_snapshot(v.publish_owned(work,r,ENV),v.identity(ENV));self.assertFalse(ready['NativeState']['OriginalJobHandleClosed']);self.assertEqual('UNKNOWN',ready['NativeState']['Status']);self.assertIn('NotTestPass',ready['Kind'])
 def test_unacquired_or_wrong_source_refused(self):
  for changes in [{'OriginalEvidenceRootAcquired':False},{'SourceHead':'b'*40}]:
   with tempfile.TemporaryDirectory() as d:
    work=self.fixture(Path(d))
    with self.assertRaises(RuntimeError):v.publish_owned(work,dict(REPORT,**changes),ENV)
 def test_empty_failed_root_preserves_typed_error_not_message(self):
  with tempfile.TemporaryDirectory() as d:
   work=Path(d)/'artifacts/ui-polish-windows';work.mkdir(parents=True)
   report=dict(REPORT,Status='Fail',ErrorType='OSError',Error='SECRET_MARKER')
   ready=v.verify_snapshot(v.publish_owned(work,report,ENV),v.identity(ENV))
   self.assertEqual(0,ready['Files']);self.assertEqual('OSError',ready['NativeState']['ErrorType']);self.assertNotIn('SECRET_MARKER',json.dumps(ready))
 def test_existing_publication_root_not_modified(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);pub=root/'artifacts/ui-polish-publication';pub.mkdir();(pub/'original').write_bytes(b'keep')
   with self.assertRaises(FileExistsError):v.publish_owned(work,REPORT,ENV)
   self.assertEqual(b'keep',(pub/'original').read_bytes())
 def test_sensitive_query_across_read_boundary_retains_original_no_ready(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);raw=b'x'*(1024*1024-4)+b'?sig=secret';(work/'raw.log').write_bytes(raw)
   with self.assertRaises(RuntimeError):v.publish_owned(work,REPORT,ENV)
   self.assertEqual(raw,(work/'raw.log').read_bytes());self.assertFalse((root/'artifacts/ui-polish-publication/READY.json').exists())
 def test_standard_signed_queries_across_read_boundary_refused(self):
  for query in (b'?X-Amz-Signature=fixture',b'&X-Amz-Credential=fixture',b'?signature=fixture',b'?X-Goog-Signature=fixture',b'&X-Goog-Credential=fixture'):
   with self.subTest(key=query.split(b'=')[0]),tempfile.TemporaryDirectory() as d:
    root=Path(d);work=self.fixture(root);raw=b'x'*(1024*1024-8)+query;(work/'signed.log').write_bytes(raw)
    with self.assertRaises(RuntimeError):v.publish_owned(work,REPORT,ENV)
    self.assertEqual(raw,(work/'signed.log').read_bytes());self.assertFalse((root/'artifacts/ui-polish-publication/READY.json').exists())
 def test_source_mutation_during_copy_refused(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);original=v.sha;source=work/'prepare-result.json'
   def changed(p):
    if p==source:source.write_text('{"changed":true}')
    return original(p)
   with patch.object(v,'sha',changed),self.assertRaises(RuntimeError):v.publish_owned(work,REPORT,ENV)
   self.assertFalse((root/'artifacts/ui-polish-publication/READY.json').exists())
 def test_symlink_and_unregistered_file_refused(self):
  for linked in (True,False):
   with tempfile.TemporaryDirectory() as d:
    root=Path(d);work=self.fixture(root)
    if linked:(work/'bad.log').symlink_to(work/'prepare-result.json')
    else:(work/'before-settings/private.bin').write_bytes(b'not registered')
    with self.assertRaises(RuntimeError):v.publish_owned(work,REPORT,ENV)
 def test_count_and_total_bounds_no_ready(self):
  for name,value in [('MAX_FILES',2),('MAX_TOTAL',5)]:
   with tempfile.TemporaryDirectory() as d:
    root=Path(d);work=self.fixture(root)
    with patch.object(v,name,value),self.assertRaises(RuntimeError):v.publish_owned(work,REPORT,ENV)
    self.assertFalse((root/'artifacts/ui-polish-publication/READY.json').exists())
 def test_modified_or_extra_snapshot_bytes_refused(self):
  for extra in (False,True):
   with tempfile.TemporaryDirectory() as d:
    root=Path(d);work=self.fixture(root);pub=v.publish_owned(work,REPORT,ENV)
    (pub/('extra.log' if extra else 'prepare-result.json')).write_text('changed')
    with self.assertRaises(RuntimeError):v.verify_snapshot(pub,v.identity(ENV))
 def test_identity_difference_and_unclosed_parent_summary_not_promoted(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);pub=v.publish_owned(work,REPORT,ENV)
   with self.assertRaises(RuntimeError):v.verify_snapshot(pub,dict(v.identity(ENV),GITHUB_RUN_ID='999'))
   parent=self.claim(root);v.write_json(parent/'parent-exit.json',{'Kind':'OriginalVisualParentReturn','Identity':v.identity(ENV),'OriginalForegroundCommandReturned':False,'NativeExitCode':None,'ErrorType':'OSError'},4096)
   self.assertEqual(0,v.qualify(root,ENV,root/'output'));result=v.read_json(root/'artifacts/ui-polish-visual-parent-publication/parent-exit.json');self.assertFalse(result['OriginalForegroundCommandReturned'])
 def test_parent_extra_sensitive_field_refused_before_output_qualification(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);work=self.fixture(root);v.publish_owned(work,REPORT,ENV);p=self.claim(root)
   v.write_json(p/'parent-exit.json',{'Kind':'OriginalVisualParentReturn','Identity':v.identity(ENV),'OriginalForegroundCommandReturned':True,'NativeExitCode':0,'ErrorType':None,'extra':'?sig=secret'},4096)
   with self.assertRaises(RuntimeError):v.qualify(root,ENV,root/'output')
   self.assertFalse((root/'output').exists())
if __name__=='__main__':unittest.main()
