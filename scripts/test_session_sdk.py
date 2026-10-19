"""Offline tests for private SDK state; no SDK process is executed."""
import importlib.util
import json
from pathlib import Path
import shutil
import stat
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("session_sdk", Path(__file__).with_name("session-sdk.py"))
launcher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(launcher)


class SessionSdkTests(unittest.TestCase):
    def test_existing_permissive_directory_is_rejected_without_chmod(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "PUBLIC_EXISTING"
            path.mkdir(mode=0o755)
            path.chmod(0o755)  # This newly owned fixture must be permissive even under umask077.
            before = stat.S_IMODE(path.stat().st_mode)
            with self.assertRaisesRegex(RuntimeError, "mode0700"):
                launcher.private_directory(path)
            self.assertEqual(before, stat.S_IMODE(path.stat().st_mode))

    def test_linked_ancestor_is_rejected_and_target_preserved(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "PUBLIC_TARGET"
            target.mkdir()
            (root / "PUBLIC_LINK").symlink_to(target, target_is_directory=True)
            with self.assertRaisesRegex(RuntimeError, "symbolic-link"):
                launcher.private_directory(root / "PUBLIC_LINK/state")
            self.assertEqual([], list(target.iterdir()))

    def test_workspace_environment_keeps_home_and_secure_defaults(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            (repo / "packaging").mkdir()
            (repo / "scripts").mkdir()
            (repo / "packaging/session-test.json").write_text(json.dumps({"sdk": "10.0.402", "runtime": "10.0.13"}))
            shutil.copyfile(Path(__file__).with_name("prepare-session-test.py"), repo / "scripts/prepare-session-test.py")
            sdk = repo / "artifacts/session-runtime-security/sdk/sdk-10.0.402"
            metadata = sdk / "sdk/10.0.402"
            metadata.mkdir(parents=True)
            (sdk / "shared/Microsoft.NETCore.App/10.0.13").mkdir(parents=True)
            (sdk / "dotnet").write_text("PUBLIC_NOT_AN_EXECUTABLE")
            (metadata / "dotnet.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"framework": {"version": "10.0.13"}}}))
            (metadata / "dotnet.deps.json").write_text(json.dumps({"libraries": {"System.Security.Cryptography.Xml/10.0.13": {}}}))
            _, env = launcher.environment(repo, {"HOME": "/PUBLIC_ORIGINAL_HOME", "PATH": "/PUBLIC_ORIGINAL_PATH", "NUGET_PACKAGES": "/PUBLIC_EXISTING_CACHE"})
            self.assertEqual("/PUBLIC_ORIGINAL_HOME", env["HOME"])
            self.assertEqual("/PUBLIC_EXISTING_CACHE", env["NUGET_PACKAGES"])
            for key in ("XDG_RUNTIME_DIR", "XDG_DATA_HOME", "DOTNET_CLI_HOME"):
                path = Path(env[key])
                self.assertTrue(path.is_relative_to(repo))
                self.assertEqual(0o700, stat.S_IMODE(path.stat().st_mode))
            self.assertEqual("false", env["DOTNET_GENERATE_ASPNET_CERTIFICATE"])
            self.assertFalse(any("UnsafeDefaultMutex" in key or "Audit" in key for key in env))


if __name__ == "__main__":
    unittest.main()
