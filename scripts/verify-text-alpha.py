"""Read-only independent artifact audit, usable on Linux. Does not launch a Windows host."""
import argparse,hashlib,json,pathlib,struct,zipfile
parser=argparse.ArgumentParser();parser.add_argument('bundle',type=pathlib.Path);parser.add_argument('--archive',type=pathlib.Path);parser.add_argument('--output',type=pathlib.Path);args=parser.parse_args()
root=args.bundle.resolve();manifest=json.loads((root/'text-alpha-manifest.json').read_text());checks=[]
def check(label,value):
 checks.append(label)
 if not value:raise AssertionError(label)
def digest(data):return hashlib.sha256(data).hexdigest()
def pe(data):
 if len(data)<128 or data[:2]!=b'MZ':return False
 offset=struct.unpack_from('<I',data,0x3c)[0]
 return offset>=64 and offset+26<=len(data) and data[offset:offset+4]==b'PE\0\0' and struct.unpack_from('<H',data,offset+4)[0]==0x8664 and struct.unpack_from('<H',data,offset+24)[0]==0x20b
check('honest managed Alpha',manifest['Kind']=='KuaiYiManagedTextAlpha' and manifest['SelfContained'] and not manifest['Installer'] and manifest['ProductNativeDlls']=='NotIncluded' and manifest['WindowsExecution']=='NotRun' and manifest['Runtime']=='10.0.12' and manifest['Rid']=='win-x64')
seen=set()
for item in manifest['Files']:
 path=pathlib.PurePosixPath(item['Path']);check('safe path '+str(path),not path.is_absolute() and '..' not in path.parts and '\\' not in str(path) and str(path).casefold() not in seen);seen.add(str(path).casefold())
 data=(root/path).read_bytes();check('file '+str(path),len(data)==item['Bytes'] and digest(data)==item['Sha256'])
actual={str(p.relative_to(root)).casefold() for p in root.rglob('*') if p.is_file() and p.name!='text-alpha-manifest.json'};check('exact manifest inventory',actual==seen)
allowed={'KuaiYi.App':'app','KuaiYi.CaptureBroker':'app','KuaiYi.ClipboardHost':'app','KuaiYi.CompatibilityProbe':'diagnostics','KuaiYi.GrowthHarness':'diagnostics','KuaiYi.MockProvider':'test-provider'}
check('exact six roles',{r['name']:r['folder'] for r in manifest['Roles']}==allowed and len(manifest['Roles'])==6)
for role in manifest['Roles']:
 name=role['name'];folder=root/role['folder'];check('PE '+name,pe((folder/(name+'.exe')).read_bytes()))
 options=json.loads((folder/(name+'.runtimeconfig.json')).read_text())['runtimeOptions']
 check('self-contained '+name,'framework' not in options and 'frameworks' not in options and bool(options.get('includedFrameworks')) and all(f['version']=='10.0.12' for f in options['includedFrameworks']))
 for file in ('coreclr.dll','hostfxr.dll','hostpolicy.dll'):check('runtimePE '+name+' '+file,pe((folder/file).read_bytes()))
 check('runtime managed '+name,(folder/'System.Private.CoreLib.dll').is_file())
 deps=json.loads((folder/(name+'.deps.json')).read_text())
 check('win-x64 deps '+name,deps['runtimeTarget']['name'].endswith('/win-x64'))
 if role['folder']!='test-provider':check('no test-provider dependency '+name,not any('MockProvider' in item or '.Tests/' in item for item in deps['libraries']))
for file in ('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll','wpfgfx_cor3.dll'):check('desktop runtime '+file,(root/'app'/file).is_file())
check('tray icon',(root/'app/Assets/KuaiYi.ico').read_bytes()[:4]==b'\x00\x00\x01\x00')
for path,producers in manifest['RoleProvenance'].items():
 data=(root/path).read_bytes()
 for producer in producers:
  original=root.parent/'publish'/producer['Role']/producer['PublishPath'];check('role merge '+producer['Role']+' '+path,original.read_bytes()==data and digest(data)==producer['Sha256'])
archive=None
if args.archive:
 with zipfile.ZipFile(args.archive) as z:
  expected={i['Path'] for i in manifest['Files']}|{'text-alpha-manifest.json'}
  check('ZIP exact inventory',set(z.namelist())==expected and len(z.namelist())==len(expected))
  for path in z.namelist():check('ZIP bytes '+path,z.read(path)==(root/path).read_bytes())
 archive={'path':str(args.archive),'bytes':args.archive.stat().st_size,'sha256':digest(args.archive.read_bytes())}
result={'allPassed':True,'checks':len(checks),'roles':len(allowed),'files':len(manifest['Files']),'Runtime':'10.0.12','SelfContained':True,'WindowsExecution':'NotRun','Installer':False,'ProductNativeDlls':'NotIncluded','RealModels':'None','archive':archive}
if args.output:args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(result,ensure_ascii=False))
