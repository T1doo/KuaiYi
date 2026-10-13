from pathlib import Path
import re
root = Path(__file__).resolve().parents[2]
native = (root / 'tools/KuaiYi.ReuseLifecycleNative/LifecycleWorker.cpp').read_text()
cmake = (root / 'tools/KuaiYi.ReuseLifecycleNative/CMakeLists.txt').read_text()
code = re.sub(r'//[^\n]*', '', native)
assert 'SelectionCapture' not in code and 'SelectionCapture' not in re.sub(r'#[^\n]*', '', cmake)
assert not re.search(r'\b(?:ElementFrom\w*|GetFocusedElement|GetParentElement|GetFirstChildElement|GetNextSiblingElement|GetCurrent(?:Pattern|Property)\w*|get_Current\w*|GetSelection|GetBoundingRectangles|GetText|GetRuntimeId|QueryInterface|GetForegroundWindow|FindWindow\w*)\s*\(', code)
assert set(re.findall(r'(?:api|held)->(\w+)\s*\(', code)) <= {'Release'}
assert 'Sleep(INFINITE)' in code and 'CoInitializeEx(' in code and 'CoCreateInstance(' in code and 'CoUninitialize(' in code
supervisor = (root / 'tools/KuaiYi.ReuseLifecycle/LifecycleSupervisor.cs').read_text()
assert 'ExclusiveKillJob' not in supervisor and 'AssignProcessToJobObject' not in supervisor and 'DuplicateHandle' not in supervisor
assert 'DeathPlan.KillOnlyParent(parent.KillRequiredExact)' in supervisor
tracking = (root / 'tools/KuaiYi.ReuseLifecycle/ProcessTracking.cs').read_text()
assert not re.search(r'\.Kill\s*\(\s*true', tracking)
assert 'p.SafeHandle' in tracking and 'p.StartTime.ToUniversalTime().Ticks!=id.Created' in tracking
assert 'var original=Process.WaitForExitAsync(token);Work.Keep(original);return original;' in tracking
print('LIFECYCLE_NO_TARGET_SOURCE passed=9 failed=0; source assertions only, Windows NotRun')
