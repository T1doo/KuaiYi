"""Run the approved isolated Linux SDK with its documented per-user state directories.

Usage: python3 scripts/session-sdk.py <dotnet arguments>
       python3 scripts/session-sdk.py --prepare
No download, system installation, HOME override, migration bypass or audit override.
"""
import importlib.util
import json
import os
from pathlib import Path
import stat
import subprocess
import sys


def private_directory(path):
    for ancestor in (path, *path.parents):
        if ancestor.is_symlink():
            raise RuntimeError("SDK state directory has a symbolic-link ancestor; retained")
    path.mkdir(mode=0o700, parents=False, exist_ok=True)
    entry = path.stat()
    if not stat.S_ISDIR(entry.st_mode) or entry.st_uid != os.geteuid() or stat.S_IMODE(entry.st_mode) != 0o700:
        raise RuntimeError("SDK state directory must already be owned by this user with mode0700; retained")


def environment(repo, inherited=None):
    if sys.platform != "linux":
        raise RuntimeError("This launcher is for the isolated Linux build workspace")
    contract = json.loads((repo / "packaging/session-test.json").read_text())
    sdk = repo / "artifacts/session-runtime-security/sdk" / ("sdk-" + contract["sdk"])
    spec = importlib.util.spec_from_file_location("session_preparation", repo / "scripts/prepare-session-test.py")
    preparation = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(preparation)
    preparation.require_safe_sdk(contract, sdk / "dotnet")
    # .NET10.0.13 uses XDG_RUNTIME_DIR for secure user-scoped named mutex storage.
    # NuGet uses XDG_DATA_HOME for its real migration records. Keep both in this
    # approved workspace, with the default secure mutex scope unchanged.
    state = repo / "artifacts/session-sdk-state"
    private_directory(state)
    for name in ("runtime", "data", "cli"):
        private_directory(state / name)
    env = dict(os.environ if inherited is None else inherited)
    env.update(DOTNET_ROOT=str(sdk), DOTNET_HOST_PATH=str(sdk / "dotnet"),
               PATH=str(sdk) + os.pathsep + env.get("PATH", ""),
               DOTNET_CLI_HOME=str(state / "cli"), XDG_RUNTIME_DIR=str(state / "runtime"),
               XDG_DATA_HOME=str(state / "data"), DOTNET_GENERATE_ASPNET_CERTIFICATE="false",
               DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", PYTHONDONTWRITEBYTECODE="1")
    return sdk, env


def main():
    if len(sys.argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    repo = Path(__file__).resolve().parent.parent
    try:
        sdk, env = environment(repo)
        if sys.argv[1] == "--prepare":
            if len(sys.argv) != 2:
                raise RuntimeError("Preparation accepts no overrides")
            args = [sys.executable, "-B", str(repo / "scripts/prepare-session-test.py")]
        else:
            args = [str(sdk / "dotnet"), *sys.argv[1:]]
        return subprocess.run(args, cwd=repo, env=env).returncode
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        print("Isolated SDK refused: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
