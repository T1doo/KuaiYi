"""Real local Git checkout regression. No Windows desktop claim or network access."""
import hashlib
import json
import pathlib
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
NAMES = ("SPEC.md", "快译_产品与技术开发规格_v1.0.md")
EXPECTED = "ea3aa6529405bcc703986988fd089f476ee8f991d867e2220b3b20c439126166"
evidence = []
with tempfile.TemporaryDirectory(prefix="kuaiyi-spec-checkout-") as temporary:
    copy = pathlib.Path(temporary) / "repo"
    subprocess.run(["git", "clone", "--no-local", "--no-checkout", "--depth", "1", str(ROOT), str(copy)], check=True, capture_output=True)
    # Reproduce the initial repository attribute, independently of the current fix.
    (copy / ".gitattributes").write_bytes(b"* text=auto\n")
    subprocess.run(["git", "-C", str(copy), "-c", "core.autocrlf=true", "checkout", "HEAD", "--", *NAMES], check=True, capture_output=True)
    for name in NAMES:
        canonical = (ROOT / name).read_bytes()
        old = (copy / name).read_bytes()
        assert hashlib.sha256(canonical).hexdigest() == EXPECTED
        assert old != canonical and old.replace(b"\r\n", b"\n") == canonical
        evidence.append({"name": name, "case": "old-attributes-autocrlf", "sha256": hashlib.sha256(old).hexdigest(), "bytes": len(old), "crlf": old.count(b"\r\n"), "onlyEolDiff": True})
        (copy / name).unlink()
    (copy / ".gitattributes").write_bytes((ROOT / ".gitattributes").read_bytes())
    subprocess.run(["git", "-C", str(copy), "-c", "core.autocrlf=true", "checkout", "HEAD", "--", *NAMES], check=True, capture_output=True)
    for name in NAMES:
        actual = (copy / name).read_bytes()
        assert actual == (ROOT / name).read_bytes()
        assert hashlib.sha256(actual).hexdigest() == EXPECTED
        evidence.append({"name": name, "case": "fixed-attributes-autocrlf", "sha256": hashlib.sha256(actual).hexdigest(), "bytes": len(actual), "crlf": actual.count(b"\r\n"), "exactCanonicalBytes": True})
print(json.dumps({"host": "local Git", "casesPassed": len(evidence), "cases": evidence, "windowsDesktopAcceptance": "NotRun"}, ensure_ascii=False, indent=2))
