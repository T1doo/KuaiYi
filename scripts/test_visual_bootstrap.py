"""Only the original pre-Job AST prefix executes; no Windows or process API."""
import ast
import json
import os
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

SOURCE=Path(__file__).with_name('windows_visual_job.py').resolve()

def prefix_code():
    tree=ast.parse(SOURCE.read_text(encoding='utf-8'))
    main=next(n for n in tree.body if isinstance(n,ast.If) and isinstance(n.test,ast.Compare) and isinstance(n.test.left,ast.Name) and n.test.left.id=='__name__')
    prefix=[]
    for node in main.body:
        if isinstance(node,ast.Try):break
        prefix.append(node)
    if not prefix or len(prefix)==len(main.body):raise AssertionError('Original pre-Job boundary missing')
    module=ast.Module(body=prefix,type_ignores=[])
    for node in ast.walk(module):
        if isinstance(node,ast.Call) and isinstance(node.func,ast.Name) and node.func.id in {'JobOwner','WindowsJobApi','supervise'}:raise AssertionError('Never execute native owner in offline prefix')
    return compile(ast.fix_missing_locations(module),str(SOURCE),'exec')

class BootstrapEncoding(unittest.TestCase):
    def run_prefix(self,raw,*,branch='refs/heads/dev/ui-polish-visual-r03'):
        env={'GITHUB_ACTIONS':'true','GITHUB_REPOSITORY':'T1doo/KuaiYi','GITHUB_REF':branch,'GITHUB_EVENT_NAME':'push','GITHUB_RUN_ATTEMPT':'1','RUNNER_OS':'Windows','GITHUB_SHA':'0a314a6eaa16ea580326a29ab7fc30032f12cada'}
        with tempfile.TemporaryDirectory() as directory:
            event=Path(directory)/'event.json';event.write_bytes(raw);env['GITHUB_EVENT_PATH']=str(event)
            original=Path.open
            def simulated_non_utf8_default(path,*args,**kwargs):
                if path==event and kwargs.get('encoding') in (None,'locale'):kwargs['encoding']='cp1252'
                return original(path,*args,**kwargs)
            namespace={'Path':Path,'os':os,'sys':types.SimpleNamespace(platform='win32'),'json':json,'__file__':str(SOURCE)}
            with patch.dict(os.environ,env,clear=True),patch.object(Path,'open',simulated_non_utf8_default),patch('subprocess.Popen',side_effect=AssertionError('No child process allowed')),patch('subprocess.run',side_effect=AssertionError('No child command allowed')):
                exec(prefix_code(),namespace)
            return namespace['event']
    def payload(self,description='ASCII',private=False):return json.dumps({'repository':{'private':private,'description':description}},ensure_ascii=False).encode('utf-8')
    def test_unicode_utf8_preserved_before_any_native_owner(self):self.assertEqual('丁／快译',self.run_prefix(self.payload('丁／快译'))['repository']['description'])
    def test_ascii_original_context_still_accepts(self):self.assertEqual('ASCII',self.run_prefix(self.payload())['repository']['description'])
    def test_invalid_utf8_still_refused(self):
        with self.assertRaises(UnicodeDecodeError):self.run_prefix(b'{"repository":{"private":false,"description":"\xff"}}')
    def test_wrong_branch_still_refused(self):
        with self.assertRaises(RuntimeError):self.run_prefix(self.payload(),branch='refs/heads/dev/ui-polish-visual-r02')
    def test_private_repository_still_refused(self):
        with self.assertRaises(RuntimeError):self.run_prefix(self.payload(private=True))

if __name__=='__main__':unittest.main()
