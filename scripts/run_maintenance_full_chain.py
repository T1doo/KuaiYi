#!/usr/bin/env python3
"""One exact newly authorized synthetic G/B fixture; never the frozen Core/Windows runner.
352MiB logical/allocated/global-growth observation caps, 1GiB free floor, 180s original process-group budget.
Retains all output on success/failure. No namespace/privilege changes, retry, cleanup or production trust override.
"""
from pathlib import Path
import argparse, hashlib, importlib.util, json, os, re, shutil, stat, subprocess, sys, time
sys.dont_write_bytecode = True
REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / "artifacts/2026-11-01-maintenance-full-chain"
LIMIT = 352 * 1024**2
FLOOR = 1024**3
CASE = "KuaiYi.Provider.Tests.InstallationMaintenanceFullChainTests.FixedSourcesRepairKnownFailureRollbackRetireAndReopenActualFiles"
CORE = REPO / "src/KuaiYi.Core/bin/Release/net10.0/KuaiYi.Core.dll"
TEST = REPO / "tests/KuaiYi.Provider.Tests/bin/MaintenanceFullChain/net10.0/KuaiYi.Provider.Tests.dll"

def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65536), b""): h.update(block)
    return h.hexdigest()

def inputs():
    # Only fixed project build outputs and the exact test suppliers, no credentials/environment dump.
    paths = [CORE, TEST, REPO/"tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj", Path(__file__).resolve(), REPO/"scripts/publish-windows-managed.py", REPO/"scripts/session-sdk.py", REPO/"tests/fixtures/payload-runtime/KuaiYi.Launcher.deps.json", REPO/"tests/KuaiYi.Provider.Tests/InstallationMaintenanceFullChainTests.cs",
             REPO/"tests/KuaiYi.Core.Tests/MaterializedPayloadSource.cs", REPO/"tests/KuaiYi.Core.Tests/BootstrapSource.cs",
             REPO/"packaging/installer-source-pins.json", REPO/"src/KuaiYi.Core/Installation/bootstrap-runtime-win-x64-10.0.13.json"]
    actual_core=TEST.parent/"KuaiYi.Core.dll"
    if digest(CORE)!=digest(actual_core):raise RuntimeError("Actual testhost Core differs from the sealed project Core")
    test_files=list(TEST.parent.rglob("*"))
    if any(p.is_symlink() for p in test_files):raise RuntimeError("Linked testhost input refused")
    paths += [p for p in test_files if p.is_file()]
    paths.append(REPO/"src/KuaiYi.Launcher/bin/Release/net10.0/win-x64/KuaiYi.Core.dll")
    if digest(CORE)!=digest(paths[-1]):raise RuntimeError("Actual bootstrap supplier Core differs from the sealed Core")
    projects = ["KuaiYi.App", "KuaiYi.NativeHost", "KuaiYi.CaptureBroker", "KuaiYi.ClipboardHost", "KuaiYi.CaptureHost", "KuaiYi.OleReceiver", "KuaiYi.ImageWorker", "KuaiYi.Launcher"]
    windows = {"KuaiYi.App", "KuaiYi.CaptureBroker", "KuaiYi.ClipboardHost", "KuaiYi.CaptureHost", "KuaiYi.OleReceiver"}
    for project in projects:
        base = REPO/"src"/project/"bin/Release"/("net10.0-windows" if project in windows else "net10.0")/"win-x64"
        paths += [base/(project+suffix) for suffix in (".exe", ".dll", ".deps.json", ".runtimeconfig.json")]
    for project in ("KuaiYi.Core", "KuaiYi.Contracts", "KuaiYi.Imaging", "KuaiYi.Providers", "KuaiYi.OleTransfer", "KuaiYi.Windows"):
        paths.append(REPO/"src"/project/"bin/Release"/("net10.0-windows" if project=="KuaiYi.Windows" else "net10.0")/(project+".dll"))
    resource_declaration=(REPO/"src/KuaiYi.Core/Installation/InstallationPayload.cs").read_text().split("ResourceFiles{get;}=",1)[1].split("});",1)[0]
    for name in re.findall(r'"([^"\n]+)"',resource_declaration):
        if name.startswith("identity/"):paths.append(REPO/"packaging"/name)
        elif name.startswith("browser-extension/"):paths.append(REPO/"extensions/chromium"/name[18:])
        elif name.startswith("licenses/SkiaSharp"):paths.append(REPO/name)
    return [{"path":str(p.relative_to(REPO)),"bytes":p.stat().st_size,"sha256":digest(p)} for p in sorted(set(paths))]

def execution_identity(binding):
    # Directory-independent consumed identity, including every actual loaded DLL.
    loaded=[]
    for row in binding["inputs"]:
        if row["path"].startswith("tests/KuaiYi.Provider.Tests/bin/") and row["path"].endswith(".dll"):
            parts=Path(row["path"]).parts
            relative=Path(*parts[parts.index("net10.0")+1:]).as_posix()
            loaded.append({"path":relative,"bytes":row["bytes"],"sha256":row["sha256"]})
    if not loaded:raise RuntimeError("No sealed loaded DLL closure")
    return hashlib.sha256((json.dumps(sorted(loaded,key=lambda x:x["path"]),sort_keys=True)+binding["case"]).encode()).hexdigest()

def reject_consumed(binding):
    wanted=execution_identity(binding)
    for marker in ROOT.glob("execution-*.json"):
        receipt=json.loads(marker.read_text())
        candidate=receipt["candidateSha256"]
        retained=ROOT/("candidate-"+candidate[:24])/"binding.json"
        old=json.loads(retained.read_text())
        if hashlib.sha256(retained.read_bytes()).hexdigest()!=candidate:raise RuntimeError("Consumed binding damaged; execution refused")
        if execution_identity(old)==wanted:raise RuntimeError("Exact loaded closure/case already consumed; no replay")
    return wanted

def main():
    parser=argparse.ArgumentParser();parser.add_argument("mode",choices=("prepare","run"));args=parser.parse_args()
    os.chdir(REPO);ROOT.mkdir(exist_ok=True)
    binding={"scope":"NewSyntheticGBFileContractOnly_NotWindows_NotFrozen23Core", "head":subprocess.check_output(["git","rev-parse","HEAD"],text=True).strip(),
             "case":CASE,"inputs":inputs(),"limits":{"logicalBytes":LIMIT,"allocatedBytes":LIMIT,"globalGrowthBytes":LIMIT,"freeFloorBytes":FLOOR,"seconds":180,"logBytes":1024**2}}
    encoded=json.dumps(binding,sort_keys=True,indent=2).encode();candidate=hashlib.sha256(encoded).hexdigest();folder=ROOT/("candidate-"+candidate[:24]);folder.mkdir(exist_ok=True);path=folder/"binding.json"
    if args.mode=="prepare":
        if path.exists() and path.read_bytes()!=encoded:raise RuntimeError("Different binding retained; no overwrite")
        if not path.exists():path.write_bytes(encoded)
        print(json.dumps({"prepared":str(path.relative_to(REPO)),"candidateSha256":candidate,"executed":False}));return
    if not path.exists() or path.read_bytes()!=encoded:raise RuntimeError("Exact independently reviewed preparation missing; no execution")
    execution_key=reject_consumed(binding)
    initial=shutil.disk_usage(REPO).free
    if initial<FLOOR+LIMIT:raise RuntimeError("New sample cap plus existing1GiB floor unavailable; no execution")
    marker=ROOT/("execution-"+execution_key+".json")
    with marker.open("x") as stream:json.dump({"candidateSha256":candidate,"startUnix":time.time(),"pid":os.getpid(),"automaticRetry":False},stream)
    spec=importlib.util.spec_from_file_location("maintenance_original_runner",REPO/"scripts/publish-windows-managed.py");module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    sdk,env=module.SDK.environment(REPO)
    for name in ("tmp","http","scratch"):(ROOT/name).mkdir(exist_ok=True)
    env.update(TMPDIR=str(ROOT/"tmp"),TMP=str(ROOT/"tmp"),TEMP=str(ROOT/"tmp"),NUGET_HTTP_CACHE_PATH=str(ROOT/"http"),NUGET_SCRATCH=str(ROOT/"scratch"),MSBUILDDISABLENODEREUSE="1")
    peaks={"logicalBytes":0,"allocatedBytes":0,"globalGrowthBytes":0,"minimumFreeBytes":initial}
    def observe(_):
        logical=allocated=0
        for p in ROOT.rglob("*"):
            info=p.lstat()
            if stat.S_ISLNK(info.st_mode):raise RuntimeError("Linked sample output refused; retained")
            if stat.S_ISREG(info.st_mode):logical+=info.st_size
            allocated+=info.st_blocks*512
        free=shutil.disk_usage(REPO).free;growth=max(0,initial-free)
        peaks["logicalBytes"]=max(peaks["logicalBytes"],logical);peaks["allocatedBytes"]=max(peaks["allocatedBytes"],allocated);peaks["globalGrowthBytes"]=max(peaks["globalGrowthBytes"],growth);peaks["minimumFreeBytes"]=min(peaks["minimumFreeBytes"],free)
        if max(logical,allocated,growth)>LIMIT or free<FLOOR:raise RuntimeError("New synthetic sample observation cap exceeded; original group stopped, outputs retained")
        return logical
    module.output_size=observe;row={"candidateSha256":candidate,"binding":"binding.json","initialFreeBytes":initial}
    command=[str(sdk/"dotnet"),"test","tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj","--no-build","--no-restore","-c","Release","-p:KuaiYiMaintenanceFullChain=true","--filter","FullyQualifiedName="+CASE,"--logger","trx;LogFileName=full-chain.trx","--results-directory",str(folder/"results")]
    try:module.execute(command,env,folder/"full-chain.log",time.monotonic()+180,ROOT,row,log_limit_bytes=1024**2)
    finally:
        row["observationPeaks"]=peaks;row["finalFreeBytes"]=shutil.disk_usage(REPO).free;row["outputsRetained"]=True
        (folder/"receipt.json").write_text(json.dumps(row,indent=2)+"\n")
    print(json.dumps(row));raise SystemExit(row.get("exit",2))
if __name__=="__main__":main()
