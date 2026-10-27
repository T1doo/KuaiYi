"""Offline diagnostic fixtures; never create a Windows owner or launch a child."""
import contextlib
import ctypes
import importlib.util
import io
import json
from pathlib import Path
import runpy
import tempfile
import unittest
from unittest.mock import patch

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('prejob_diagnostic',HERE/'diagnose_visual_prejob.py');d=importlib.util.module_from_spec(spec);spec.loader.exec_module(d)

class DiagnosticTests(unittest.TestCase):
    def fixture(self,root,raw=None):
        for relative in d.SOURCES:
            p=root/relative;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes((HERE.parent/relative).read_bytes())
        event=root/'event.json';event.write_bytes(raw if raw is not None else json.dumps({'repository':{'private':False,'description':'丁 SECRET_MARKER'}},ensure_ascii=False).encode())
        return {'GITHUB_ACTIONS':'true','GITHUB_REPOSITORY':'T1doo/KuaiYi','GITHUB_REF':'refs/heads/dev/ui-polish-visual-r03','GITHUB_EVENT_NAME':'push','GITHUB_RUN_ATTEMPT':'1','RUNNER_OS':'Windows','GITHUB_SHA':'a'*40,'GITHUB_EVENT_PATH':str(event)}
    def invoke(self,root,env,**kwargs):
        output=io.StringIO()
        with contextlib.redirect_stdout(output),patch('subprocess.Popen',side_effect=AssertionError('No child allowed')),patch('subprocess.run',side_effect=AssertionError('No child allowed')),patch.object(ctypes,'WinDLL',side_effect=AssertionError('No Windows Job API allowed'),create=True):
            code=d.diagnose(root,env,'win32',encoding='cp1252',**kwargs)
        p=root/'artifacts/ui-polish-bootstrap/prejob.json'
        data=json.loads(p.read_text()) if p.exists() else None
        return code,data,output.getvalue()
    def test_unicode_actual_gate_and_import_stop_before_job_without_payload(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);env=self.fixture(root);code,data,out=self.invoke(root,env)
            self.assertEqual(0,code);self.assertEqual('UnicodeError',data['LegacyDefaultDecode']);self.assertEqual('StoppedBeforeJobConstruction',data['Stage']);self.assertFalse(data['NativeJobCreated']);self.assertFalse(data['WpfStarted']);self.assertFalse(data['GitSourceCommitVerified']);self.assertFalse(data['ExplicitRegistryCalls']);self.assertNotIn('SECRET_MARKER',json.dumps(data)+out);self.assertLessEqual((root/'artifacts/ui-polish-bootstrap/prejob.json').stat().st_size,d.MAX_RECEIPT)
    def test_invalid_utf8_remains_failure_without_content(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);env=self.fixture(root,b'{"repository":{"private":false,"description":"\xff"}}');code,data,out=self.invoke(root,env)
            self.assertEqual(2,code);self.assertEqual('EventDecode',data['Stage']);self.assertEqual('UnicodeDecodeError',data['ErrorType'])
    def test_original_private_and_branch_gates_remain(self):
        for field in ('private','branch'):
            with self.subTest(field=field),tempfile.TemporaryDirectory() as directory:
                root=Path(directory);env=self.fixture(root)
                if field=='private':(root/'event.json').write_text('{"repository":{"private":true}}')
                else:env['GITHUB_REF']='refs/heads/main'
                code,data,out=self.invoke(root,env);self.assertEqual(2,code);self.assertEqual('OriginalContextGate',data['Stage'])
    def test_source_difference_or_size_stops_before_import(self):
        for raw in (b'changed',b'x'*(d.MAX_SOURCE+1)):
            with self.subTest(size=len(raw)),tempfile.TemporaryDirectory() as directory:
                root=Path(directory);env=self.fixture(root);(root/'scripts/windows_visual_job.py').write_bytes(raw)
                code,data,out=self.invoke(root,env,loader=lambda *a:self.fail('no import'));self.assertEqual(2,code);self.assertEqual('SourceParse',data['Stage'])
    def test_existing_root_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);env=self.fixture(root);target=root/'artifacts/ui-polish-bootstrap';target.mkdir(parents=True);(target/'original').write_bytes(b'keep')
            code,data,out=self.invoke(root,env);self.assertEqual(2,code);self.assertIsNone(data);self.assertEqual([target/'original'],list(target.iterdir()))
    def test_import_exception_records_class_without_message(self):
        def load(*args):raise RuntimeError('SECRET_MARKER https://invalid/?sig=secret')
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);env=self.fixture(root);code,data,out=self.invoke(root,env,loader=load)
            self.assertEqual(2,code);self.assertEqual('ModuleImport',data['Stage']);self.assertEqual('RuntimeError',data['ErrorType']);self.assertNotIn('SECRET_MARKER',json.dumps(data)+out);self.assertNotIn('sig=',json.dumps(data)+out)
    def test_initial_write_failure_closes_original_receipt_stream(self):
        class DiskFull(io.StringIO):
            def write(self,*a):raise OSError('SECRET_MARKER ENOSPC')
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);env=self.fixture(root);stream=DiskFull();original=Path.open;out=io.StringIO()
            def opened(path,*a,**kw):return stream if path.name=='prejob.json' else original(path,*a,**kw)
            with patch.object(Path,'open',opened),contextlib.redirect_stdout(out):code=d.diagnose(root,env,'win32',loader=lambda *a:self.fail('no import'))
            self.assertEqual(2,code);self.assertTrue(stream.closed);self.assertNotIn('SECRET_MARKER',out.getvalue());self.assertIn('ReceiptWriteError',out.getvalue())
    def test_close_failure_cannot_return_diagnostic_success(self):
        class CloseFail(io.StringIO):
            def close(self):super().close();raise OSError('SECRET_MARKER close')
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);env=self.fixture(root);stream=CloseFail();original=Path.open;out=io.StringIO()
            def opened(path,*a,**kw):return stream if path.name=='prejob.json' else original(path,*a,**kw)
            with patch.object(Path,'open',opened),contextlib.redirect_stdout(out):code=d.diagnose(root,env,'win32',encoding='cp1252')
            self.assertEqual(2,code);self.assertIn('"Status":"Fail"',out.getvalue());self.assertIn('ReceiptCloseError',out.getvalue());self.assertNotIn('SECRET_MARKER',out.getvalue())
    def test_event_input_bound_is_not_relaxed(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);env=self.fixture(root,b' '*(d.MAX_EVENT+1));code,data,out=self.invoke(root,env)
            self.assertEqual(2,code);self.assertEqual('EventDecode',data['Stage']);self.assertLessEqual((root/'artifacts/ui-polish-bootstrap/prejob.json').stat().st_size,d.MAX_RECEIPT)
    def test_default_cli_exits_before_diagnostic(self):
        with patch('sys.argv',['diagnose_visual_prejob.py']),patch.object(Path,'mkdir',side_effect=AssertionError('No output creation')),contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaises(SystemExit) as error:runpy.run_path(str(HERE/'diagnose_visual_prejob.py'),run_name='__main__')
        self.assertEqual(2,error.exception.code)

if __name__=='__main__':unittest.main()
