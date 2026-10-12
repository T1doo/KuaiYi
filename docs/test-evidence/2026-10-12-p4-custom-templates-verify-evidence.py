"""Independent byte/retention/packaging verification; never executes a model or a Windows host."""
import collections,hashlib,json,pathlib,struct,subprocess,sys,xml.etree.ElementTree as ET,zipfile
root=pathlib.Path(__file__).resolve().parents[2];path=pathlib.Path(__file__).with_name('2026-10-12-p4-custom-templates-summary.json');summary=json.loads(path.read_text());checks=[]
def record(label,ok):
 checks.append({'check':label,'pass':bool(ok)})
 if not ok:raise AssertionError(label)
def digest_ok(item,data):return len(data)==item['bytes'] and hashlib.sha256(data).hexdigest()==item['sha256']
def results(data):return collections.Counter(i.attrib['testName'] for i in ET.fromstring(data).iter() if i.tag.endswith('}UnitTestResult'))
for group in ('sourceDigests','evidenceFiles','reviewDigests'):
 record(group,all(digest_ok(i,(root/i['path']).read_bytes()) for i in summary[group]))
record('verifier bytes',digest_ok(summary['verifier'],pathlib.Path(__file__).read_bytes()))
for commit,name in summary['historicalSummaries'].items():
 historic=json.loads(subprocess.check_output(['git','show',commit+':'+name],cwd=root))
 for group in ('sourceDigests','evidenceFiles','reviewDigests'):
  for item in historic.get(group,[]):
   data=subprocess.check_output(['git','show',commit+':'+item['path']],cwd=root)
   record(commit+' '+item['path'],digest_ok(item,data))
for item in summary['originalProductionEvidence']:
 data=(root/item['path']).read_bytes()
 record('original baseline '+item['originalPath'],digest_ok(item,data) and data==subprocess.check_output(['git','show',summary['sourceBaseCommit']+':'+item['originalPath']],cwd=root))
for item in summary['historicalArchives']+[summary['archive']]:
 record('archive '+item['archive'],digest_ok(dict(item,path=item['archive']),(root/item['archive']).read_bytes()))
with zipfile.ZipFile(root/summary['archive']['archive']) as archive:
 manifest=json.loads(archive.read('review-manifest.json'))
 record('manifest entries',all(len(archive.read(i['Path']))==i['Bytes'] and hashlib.sha256(archive.read(i['Path'])).hexdigest()==i['Sha256'] for i in manifest['Files']))
 record('manifest exact inventory',set(archive.namelist())=={i['Path'] for i in manifest['Files']}|{'review-manifest.json'})
 for name in summary['archive']['actualPe32PlusX64Hosts']:
  data=archive.read(name);offset=struct.unpack_from('<I',data,0x3c)[0]
  record('PE '+name,data[:2]==b'MZ' and data[offset:offset+4]==b'PE\0\0' and struct.unpack_from('<H',data,offset+4)[0]==0x8664 and struct.unpack_from('<H',data,offset+24)[0]==0x20b)
 deps=json.loads(archive.read('app/KuaiYi.App.deps.json'))
 record('actual production graph',any('KuaiYi.Providers' in i for i in deps['libraries']) and not any('MockProvider' in i for i in deps['libraries']))
 record('app command published bytes',archive.read('app/KuaiYi.App.dll')==(root/'src/KuaiYi.App/bin/Release/net10.0-windows/win-x64/KuaiYi.App.dll').read_bytes())
 record('provider published bytes',archive.read('app/KuaiYi.Providers.dll')==(root/'src/KuaiYi.Providers/bin/Release/net10.0/KuaiYi.Providers.dll').read_bytes())
 record('test server separate', 'test-provider/KuaiYi.MockProvider.dll' in archive.namelist() and 'app/KuaiYi.MockProvider.dll' not in archive.namelist())
base=summary['sourceBaseCommit'];old=json.loads(subprocess.check_output(['git','show',base+':docs/test-evidence/2026-10-12-p4-session-style-summary.json'],cwd=root))
for kind in ('core','provider'):
 before=results(subprocess.check_output(['git','show',base+':'+old['offline'][kind+'Trx']],cwd=root));after=results((root/summary['offline'][kind+'Trx']).read_bytes())
 record('all original tests '+kind,not before-after and sum(after.values())==int(summary['offline'][kind]['total']))
 record('whole '+kind+' pass',summary['offline'][kind]['failed']=='0' and summary['offline'][kind]['notExecuted']=='0' and summary['offline'][kind]['passed']==summary['offline'][kind]['total'])
for kind,n in [('core',30),('provider',37)]:
 trx=ET.parse(root/summary['focused'][kind+'Trx']);counts=next(i.attrib for i in trx.iter() if i.tag.endswith('}Counters'))
 record('new focused '+kind,counts['passed']==str(n) and counts['total']==str(n) and counts['failed']=='0')
for item in summary['initialFailures']+summary['recoveryFailures']:
 counts=next(i.attrib for i in ET.parse(root/item['trx']).iter() if i.tag.endswith('}Counters'))
 record('original failures '+item['trx'],counts['failed']==str(item['failed']) and counts['total']==str(item['total']))
for item in summary['originalTemplatesAudit']:
 record('original template '+item['path'],digest_ok(item,(root/item['path']).read_bytes()) and (root/item['path']).read_bytes()==subprocess.check_output(['git','show',summary['sourceBaseCommit']+':'+item['path']],cwd=root))
composer=(root/summary['phaseEvidence']['sharedComposer']).read_text();dispatch=(root/summary['phaseEvidence']['sharedDispatch']).read_text();ui=(root/'src/KuaiYi.App/SettingsTranslation.cs').read_text()
record('preview and request same composer','PromptBuilder.Compose' in dispatch and 'PromptBuilder.Compose' in (root/'src/KuaiYi.Providers/PromptTemplateEditor.cs').read_text() and 'promptEditor.Preview' in ui)
record('fixed base is still built-in','string basis=Read("base")' in composer and 'string system=basis+' in composer)
record('copy/reset mutate after safety notification','{beforeEdit();' in (root/'src/KuaiYi.Providers/PromptTemplateEditor.cs').read_text())
record('actual settings command wiring','promptEditor=new(()=>draft,ApiFormEdited)' in ui and 'CopyBuiltIn()' in ui and 'RestoreDefault()' in ui and 'LoadTranslationTemplates();' in (root/'src/KuaiYi.App/SettingsWindow.cs').read_text())
schema=json.loads((root/summary['phaseEvidence']['schemaFile']).read_text());definition=schema['properties']['translation']['properties']['customTemplates'];record('schema describes actual template mapping',definition['maxProperties']==4 and definition['additionalProperties']['maxLength']==8192 and definition['propertyNames']['enum']==['Faithful','Plain','Professional','Explain'])
defaults=json.loads((root/summary['phaseEvidence']['defaultsFile']).read_text());record('default has no custom template and original schema',defaults['schemaVersion']==1 and defaults['translation']['customTemplates']=={})
resume=json.loads((root/summary['phaseEvidence']['disconnectEvidence']).read_text());record('actual recovered command retained, no duplicate CI',resume['firstSuccessfulCommandUtc']=='2026-10-12 00:53:20 UTC' and resume['head']==summary['sourceBaseCommit'] and resume['originalCiPid']==563098 and not resume['duplicateStarted'] and not resume['permissionsExpanded'])
record('Node63 actual',len(ET.parse(root/summary['offline']['browserXml']).findall('.//testcase'))==63)
for item in summary['lockAudit']['files']:
 record('lock digest '+item['path'],digest_ok(item,(root/item['path']).read_bytes()))
 before=json.loads(subprocess.check_output(['git','show',base+':'+item['path']],cwd=root))['dependencies'];after=json.loads((root/item['path']).read_bytes())['dependencies']
 record('all original NuGet '+item['path'],all(after[framework].get(name)==value for framework,entries in before.items() for name,value in entries.items() if value['type']!='Project'))
 if not item['changed']:record('unchanged lock '+item['path'],before==after)
trace=json.loads((root/'docs/traceability.json').read_text());whole=[i for i in trace['items'] if i['category']=='acceptance']
record('trace176/whole106 NotRun',len(trace['items'])==176 and len(whole)==106 and all(i['validationStatus']=='NotRun' for i in whole))
record('original spec bytes',(root/'SPEC.md').read_bytes()==(root/'快译_产品与技术开发规格_v1.0.md').read_bytes() and hashlib.sha256((root/'SPEC.md').read_bytes()).hexdigest()==summary['environment']['sourceSpecSha256'])
output={'allPassed':True,'checks':len(checks),'historicalSummaries':len(summary['historicalSummaries']),'archives':len(summary['historicalArchives'])+1,'currentArchive':summary['archive'],'tests':1607,'focused':67,'Node':63,'Windows':'NotRun','RemoteCi':'NotRun','ModelCalls':'None'}
if len(sys.argv)>1:pathlib.Path(sys.argv[1]).write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(output,ensure_ascii=False))
