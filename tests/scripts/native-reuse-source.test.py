from pathlib import Path

root = Path(__file__).resolve().parents[2]
old = (root / "tools/KuaiYi.ComCaptureProbe/ComCaptureProbe.cpp").read_text()
new = (root / "tools/KuaiYi.SelectionReuseNative/SelectionCapture.h").read_text()
def body(source):
    return source[source.index("template<class Trace> result capture("):source.index("template<class T> void json_array(")]
expected = body(old)
actual = body(new).replace("request_wstring", "std::wstring")
for holder in ("root", "focused", "hit", "walker", "next", "selections", "range", "child", "current"):
    actual = actual.replace(holder + ".GetAddressOf()", "&" + holder)
actual = actual.replace("origin,result& info,Trace& trace,IUIAutomation* persistent)", "origin,result& info,Trace& trace)")
start = expected.index("    auto com_phase=trace.observe(1);")
end = expected.index("    info.stage=3;", start)
actual = actual.replace("    ComPtr<IUIAutomation> api(persistent);\n", expected[start:end])
assert actual == expected, "Capture source/gesture/password/geometry/limits/final identity gates drifted"
print("NATIVE_REUSE_CAPTURE_GATES passed=1 failed=0; normalized source comparison, SDK/COM NotRun")
