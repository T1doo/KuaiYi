import importlib.util,json,subprocess,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('owned_job',Path(__file__).with_name('windows_visual_job.py'));v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class FakeApi:
 def __init__(self,member=(1,[17]),assign_error=False):self.member=member;self.closed=[];self.assign_error=assign_error
 def create(self):return 'original-handle'
 def assign_current(self,h):
  if self.assign_error:raise PermissionError('synthetic denied')
 def members(self,h):
  assert h=='original-handle'
  if isinstance(self.member,Exception):raise self.member
  return self.member
 def close(self,h):self.closed.append(h)
class OwnerTests(unittest.TestCase):
 def test_only_self_confirms_and_closes_original(self):
  api=FakeApi();owner=v.JobOwner(api,17);self.assertEqual('Pass_OwnValidationDescendantsExited',owner.sample()['Status']);self.assertTrue(owner.close_if_confirmed());self.assertEqual(['original-handle'],api.closed)
 def test_descendants_do_not_pass_or_close(self):
  api=FakeApi((2,[17,18]));o=v.JobOwner(api,17);self.assertEqual('UNKNOWN',o.sample()['Status']);self.assertFalse(o.close_if_confirmed());self.assertEqual([],api.closed)
 def test_incomplete_duplicate_missing_self_capacity_remain_unknown(self):
  for members in [(3,[17]),(2,[17,17]),(1,[18]),(513,list(range(513)))]:
   with self.subTest(members=members):self.assertEqual('UNKNOWN',v.JobOwner(FakeApi(members),17).sample()['Status'])
 def test_query_denial_is_unknown_without_fallback(self):
  api=FakeApi(PermissionError('synthetic'));o=v.JobOwner(api,17);self.assertEqual('UNKNOWN',o.sample()['Status']);self.assertFalse(o.close_if_confirmed());self.assertEqual([],api.closed)
 def test_assign_denial_closes_empty_handle_and_never_adopts(self):
  api=FakeApi(assign_error=True)
  with self.assertRaises(PermissionError):v.JobOwner(api,17)
  self.assertEqual(['original-handle'],api.closed)
 def run_supervisor(self,exits,api=None):
  api=api or FakeApi();owner=v.JobOwner(api,17);launched=[];held=[]
  class Process:
   pid=21
   def wait(self,timeout=None):return exits[len(launched)-1]
  def launch(argv,**kwargs):launched.append(argv[-1]);return Process()
  with tempfile.TemporaryDirectory() as d:
   result=v.supervise(Path(d),{'GITHUB_SHA':'a'*40},owner,launch=launch,_hold=lambda:held.append(True))
   report=json.loads((Path(d)/'artifacts/ui-polish-windows/validation-tree.json').read_text())
  return result,launched,held,report
 def test_success_two_original_phases_and_tree_only_self(self):
  result,phases,held,r=self.run_supervisor([0,0]);self.assertEqual(0,result);self.assertEqual(['prepare','run'],phases);self.assertTrue(r['OriginalJobHandleClosed']);self.assertFalse(held)
 def test_prepare_error_never_launches_visual(self):
  result,phases,held,r=self.run_supervisor([7]);self.assertEqual(2,result);self.assertEqual(['prepare'],phases);self.assertEqual('Fail',r['Status']);self.assertFalse(held)
 def test_leaked_tree_retains_owner_and_stops_next_phase(self):
  result,phases,held,r=self.run_supervisor([0],FakeApi((2,[17,88])));self.assertEqual(2,result);self.assertEqual([],phases);self.assertTrue(held);self.assertEqual('UNKNOWN',r['Status']);self.assertFalse(r['OriginalJobHandleClosed'])
 def test_query_denied_before_any_child(self):
  result,phases,held,r=self.run_supervisor([],FakeApi(PermissionError('denied')));self.assertEqual([],phases);self.assertEqual('UNKNOWN',r['Status']);self.assertTrue(held)
 def test_prepare_wait_exit_with_surviving_descendant_holds_original(self):
  class Stateful(FakeApi):
   calls=0
   def members(self,h):
    self.calls+=1
    return (1,[17]) if self.calls==1 else (2,[17,88])
  api=Stateful();result,phases,held,r=self.run_supervisor([0],api)
  self.assertEqual(['prepare'],phases);self.assertEqual(2,result);self.assertTrue(held);self.assertFalse(api.closed);self.assertEqual('UNKNOWN',r['Status'])
 def test_final_query_failure_latches_unknown_and_holds_original(self):
  class FinalDenied(FakeApi):
   calls=0
   def members(self,h):
    self.calls+=1
    if self.calls>=4:raise PermissionError('final synthetic denial')
    return (1,[17])
  api=FinalDenied();result,phases,held,r=self.run_supervisor([0,0],api)
  self.assertEqual(['prepare','run'],phases);self.assertEqual(2,result);self.assertTrue(held);self.assertFalse(api.closed);self.assertEqual('UNKNOWN',r['Status'])
 def test_close_failure_is_single_attempt_unknown_and_retains_handle(self):
  class CloseDenied(FakeApi):
   def close(self,h):self.closed.append(h);raise OSError('synthetic close failure')
  api=CloseDenied();o=v.JobOwner(api,17)
  self.assertFalse(o.close_if_confirmed());self.assertFalse(o.close_if_confirmed());self.assertEqual(['original-handle'],api.closed);self.assertEqual('original-handle',o.handle)
  api=CloseDenied();result,phases,held,r=self.run_supervisor([0,0],api)
  self.assertEqual(2,result);self.assertTrue(held);self.assertEqual(['original-handle'],api.closed);self.assertEqual('UNKNOWN',r['Status'])
 def test_final_evidence_write_failure_cannot_release_unknown_owner(self):
  api=FakeApi((2,[17,88]));owner=v.JobOwner(api,17);held=[];writes=[];original=Path.write_text
  def write(path,*args,**kwargs):
   writes.append(path)
   if len(writes)>1:raise OSError('synthetic evidence disk full')
   return original(path,*args,**kwargs)
  with tempfile.TemporaryDirectory() as d,patch.object(Path,'write_text',write):
   result=v.supervise(Path(d),{'GITHUB_SHA':'a'*40},owner,launch=lambda *a,**k:self.fail('no launch'),_hold=lambda:held.append(True))
  self.assertEqual(2,result);self.assertTrue(held);self.assertFalse(api.closed);self.assertEqual('original-handle',owner.handle)
 def test_unknown_diagnostic_broken_pipe_still_holds_original(self):
  api=FakeApi((2,[17,88]));owner=v.JobOwner(api,17);held=[]
  with tempfile.TemporaryDirectory() as d,patch('builtins.print',side_effect=BrokenPipeError('synthetic pipe')):
   result=v.supervise(Path(d),{'GITHUB_SHA':'a'*40},owner,launch=lambda *a,**k:self.fail('no launch'),_hold=lambda:held.append(True))
  self.assertEqual(2,result);self.assertTrue(held);self.assertFalse(api.closed);self.assertEqual('original-handle',owner.handle)
 def test_original_wait_timeout_is_failure_not_cleanup_success(self):
  api=FakeApi();owner=v.JobOwner(api,17);calls=[];waits=[]
  class Process:
   pid=22
   def wait(self,timeout=None):
    waits.append(timeout)
    if timeout is not None:raise subprocess.TimeoutExpired('synthetic',timeout)
    return 0
  def launch(argv,**kwargs):calls.append(argv[-1]);return Process()
  with tempfile.TemporaryDirectory() as d:
   result=v.supervise(Path(d),{'GITHUB_SHA':'a'*40},owner,launch=launch,_hold=lambda:None)
   r=json.loads((Path(d)/'artifacts/ui-polish-windows/validation-tree.json').read_text())
  self.assertEqual(2,result);self.assertEqual(['prepare'],calls);self.assertEqual(2,len(waits));self.assertIsNone(waits[-1]);self.assertEqual('Fail',r['Status']);self.assertTrue(r['Phases'][0]['OriginalExitConfirmed']);self.assertIsNone(r['Phases'][0]['Exit'])
if __name__=='__main__':unittest.main()
