import pathlib,json,hashlib,subprocess,collections,xml.etree.ElementTree as E,shutil
A=pathlib.Path(__file__).parent;C=json.loads((A/'task.json').read_text());D=pathlib.Path('docs/test-evidence');ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def dump(p,v):pathlib.Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
def root(p):return E.parse(p).getroot()
def names(p):return collections.Counter(x.attrib['testName'] for x in root(p).findall('.//t:UnitTestResult',ns))
runs=C['FinalRuns'];frozen=json.loads((A/(runs[0]+'-source.json')).read_text());out={};dll={}
for name in runs:
 meta=json.loads((A/(name+'-executed.json')).read_text());assert meta['exit']==0 and meta['source']==frozen
 for p,h in frozen.items():assert sha(p)==h==sha(A/(name+'-source__'+p.replace('/','__')+'.txt'))
 trx=A/(name+'.trx')
 if trx.exists():
  count=root(trx).find('.//t:Counters',ns).attrib;assert int(count['failed'])==0 and int(count['notExecuted'])==0;out[name]={'counters':count,'trx':str(D/(C['Prefix']+name+'.trx'))}
 else:out[name]={'exit':0,'log':str(D/(C['Prefix']+name+'.log'))}
 target=C.get('Targets',{}).get(name,'tests/KuaiYi.Provider.Tests/bin/Release/net10.0/');dll[name]={p:h for p,h in meta['sha256'].items() if p.startswith(target)};assert dll[name]
 for p,h in dll[name].items():assert sha(p)==h
full=C['FullRun'];assert '--filter' not in (A/(full+'-command.sh')).read_text();old=names(C['ParentTrx']);now=names(A/(full+'.trx'));new=now-old;assert now==old+new and sum(new.values())==C['NewTests'];manifest=json.loads((A/'parent-manifest.json').read_text());allowed=C['AllowedOldModified']
for p,h in manifest.items():
 if p not in allowed:assert sha(p)==h,p
v={'SourceParent':C['Parent'],'Branch':C['Branch'],'Scope':C['Scope'],'FrozenSource':frozen,'FinalSourceBindings':len(frozen)*len(runs),'Runs':out,'RelevantTargetDlls':dll,'DllScopeNote':'Other pre-existing selected DLLs are observations only; only the command target closure executed/built.','NameComparison':{'parent':sum(old.values()),'final':sum(now.values()),'new':dict(new),'exactParentPlusNew':True},'Preservation':{'parentTracked':len(manifest),'allowedOldModified':allowed,'allOtherParentSameBytes':len(manifest)-len(allowed),'noDeletes':True},'Review':{'Source':C['SourceReview'],'Packet':'Pending separate final audit'},'Notes':C['Notes'],'Boundaries':C['Boundaries']};v.update(C.get('AdditionalValidation',{}));dump(A/'analysis.json',v);raw={}
for p in sorted(A.iterdir()):
 if p.is_file():
  d=D/(C['Prefix']+p.name);shutil.copyfile(p,d);raw[str(d)]={'sha256':sha(d),'bytes':d.stat().st_size,'artifact':str(p)}
v|={'RawEvidenceCount':len(raw),'RawEvidence':raw};dump(D/C['Index'],v);attrs=pathlib.Path('.gitattributes');s=attrs.read_text();lines=s.splitlines();extra=[p+' -text' for p in raw if p+' -text' not in lines]
if extra:attrs.write_text(s+'\n# '+C['Branch']+' exact original bytes\n'+'\n'.join(extra)+'\n')
print(json.dumps({'raw':len(raw),'sourceBindings':v['FinalSourceBindings'],'counts':{k:r.get('counters',{}) for k,r in out.items()},'otherParentSame':v['Preservation']['allOtherParentSameBytes']}))
