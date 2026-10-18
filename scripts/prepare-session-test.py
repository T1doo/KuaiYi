"""Offline preparation of the explicit isolated App build. Never installs or executes the App."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import zipfile


def prepare():
    repo = Path(__file__).resolve().parent.parent
    contract = json.loads((repo / "packaging/session-test.json").read_text())
    # No arbitrary output option. The freshly built .NET helper applies the same link/production-root
    # checks using the actual KnownFolder before any preparation directory or log is written.
    env = dict(os.environ, DOTNET_GENERATE_ASPNET_CERTIFICATE="false")
    fixture = "tools/KuaiYi.SettingsStoreFixture/KuaiYi.SettingsStoreFixture.csproj"
    driver = "tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll"
    bootstrap = []
    try:
        for args in [["dotnet", "--version"], ["dotnet", "build", fixture, "--no-restore", "-c", "Release"]]:
            result = subprocess.run(args, cwd=repo, env=env, capture_output=True)
            bootstrap.append((args, result))
            if result.returncode or len(bootstrap) == 1 and result.stdout.decode().strip() != contract["sdk"]:
                raise RuntimeError("Existing pinned SDK / fixture build unavailable; no SDK download attempted")
        args = ["dotnet", driver, "--prepare-isolated-session-build-workspace", str(repo / "artifacts/session-test-preparation")]
        result = subprocess.run(args, cwd=repo, env=env, capture_output=True)
        bootstrap.append((args, result))
        if result.returncode:
            raise RuntimeError("Build workspace guard refused; no preparation output created")
        work = Path(json.loads(result.stdout)["Workspace"])
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        for _, result in bootstrap:
            sys.stderr.buffer.write(result.stdout + result.stderr)
        print(json.dumps({"Status": "Blocked", "Reason": str(error), "Bundle": None,
                          "BootstrapExitCodes": [result.returncode for _, result in bootstrap]}))
        return 2
    report = {"Kind": contract["kind"], "WindowsExecution": "NotRun", "ModelCalls": "None",
              "ProductionInstaller": False, "Bundle": None, "Commands": []}
    for index, (args, result) in enumerate(bootstrap, 1):
        log = work / f"bootstrap-{index:02}.log"
        log.write_bytes(result.stdout + result.stderr)
        report["Commands"].append({"Argv": args, "Exit": result.returncode, "Log": log.name,
                                   "Sha256": hashlib.sha256(log.read_bytes()).hexdigest()})

    def command(args):
        index = len(report["Commands"]) + 1
        log = work / f"command-{index:02}.log"
        with log.open("wb") as stream:
            result = subprocess.run(args, cwd=repo, env=env, stdout=stream, stderr=subprocess.STDOUT)
        report["Commands"].append({"Argv": args, "Exit": result.returncode, "Log": log.name,
                                   "Sha256": hashlib.sha256(log.read_bytes()).hexdigest()})
        if result.returncode:
            raise RuntimeError(f"Command {index} failed; original log retained")
        return log.read_text().strip()

    try:
        report["SourceCommit"] = command(["git", "rev-parse", "HEAD"])
        report["SourceDirty"] = bool(command(["git", "status", "--porcelain=v1"]))
        assets_path = repo / "src/KuaiYi.App/obj/project.assets.json"
        assets = json.loads(assets_path.read_text())
        packages = list(assets["packageFolders"])
        runtime = contract["runtime"]
        required = {
            "microsoft.netcore.app.runtime.win-x64": "runtimes/win-x64/native/coreclr.dll",
            "microsoft.windowsdesktop.app.runtime.win-x64": "runtimes/win-x64/lib/net10.0/PresentationFramework.dll",
            "microsoft.netcore.app.host.win-x64": "runtimes/win-x64/native/apphost.exe",
            "microsoft.windowsdesktop.app.ref": "ref/net10.0/PresentationFramework.dll",
        }
        missing = [name + "/" + runtime for name, member in required.items()
                   if not any((Path(folder) / name / runtime / member).is_file() for folder in packages)]
        report["MissingCachedPacks"] = missing
        if missing:
            raise RuntimeError("Pinned runtime/apphost packs missing from existing cache; no download attempted")
        # Both package and audit sources are local. Audit remains enabled; an unavailable offline
        # check is allowed to fail preparation, never bypassed by disabling security checks.
        feed = work / "empty-offline-feed"
        feed.mkdir()
        import xml.etree.ElementTree as xml
        config = xml.Element("configuration")
        for section in ("packageSources", "auditSources"):
            entries = xml.SubElement(config, section)
            xml.SubElement(entries, "clear")
            xml.SubElement(entries, "add", key="session-offline", value=str(feed))
        mapping = xml.SubElement(config, "packageSourceMapping")
        xml.SubElement(mapping, "clear")
        xml.SubElement(xml.SubElement(mapping, "packageSource", key="session-offline"), "package", pattern="*")
        offline_config = work / "NuGet.offline.config"
        xml.ElementTree(config).write(offline_config, encoding="utf-8", xml_declaration=True)
        props = ["-p:KuaiYiTextAlpha=true", "-p:KuaiYiIsolatedSessionTest=true", "-p:SelfContained=true",
                 "-p:PublishSingleFile=false", "-p:PublishTrimmed=false", "-p:PublishReadyToRun=false",
                 "-p:EnableWindowsTargeting=true", "-p:NuGetAudit=true", "-p:RuntimeFrameworkVersion=" + runtime]
        command(["dotnet", "restore", contract["project"], "--locked-mode", "-r", contract["rid"],
                 "--configfile", str(offline_config), "--source", str(feed), *props])
        publish = work / "publish"
        command(["dotnet", "publish", contract["project"], "--no-restore", "-c", "Release", "-r",
                 contract["rid"], "--self-contained", "true", "-o", str(publish), *props])
        result = json.loads(command(["dotnet", driver,
                                     "--prepare-isolated-session-package", str(publish), str(work / "prepared")]))
        workspace = Path(result["Workspace"])
        manifest = []
        for path in sorted(workspace.rglob("*")):
            if path.is_symlink():
                raise RuntimeError("Prepared workspace contains a link; retained")
            if path.is_file():
                manifest.append({"Path": path.relative_to(workspace).as_posix(), "Bytes": path.stat().st_size,
                                 "Sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        report["Files"] = manifest
        archive = work / (workspace.name + ".zip")
        with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as bundle:
            for path in sorted(workspace.rglob("*")):
                bundle.write(path, path.relative_to(workspace.parent).as_posix())
        report.update(Status="PreparedNotRun", Bundle=archive.name, Sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
                      CatalogSha256=result["CatalogSha256"])
        code = 0
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        report.update(Status="Blocked", Reason=str(error))
        code = 2
    (work / "preparation-result.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"Report": str(work / "preparation-result.json"), "Status": report["Status"],
                      "Bundle": report["Bundle"]}))
    return code


if __name__ == "__main__":
    if len(sys.argv) != 1:
        print("Preparation accepts no output override or other arguments.", file=sys.stderr)
        sys.exit(2)
    sys.exit(prepare())
