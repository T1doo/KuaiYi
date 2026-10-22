"""Audited NuGet preparation of the isolated App build. Never installs or executes the App."""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import shutil
import sys
import urllib.request
import xml.etree.ElementTree as xml
import zipfile


NUGET_SOURCE = "https://api.nuget.org/v3/index.json"
RUNTIME_IDS = ("Microsoft.NETCore.App.Runtime.win-x64", "Microsoft.WindowsDesktop.App.Runtime.win-x64")


def require_safe_sdk(contract, executable=None):
    executable = executable or shutil.which("dotnet")
    if not executable:
        raise RuntimeError("Existing safe SDK is unavailable; no SDK download attempted")
    root = Path(executable).resolve().parent
    sdk = root / "sdk" / contract["sdk"]
    config = json.loads((sdk / "dotnet.runtimeconfig.json").read_text())
    if config["runtimeOptions"]["framework"]["version"] != contract["runtime"]:
        raise RuntimeError("Build host runtime is not the pinned security patch; no dotnet execution attempted")
    libraries = json.loads((sdk / "dotnet.deps.json").read_text())["libraries"]
    xml_versions = [key.split("/", 1)[1] for key in libraries if key.startswith("System.Security.Cryptography.Xml/")]
    if xml_versions != [contract["runtime"]] or not (root / "shared/Microsoft.NETCore.App" / contract["runtime"]).is_dir():
        raise RuntimeError("SDK private XML/runtime security patch mismatch; no dotnet execution attempted")


def require_signature_environment(env):
    if env.get("NUGET_CERT_REVOCATION_MODE", "online").lower() != "online" or env.get("DOTNET_NUGET_SIGNATURE_VERIFICATION", "true").lower() != "true":
        raise RuntimeError("Runtime preparation requires online revocation and signature verification")


def require_verified_signature(output, identity, runtime, content_hash):
    # nuget verify can return zero with revocation/chain warnings. Such a result
    # does not establish the signature gate, even when the archive hash matches.
    if re.search(r"\b(?:warn|warning|error)\s*:", output, re.IGNORECASE) or "RevocationStatusUnknown" in output:
        raise RuntimeError("Runtime signature verification reported warnings/errors; preparation refused")
    required = ("Verifying " + identity + "." + runtime, "Content hash: " + content_hash,
                "Signature type: Author", "Signature type: Repository")
    if any(line not in output.splitlines() for line in required):
        raise RuntimeError("Runtime signature verification lacks expected package/hash/signatures")


def runtime_license_metadata(runtime):
    records = []
    for identity in RUNTIME_IDS:
        name = identity.lower()
        uri = f"https://api.nuget.org/v3-flatcontainer/{name}/{runtime}/{name}.nuspec"
        with urllib.request.urlopen(uri, timeout=30) as response:
            raw = response.read(1024 * 1024 + 1)
        if len(raw) > 1024 * 1024:
            raise RuntimeError("Runtime license metadata too large")
        metadata = next(child for child in xml.fromstring(raw) if child.tag.split("}")[-1] == "metadata")
        values = {child.tag.split("}")[-1]: child.text for child in metadata}
        license_node = next((node for node in metadata if node.tag.split("}")[-1] == "license"), None)
        if values.get("id") != identity or values.get("version") != runtime or values.get("license") != "MIT" or license_node is None or license_node.attrib.get("type") != "expression" or values.get("requireLicenseAcceptance", "false").lower() != "false":
            raise RuntimeError("Runtime package has unreviewed license/identity requirements")
        records.append({"Id": identity, "Version": runtime, "License": "MIT", "ExplicitAcceptanceDeclared": False,
                        "Source": uri, "Sha256": hashlib.sha256(raw).hexdigest()})
    return records


def reject_audit_problems(node):
    for key in ("problems", "errors", "warnings"):
        if key in node and (not isinstance(node[key], list) or node[key]):
            raise RuntimeError("Package audit reported a problem; preparation refused")


def audit_project(document, expected_project):
    if not isinstance(document, dict) or type(document.get("version")) is not int or document["version"] != 1:
        raise RuntimeError("Unsupported package audit result")
    projects = document.get("projects")
    if not isinstance(projects, list) or len(projects) != 1 or not isinstance(projects[0], dict):
        raise RuntimeError("Audit must cover exactly the expected project")
    project = projects[0]
    if not isinstance(project.get("path"), str) or Path(project["path"]).resolve() != Path(expected_project).resolve():
        raise RuntimeError("Audit project scope mismatch")
    for node in (document, project):
        reject_audit_problems(node)
    return project


def audit_packages(project, allow_empty=False):
    # A clean --vulnerable result can omit frameworks/packages. A separate ordinary
    # list proves the actual restored inventory instead of treating omission as coverage.
    frameworks = project.get("frameworks", [])
    if not isinstance(frameworks, list) or not frameworks and not allow_empty:
        raise RuntimeError("Package inventory lacks framework coverage")
    packages = {}
    seen = set()
    for framework in frameworks:
        if not isinstance(framework, dict) or not isinstance(framework.get("framework"), str) or not framework["framework"] or framework["framework"] in seen:
            raise RuntimeError("Invalid package inventory framework")
        seen.add(framework["framework"])
        reject_audit_problems(framework)
        for group in ("topLevelPackages", "transitivePackages"):
            entries = framework.get(group, [])
            if not isinstance(entries, list):
                raise RuntimeError("Invalid package inventory group")
            for package in entries:
                if not isinstance(package, dict) or not isinstance(package.get("id"), str) or not package["id"] or not isinstance(package.get("resolvedVersion"), str) or not package["resolvedVersion"]:
                    raise RuntimeError("Package inventory lacks resolved identity")
                identity = (framework["framework"], package["id"].lower())
                if identity in packages:
                    raise RuntimeError("Duplicate package inventory identity")
                if "vulnerabilities" in package and (not isinstance(package["vulnerabilities"], list) or package["vulnerabilities"]):
                    raise RuntimeError("NuGet reported a package vulnerability; preparation refused")
                packages[identity] = package["resolvedVersion"]
    if not packages and not allow_empty:
        raise RuntimeError("Package inventory is empty")
    return packages, seen


def assets_prove_zero_packages(assets, expected_project):
    if not isinstance(assets, dict) or assets.get("version") not in (3, 4) or not isinstance(assets.get("libraries"), dict):
        return False
    project = assets.get("project")
    if not isinstance(project, dict) or not isinstance(project.get("frameworks"), dict) or not isinstance(project.get("restore"), dict):
        return False
    restore = project["restore"]
    if not isinstance(restore.get("projectUniqueName"), str) or Path(restore["projectUniqueName"]).resolve() != Path(expected_project).resolve():
        return False
    return bool(project["frameworks"]) and all(isinstance(item, dict) and item.get("type") == "project" for item in assets["libraries"].values())


def require_no_advisories(document, expected_project, inventory, runtime=None, assets=None):
    project = audit_project(document, expected_project)
    if document.get("sources") != [NUGET_SOURCE]:
        raise RuntimeError("Vulnerability audit lacks the actual official source")
    zero_packages = runtime is None and assets_prove_zero_packages(assets, expected_project)
    actual, frameworks = audit_packages(audit_project(inventory, expected_project), allow_empty=zero_packages)
    if zero_packages and actual:
        raise RuntimeError("Package inventory differs from zero-package restore assets")
    if not frameworks and zero_packages:
        frameworks = set(assets["project"]["frameworks"])
    reported, checked = audit_packages(project, allow_empty=True)
    if not checked.issubset(frameworks) or any(actual.get(key) != version for key, version in reported.items()):
        raise RuntimeError("Vulnerability report differs from restored package inventory")
    if runtime is not None and actual != {("net10.0", identity.lower()): runtime for identity in RUNTIME_IDS}:
        raise RuntimeError("Runtime audit inventory does not contain exactly the pinned runtime packs")
    return {"Project": str(Path(expected_project).resolve()), "PackageCount": len(actual), "Frameworks": sorted(frameworks)}


def runtime_payload_records(packages):
    """Read the exact WPF/runtime member map; caller must verify archive identity/signature separately."""
    outputs = {}
    for identity, version, package in packages:
        with zipfile.ZipFile(package) as archive:
            root = xml.fromstring(archive.read("data/RuntimeList.xml"))
            if not len(root) or len(root) > 4096:
                raise RuntimeError("Runtime pack has invalid payload inventory")
            for entry in root:
                if entry.get("Profile") and "WPF" not in entry.get("Profile").split(";"):
                    continue
                member = entry.get("Path", "")
                parts = member.split("/")
                if not member.startswith("runtimes/win-x64/") or any(not p or p in (".", "..") or ":" in p or "\\" in p for p in parts):
                    raise RuntimeError("Runtime pack payload path invalid")
                relative = parts[-1]
                if entry.get("Type") == "Resources":
                    culture = entry.get("Culture", "")
                    if not 1 <= len(culture) <= 32 or not all(c.isascii() and (c.isalnum() or c == "-") for c in culture):
                        raise RuntimeError("Runtime pack culture invalid")
                    relative = culture + "/" + relative
                elif entry.get("Type") not in ("Managed", "Native"):
                    raise RuntimeError("Runtime pack payload type invalid")
                expected = hashlib.sha256(archive.read(member)).hexdigest()
                record = {"Id": identity, "Version": version, "Member": member, "Path": relative, "Sha256": expected}
                previous = outputs.get(relative.lower())
                if previous and previous["Sha256"] != expected:
                    # The verified Core pack declares a WindowsBase v4 stub, while WPF
                    # declares the v10 implementation. Only this exact framework pair
                    # has an override; all other differing-byte collisions are refused.
                    pair = {previous["Id"], identity}
                    if relative != "WindowsBase.dll" or pair != set(RUNTIME_IDS) or previous["Version"] != version:
                        raise RuntimeError("Runtime packs conflict on a published file")
                    if identity != RUNTIME_IDS[1]:
                        previous["Overrides"] = {"Id": identity, "Sha256": expected}
                        continue
                    record["Overrides"] = {"Id": previous["Id"], "Sha256": previous["Sha256"]}
                outputs[relative.lower()] = record
    if not outputs:
        raise RuntimeError("Runtime payload inventory is empty")
    return sorted(outputs.values(), key=lambda entry: entry["Path"])


def bind_runtime_payload(publish, packages):
    """Bind every selected WPF/runtime payload to the verified nupkg, before ZIP creation."""
    records = runtime_payload_records(packages)
    for record in records:
        target = publish / record["Path"]
        if any(p.is_symlink() for p in (target, *target.parents)) or not target.is_file() or hashlib.sha256(target.read_bytes()).hexdigest() != record["Sha256"]:
            raise RuntimeError("Published runtime payload differs from verified pack: " + record["Path"])
    return records


def prepare():
    repo = Path(__file__).resolve().parent.parent
    contract = json.loads((repo / "packaging/session-test.json").read_text())
    # No arbitrary output option. The freshly built .NET helper applies the same link/production-root
    # checks using the actual KnownFolder before any preparation directory or log is written.
    env = dict(os.environ, DOTNET_GENERATE_ASPNET_CERTIFICATE="false", DOTNET_CLI_UI_LANGUAGE="en-US",
               EnableWindowsTargeting="true")
    fixture = "tools/KuaiYi.SettingsStoreFixture/KuaiYi.SettingsStoreFixture.csproj"
    driver = "tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll"
    bootstrap = []
    try:
        # Read metadata before invoking dotnet: the previous SDK's host/private XML are vulnerable.
        require_safe_sdk(contract)
        require_signature_environment(env)
        licenses = runtime_license_metadata(contract["runtime"])
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
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RuntimeError, xml.ParseError, StopIteration) as error:
        for _, result in bootstrap:
            sys.stderr.buffer.write(result.stdout + result.stderr)
        print(json.dumps({"Status": "Blocked", "Reason": str(error), "Bundle": None,
                          "BootstrapExitCodes": [result.returncode for _, result in bootstrap]}))
        return 2
    report = {"Kind": contract["kind"], "WindowsExecution": "NotRun", "ModelCalls": "None",
              "ProductionInstaller": False, "Bundle": None, "Commands": [], "RuntimeLicenses": licenses}
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
        packages = list(json.loads(assets_path.read_text())["packageFolders"]) if assets_path.is_file() else []
        runtime = contract["runtime"]
        required = {
            "microsoft.netcore.app.runtime.win-x64": "runtimes/win-x64/native/coreclr.dll",
            "microsoft.windowsdesktop.app.runtime.win-x64": "runtimes/win-x64/lib/net10.0/PresentationFramework.dll",
            "microsoft.netcore.app.host.win-x64": "runtimes/win-x64/native/apphost.exe",
            "microsoft.windowsdesktop.app.ref": "ref/net10.0/PresentationFramework.dll",
        }
        missing = [name + "/" + runtime for name, member in required.items()
                   if not any((Path(folder) / name / runtime / member).is_file() for folder in packages)]
        report["MissingCachedPacksBeforeRestore"] = missing
        # Genuine official package and vulnerability resources. No empty audit source or warning bypass.
        config = xml.Element("configuration")
        for section in ("packageSources", "auditSources"):
            entries = xml.SubElement(config, section)
            xml.SubElement(entries, "clear")
            xml.SubElement(entries, "add", key="nuget.org", value=NUGET_SOURCE)
        mapping = xml.SubElement(config, "packageSourceMapping")
        xml.SubElement(mapping, "clear")
        xml.SubElement(xml.SubElement(mapping, "packageSource", key="nuget.org"), "package", pattern="*")
        source_config = work / "NuGet.audited.config"
        xml.ElementTree(config).write(source_config, encoding="utf-8", xml_declaration=True)
        audit_project = "packaging/session-runtime-audit/session-runtime-audit.csproj"
        audit_props = ["-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low", "-p:TreatWarningsAsErrors=true"]
        # Framework download packs need an explicit restore-only inventory to participate in NuGetAudit.
        command(["dotnet", "restore", audit_project, "--locked-mode", "--configfile", str(source_config), *audit_props])
        packages = list(json.loads((repo / "packaging/session-runtime-audit/obj/project.assets.json").read_text())["packageFolders"])
        inventory = json.loads(command(["dotnet", "list", audit_project, "package", "--include-transitive", "--format", "json", "--no-restore"]))
        audit = json.loads(command(["dotnet", "list", audit_project, "package", "--vulnerable", "--include-transitive",
                                    "--format", "json", "--no-restore", "--source", NUGET_SOURCE]))
        report["RuntimeAuditCoverage"] = require_no_advisories(audit, repo / audit_project, inventory, runtime)
        report["RuntimeVulnerabilityAudit"], report["RuntimePackageInventory"] = audit, inventory
        locked = json.loads((repo / "packaging/session-runtime-audit/packages.lock.json").read_text())["dependencies"]["net10.0"]
        reviewed = json.loads((repo / "packaging/session-runtime-audit/runtime-pack-integrity.json").read_text())["Packages"]
        if set(reviewed) != set(RUNTIME_IDS) or any(
                reviewed[identity]["Version"] != runtime or reviewed[identity]["Source"] != NUGET_SOURCE or
                reviewed[identity]["NuGetContentHash"] != locked[identity]["contentHash"] for identity in RUNTIME_IDS):
            raise RuntimeError("Reviewed runtime archive identity/content lock mismatch")
        integrity, verified_packages = [], []
        for identity in RUNTIME_IDS:
            name = identity.lower()
            package = next((Path(folder) / name / runtime / (name + "." + runtime + ".nupkg") for folder in packages
                            if (Path(folder) / name / runtime / (name + "." + runtime + ".nupkg")).is_file()), None)
            if package is None or base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode() != reviewed[identity]["ArchiveSha512"]:
                raise RuntimeError("Runtime package SHA-512 differs from reviewed official catalog/lock")
            verified = command(["dotnet", "nuget", "verify", str(package), "--all"])
            require_verified_signature(verified, identity, runtime, locked[identity]["contentHash"])
            with zipfile.ZipFile(package) as archive:
                notices = {entry: hashlib.sha256(archive.read(entry)).hexdigest() for entry in archive.namelist()
                           if "license" in entry.lower() or "thirdpartynotice" in entry.lower()}
            integrity.append({"Id": identity, "Version": runtime, "ArchiveSha512": reviewed[identity]["ArchiveSha512"],
                              "NuGetContentHash": locked[identity]["contentHash"],
                              "SignatureVerification": "Pass", "NoticeFilesSha256": notices})
            verified_packages.append((identity, runtime, package))
        report["RuntimeIntegrity"] = integrity
        props = ["-p:KuaiYiTextAlpha=true", "-p:KuaiYiIsolatedSessionTest=true", "-p:SelfContained=true",
                 "-p:PublishSingleFile=false", "-p:PublishTrimmed=false", "-p:PublishReadyToRun=false",
                 "-p:EnableWindowsTargeting=true", "-p:RuntimeFrameworkVersion=" + runtime, *audit_props]
        command(["dotnet", "restore", contract["project"], "--locked-mode", "-r", contract["rid"],
                 "--configfile", str(source_config), *props])
        inventory = json.loads(command(["dotnet", "list", contract["project"], "package", "--include-transitive", "--format", "json", "--no-restore"]))
        audit = json.loads(command(["dotnet", "list", contract["project"], "package", "--vulnerable", "--include-transitive",
                                    "--format", "json", "--no-restore", "--source", NUGET_SOURCE]))
        report["AppAuditCoverage"] = require_no_advisories(audit, repo / contract["project"], inventory,
                                                          assets=json.loads(assets_path.read_text()))
        report["AppVulnerabilityAudit"], report["AppPackageInventory"] = audit, inventory
        publish = work / "publish"
        command(["dotnet", "publish", contract["project"], "--no-restore", "-c", "Release", "-r",
                 contract["rid"], "--self-contained", "true", "-o", str(publish), *props])
        report["RuntimePayload"] = bind_runtime_payload(publish, verified_packages)
        for identity, _, package in verified_packages:
            with zipfile.ZipFile(package) as source:
                for member in source.namelist():
                    if "license" in member.lower() or "thirdpartynotice" in member.lower():
                        # Names come from the reviewed, signed package; flatten to this notice directory.
                        notice = publish / "licenses" / identity / Path(member).name
                        notice.parent.mkdir(parents=True, exist_ok=True)
                        with notice.open("xb") as output:
                            output.write(source.read(member))
        result = json.loads(command(["dotnet", driver,
                                     "--prepare-isolated-session-package", str(publish), str(work / "prepared")]))
        workspace = Path(result["Workspace"])
        report["DeclaredDependencyFiles"] = result["DeclaredDependencyFiles"]
        report["PreparedRuntimePayload"] = bind_runtime_payload(Path(result["DirectoryPath"]), verified_packages)
        if report["PreparedRuntimePayload"] != report["RuntimePayload"]:
            raise RuntimeError("Prepared runtime payload differs from audited publish output")
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
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RuntimeError, xml.ParseError, zipfile.BadZipFile) as error:
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
