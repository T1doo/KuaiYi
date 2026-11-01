"""Local eight-role framework-dependent review build; never publish/install/run product.
All MSBuild bin/obj, temp and HTTP cache outputs use one fresh artifacts root.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import time

REPO=Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location("review_publisher",REPO/"scripts/publish-windows-managed.py")
pub=importlib.util.module_from_spec(spec);spec.loader.exec_module(pub)
spec=importlib.util.spec_from_file_location("review_zip",REPO/"scripts/package-preview.py")
pack=importlib.util.module_from_spec(spec);spec.loader.exec_module(pack)
BUDGET=256*1024*1024
FLOOR=1024**3
DOCS=("docs/release/README-managed-review.md","docs/release/FUNCTIONAL-GAPS-2026-11-01.md")
TOOLS=("scripts/build-managed-review.py","scripts/managed-review-metadata.cs")
HELPERS=("scripts/publish-windows-managed.py","scripts/package-preview.py","scripts/inspect-windows-payload.py",
         "scripts/session-sdk.py","scripts/prepare-session-test.py")


def sha(path):
    return pack.digest(path)


def authored():
    result={}
    for rel in (*DOCS,*TOOLS,*HELPERS):
        raw=subprocess.check_output(["git","show","HEAD:"+rel],cwd=REPO)
        if raw!=(REPO/rel).read_bytes():raise RuntimeError("Authored input differs from committed bytes")
        result[rel]=hashlib.sha256(raw).hexdigest()
    return result


def build(requested):
    out=pub.INPUTS.checked_output(REPO,requested)
    initial=shutil.disk_usage(REPO).free
    if initial<FLOOR+BUDGET:raise RuntimeError("Insufficient ordinary-build reservation; no command started")
    source=pub.product_sources();head=subprocess.check_output(["git","rev-parse","HEAD"],cwd=REPO,text=True).strip();extra=authored()
    out.mkdir();(out/"sources-before.json").write_text(json.dumps(source,indent=2)+"\n")
    report={"kind":"FrameworkDependentManagedReview_NotInstaller","status":"InProgress","sourceHead":head,"authored":extra,
            "selfContained":False,"productionPins":"Unavailable","windowsExecution":"NotRun","commands":[],"roles":[],
            "budgetBytes":BUDGET,"freeFloorBytes":FLOOR,"initialFreeBytes":initial,"maxObservedGrowthBytes":0,"maxObservedStageBytes":0}
    sdk,env=pub.SDK.environment(REPO)
    pub.INPUTS.RUNTIME.require_signature_environment(env)
    for folder in ("tmp","http-cache","plugin-cache","nuget-scratch","audit"):(out/folder).mkdir()
    env.update(MSBUILDDISABLENODEREUSE="1",TMPDIR=str(out/"tmp"),TMP=str(out/"tmp"),TEMP=str(out/"tmp"),
               NUGET_HTTP_CACHE_PATH=str(out/"http-cache"),NUGET_PLUGINS_CACHE_PATH=str(out/"plugin-cache"),NUGET_SCRATCH=str(out/"nuget-scratch"))
    original_scan=pub.output_size;original_execute=pub.execute
    def monitor(root):
        free=shutil.disk_usage(REPO).free;used=original_scan(out);growth=max(0,initial-free)
        report["maxObservedGrowthBytes"]=max(report["maxObservedGrowthBytes"],growth)
        report["maxObservedStageBytes"]=max(report["maxObservedStageBytes"],used)
        if free<FLOOR or growth>BUDGET-4*1024*1024 or used>BUDGET-4*1024*1024:raise RuntimeError("Ordinary-build resource bound exceeded; final receipt reserve retained")
        return used
    pub.output_size=monitor
    deadline=time.monotonic()+600
    def arm():
        remaining=deadline-time.monotonic()
        if remaining<=0:raise TimeoutError("Ordinary-build total 600-second bound exceeded")
        signal.setitimer(signal.ITIMER_REAL,remaining)
    def bounded_execute(argv,environment,log,until,work,receipt=None,**kwargs):
        row={} if receipt is None else receipt
        # No asynchronous alarm in Popen/ownership establishment or settlement.
        # The original supervisor owns command deadline + child cleanup.
        signal.setitimer(signal.ITIMER_REAL,0)
        if time.monotonic()>=deadline:raise TimeoutError("No command admission after deadline")
        original_execute(argv,environment,log,min(until,deadline),work,row,log_limit_bytes=1024*1024)
        arm()
        if row.get("exit")!=0 or row.get("settlement")!="LeaderJoined_GroupEmpty":raise RuntimeError("Original command failed or did not settle")
        monitor(out)
    pub.execute=bounded_execute
    def timeout(signum,frame):raise TimeoutError("Ordinary-build total 600-second bound exceeded")
    original_handler=signal.signal(signal.SIGALRM,timeout);arm()
    def run(label,args):
        row={"label":label};report["commands"].append(row)
        pub.execute([str(sdk/"dotnet"),*args],env,out/(label+".log"),deadline,out,row,log_limit_bytes=1024*1024)
        if row.get("exit")!=0 or row.get("settlement")!="LeaderJoined_GroupEmpty":raise RuntimeError("Original command failed or did not settle: "+label)
        monitor(out)
    try:
        assets=json.loads((REPO/"src/KuaiYi.App/obj/project.assets.json").read_text());folders=list(assets["packageFolders"])
        if len(folders)!=1:raise RuntimeError("One original package cache required")
        cache=Path(folders[0]);env["NUGET_PACKAGES"]=str(cache)
        if not cache.is_relative_to(REPO/"artifacts") or any(p.is_symlink() for p in (cache,*cache.parents)):raise RuntimeError("Original cache provenance differs")
        common=["--artifacts-path",str(out/"build"),"--disable-build-servers","-p:EnableWindowsTargeting=true","-p:SelfContained=false",
                "-p:RuntimeFrameworkVersion=10.0.13","-p:SourceRevisionId="+head,"-p:ContinuousIntegrationBuild=true",
                "-p:KuaiYiTextAlpha=false","-p:KuaiYiIsolatedSessionTest=false","-p:KuaiYiIncludeWindowsCiProbe=false",
                "-p:UseSharedCompilation=false","-nodeReuse:false","-maxcpucount:1","-p:NuGetAudit=true","-p:NuGetAuditMode=all","-p:NuGetAuditLevel=low"]
        # Metadata-only audit: PEReader never loads or runs any product assembly.
        refs=sorted((sdk/"packs/Microsoft.NETCore.App.Ref/10.0.13/ref/net10.0").glob("*.dll"))
        if not refs:raise RuntimeError("Locked SDK reference pack unavailable")
        audit=out/"audit/metadata.dll"
        run("metadata-compile",[str(sdk/"sdk/10.0.402/Roslyn/bincore/csc.dll"),"-nologo","-noconfig","-nostdlib+","-nullable:enable","-warnaserror+","-target:exe","-out:"+str(audit),*["-r:"+str(p) for p in refs],str(REPO/TOOLS[1])])
        audit.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.13"}}}))
        entries=[];managed=[]
        for role in pub.ROLES:
            project="src/KuaiYi."+role+"/KuaiYi."+role+".csproj";directory=out/"roles"/role
            run(role+"-restore",["restore",project,"--locked-mode","--configfile",str(REPO/"NuGet.Config"),*common])
            run(role+"-build",["build",project,"--no-restore","-c","Release","-o",str(directory),*common])
            config=json.loads((directory/("KuaiYi."+role+".runtimeconfig.json")).read_text())["runtimeOptions"]
            frameworks=config.get("frameworks",[config["framework"]] if "framework" in config else [])
            names=[f["name"] for f in frameworks]
            if ("includedFrameworks" in config or not frameworks or any(f["version"]!="10.0.13" for f in frameworks)
                or len(names)!=len(set(names)) or "Microsoft.NETCore.App" not in names
                or not set(names)<= {"Microsoft.NETCore.App","Microsoft.WindowsDesktop.App"}):raise RuntimeError("Framework-dependent runtime requirements differ")
            rows=pub.manifest(directory);report["roles"].append({"role":role,"frameworks":frameworks,"files":rows})
            for row in rows:
                rel=row["path"];path=directory/rel
                allowed=(re.fullmatch(r"KuaiYi\.[A-Za-z]+(?:\.(?:dll|exe|pdb)|\.(?:deps|runtimeconfig)\.json)",rel)
                         or rel in ("SkiaSharp.dll","SkiaSharp.pdb","SkiaSharp.xml","libSkiaSharp.dll","libSkiaSharp.pdb")
                         or re.fullmatch(r"runtimes/win-x64/native/libSkiaSharp\.(dll|pdb)",rel)
                         or rel.startswith("licenses/SkiaSharp-4.153.1/"))
                if not allowed:raise RuntimeError("Unexpected role output; candidate not packaged: "+rel)
                if path.suffix==".pdb":continue # Keep symbols local; do not distribute machine document paths.
                entries.append(("roles/"+role+"/"+rel,path,row["bytes"],row["sha256"]))
                if rel.startswith("KuaiYi.") and path.suffix==".dll":managed.append(path)
        run("metadata-audit",[str(audit),*map(str,managed)])
        metadata=json.loads((out/"metadata-audit.log").read_text());versions=set();core=set()
        expected={"KuaiYi.Prompts."+p.name:sha(p) for p in (REPO/"assets/prompts").glob("*.txt")}
        expected["KuaiYi.VisionTest.Sample.png"]=sha(REPO/"assets/fixtures/vision-test@1.png")
        for row in metadata:
            row["Path"]=Path(row["Path"]).relative_to(out).as_posix();versions.add(row["Version"])
            if not row["Information"] or not row["Information"].endswith("+"+head):raise RuntimeError("Actual assembly source revision differs")
            resources={r["Name"]:r["Sha256"] for r in row["Resources"]}
            if row["Name"]=="KuaiYi.Providers" and resources!=expected:raise RuntimeError("Embedded prompts/fixed sample differ")
            if row["Name"]=="KuaiYi.Core":
                core.add(sha(out/row["Path"]))
                if resources!={"KuaiYi.BootstrapRuntime":sha(REPO/"src/KuaiYi.Core/Installation/bootstrap-runtime-win-x64-10.0.13.json")}:raise RuntimeError("Bootstrap resource differs")
        if len(versions)!=1 or len(core)!=1:raise RuntimeError("Role assembly versions/Core copies differ")
        report["assemblyVersion"]=versions.pop();report["coreSha256"]=core.pop();report["assemblyMetadata"]=metadata
        # Strict source/lock and cache-byte provenance. Never manufacture source authority.
        for role in report["roles"]:
            folder=out/"roles"/role["role"];deps=json.loads((folder/("KuaiYi."+role["role"]+".deps.json")).read_text())
            for library in deps["libraries"]:
                identity,version=library.rsplit("/",1)
                if identity.startswith("KuaiYi.") and version!="1.0.0":raise RuntimeError("Unexpected project dependency version")
                if not identity.startswith("KuaiYi.") and (identity not in ("SkiaSharp","SkiaSharp.NativeAssets.Win32","SkiaSharp.NativeAssets.macOS") or version!="4.153.1"):raise RuntimeError("Unexpected role dependency")
            for file in role["files"]:
                if file["path"].startswith("licenses/") and sha(folder/file["path"])!=sha(REPO/"licenses/SkiaSharp-4.153.1"/Path(file["path"]).name):raise RuntimeError("License bytes differ")
        report["skiaAudits"]=[];pub.verify_skia_inputs(cache,env,out,deadline,report["skiaAudits"])
        for rel,path,_,digest in entries:
            if Path(rel).name not in ("SkiaSharp.dll","libSkiaSharp.dll"):continue
            slug,member=("skiasharp","lib/net10.0/SkiaSharp.dll") if Path(rel).name=="SkiaSharp.dll" else ("skiasharp.nativeassets.win32","runtimes/win-x64/native/libSkiaSharp.dll")
            package=cache/slug/"4.153.1"/(slug+".4.153.1.nupkg")
            with pub.INPUTS.verified_zip(package,pub.INPUTS.SKIA[slug],"sha256") as original:
                if hashlib.sha256(original.read(member)).hexdigest()!=digest:raise RuntimeError("Skia output differs from pinned archive")
        for rel in DOCS:entries.append((rel,REPO/rel,(REPO/rel).stat().st_size,sha(REPO/rel)))
        after=pub.product_sources();(out/"sources-after.json").write_text(json.dumps(after,indent=2)+"\n")
        report["sourcesUnchanged"]=after==source and authored()==extra and subprocess.check_output(["git","rev-parse","HEAD"],cwd=REPO,text=True).strip()==head
        if not report["sourcesUnchanged"]:raise RuntimeError("Inputs changed during build")
        metadata_package={"kind":report["kind"],"sourceHead":head,"selfContained":False,"assemblyVersion":report["assemblyVersion"],"sdk":"10.0.402","runtimeRequested":"10.0.13",
                          "productionPins":"Unavailable","windowsExecution":"NotRun","authored":extra,"sourceFiles":source,"assemblyMetadata":metadata,"files":[{"path":r,"bytes":n,"sha256":h} for r,_,n,h in entries]}
        target=out/("KuaiYi-managed-review-"+head[:12]+"-framework-dependent.zip")
        # ZIP's conservative raw-data + deflate/header allowance, before writing.
        raw_total=sum(n for _,_,n,_ in entries)+len(json.dumps(metadata_package,indent=2).encode())
        zip_reservation=raw_total+raw_total//100+len(entries)*1024+1024*1024
        if monitor(out)+zip_reservation+4*1024*1024>BUDGET or shutil.disk_usage(REPO).free-zip_reservation-4*1024*1024<FLOOR:
            raise RuntimeError("ZIP plus final receipt reservation does not fit; originals retained")
        monitor(out);pack.write_zip(target,entries,{"review-manifest.json":json.dumps(metadata_package,indent=2).encode()});monitor(out)
        if pub.product_sources()!=source or authored()!=extra:raise RuntimeError("Inputs changed during package readback")
        report.update(status="Pass_ManagedReviewOnly_NotWindowsQualification",zip=target.name,zipBytes=target.stat().st_size,zipSha256=sha(target),files=len(entries)+1)
        (out/"result.json").write_text(json.dumps(report,indent=2)+"\n")
        monitor(out)
    except BaseException as error:
        signal.setitimer(signal.ITIMER_REAL,0)
        report.update(status="FailedOrUnconfirmed",errorType=type(error).__name__,error=str(error))
        try:(out/"result.json").write_text(json.dumps(report,indent=2)+"\n")
        except BaseException as receipt_error:raise BaseExceptionGroup("Build failure and receipt failure",[error,receipt_error])
        raise
    finally:
        signal.setitimer(signal.ITIMER_REAL,0);signal.signal(signal.SIGALRM,original_handler)
        pub.output_size=original_scan
        pub.execute=original_execute
    return report

if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument("output",type=Path);args=parser.parse_args()
    result=build(args.output);print(json.dumps({k:result[k] for k in ("status","sourceHead","zip","zipBytes","zipSha256","files")}))
