import zipfile,json,hashlib,struct,pathlib,os
root=pathlib.Path('artifacts/test-results/ole-presentation')
p=pathlib.Path('artifacts/KuaiYi-P0-ole-presentation-managed-win-x64.zip')
with zipfile.ZipFile(p) as z:
 files={x.filename:z.read(x) for x in z.infolist() if not x.is_dir()};manifest=json.loads(files['review-manifest.json'])
 for f in manifest['Files']:
  data=files[f['Path']];assert len(data)==f['Bytes'];assert hashlib.sha256(data).hexdigest()==f['Sha256']
 assert len(files)==len(manifest['Files'])+1
 assert not any('Fixture' in n or os.path.basename(n) in {'config.json','receipt.json','native-host-install.json'} or n.endswith(('.key','.pfx')) for n in files)
 assert not any(n.endswith(('KuaiYi.OleNativeBridge.dll','KuaiYi.CaptureNative.dll','KuaiYi.OcrNative.dll','KuaiYi.ShellExtension.dll')) for n in files)
 hosts=[]
 for n,data in files.items():
  if not n.endswith('.exe'):continue
  pe=struct.unpack_from('<I',data,60)[0];assert data[:2]==b'MZ';assert data[pe:pe+4]==b'PE\0\0';assert struct.unpack_from('<H',data,pe+4)[0]==0x8664;assert struct.unpack_from('<H',data,pe+24)[0]==0x20b
  if n.endswith(('KuaiYi.CaptureHost.exe','KuaiYi.OleReceiver.exe')):assert b'PerMonitorV2' in data and b'asInvoker' in data
  hosts.append({'path':n,'machine':'x64','format':'PE32+','WindowsExecution':'NotRun'})
 result={'archive':str(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'files':len(files),'appAssets':sum(n.startswith('app/') for n in files),'nativeSources':sum(n.startswith('ole-native-source/') for n in files),'manifestHashes':'Pass','executableHeaders':hosts,'NativeDLLs':'NotIncluded','Fixtures':'NotIncluded','Installer':False,'WindowsRuntime':'NotRun'}
 (root/'archive-validation.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,ensure_ascii=False))
