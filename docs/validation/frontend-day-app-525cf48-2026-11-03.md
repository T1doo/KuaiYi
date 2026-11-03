# App-only SDK401 compilation — completed once

Source `525cf483e54a4486edcbbecc016e426035281076` compiled successfully with SDK **10.0.401**: **exit 0, zero warnings, zero errors**. This is App cross-compilation only. The newly added input surfaces and tint code now have compiler evidence; they have not been instantiated or exercised on Windows.

The user explicitly approved one App-only execution with a separate 33,554,432-byte incremental logical-output budget, a 25,165,824-byte early-stop threshold, a 60-second overall deadline and a 45-second compiler limit. The two-second termination grace was available but not needed. The unique result directory was absent before this attempt. The working tree was clean and all 564 source hashes matched the approved snapshot.

The supervised window was **2026-11-03 16:11:03.218–16:11:28.100 UTC**, lasting **24.882476191 seconds**. The compilation stage began about 2.156 seconds into the window and lasted 22.267842237 seconds; MSBuild reported **20.73 seconds**. The owned process group was empty on completion. No timeout or early-stop threshold was reached. The authorization window is consumed; no retry or subsequent window was started.

## Actual command and integrity

The command ran from the existing temporary working directory using the installed SDK401 and package cache, without changing the repository's SDK402 lock:

```text
dotnet build <repo>/src/KuaiYi.App/KuaiYi.App.csproj --no-restore -p:EnableWindowsTargeting=true -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:NonExistentFile=<validation>/app-525cf48-once/force-compile-never-created -p:BuildProjectReferences=false
```

The missing-output property forced App's compiler target to execute without deleting build files. Its intermediate DLL write time advanced, and the final DLL matched it. The new App DLL SHA256 is **`1cb726b5eb619ce54e1e2117aedc47c570d220b2d361c7fcd6ac2fc41c5d334d`**, replacing the previous `a85d6f5c…` output.

The seven reused reference projects were not rebuilt. Their production and compiler-reference DLLs, all 309 files in the WindowsDesktop targeting/apphost packages, resolved dependency files and project assets had matching pre/post hashes. The source snapshot also matched after execution. Package and CLI cache inventories did not change. No restore, dependency/cache download or repair command ran; no wire-byte counter was available, so this is not a measured network-traffic total.

## Separate output and time account

The monitor took 76 samples. The maximum observed gap was **2.154 seconds**, including initial preflight hashing. Sampled positive-path-growth high water was **307,598 bytes**; net-growth high water was **303,496 bytes**, and net allocated-growth high water was **286,720 bytes**. Final receipt/inventory writes occurred after the last in-run sample: explicitly adding those 443,606 bytes and the post-accounting record gives a conservative observed execution-record charge of **752,411 bytes**. Repository archival is accounted separately, and Git object growth is outside this monitor.

These are observations, not a kernel hard limit or proof of instantaneous maximum. Unsampled transients, unlinked-open files, unlisted paths, filesystem metadata and allocation peaks may be missed. Positive per-path growth does not refund deletion or shrinking. The previously denied private temporary subtree was excluded before stat/traversal, without an access attempt; no inventory error occurred. Raw private-path inventories remain local; repository evidence contains normalized paths and hashes of preserved originals.

The previous two execution windows remain **97.149980948 seconds** and **1,848,889 observed execution-record bytes**. Adding this separately approved window yields **122.032457139 seconds** and **2,601,300 observed execution-record bytes** across the three windows, excluding separately recorded archival, Git objects and other unknown output. Original content balance zero, earlier known excess lower bound **112,753,763 bytes**, and inherited Core remaining upper bound **622.532 seconds** remain unchanged. No protected file was deleted or newly rehashed, no cleanup credit was taken, and new-container capacity did not reset any account.

## Exact remaining boundary

**Zero tests executed.** Provider compilation/eight cases, the 23 blocked Core paths, new CI, Windows and security-setting changes were not performed. The previous eight passing cases remain attached to their previous source SHA.

SDK402 formal compilation remains **NotRun**. WPF template loading/ApplyTemplate, PART_ContentHost behavior, IME/caret/selection, password masking, tint playback/reversal/reduced motion, actual pixels, independent text scale and native Windows acceptance remain **NotRun**. No main change, deployment, Release or package upload occurred.

Evidence: [safe receipt](../test-evidence/frontend-day-2026-11-03/app-525cf48-once.json), [normalized compiler log](../test-evidence/frontend-day-2026-11-03/app-525cf48-once.log), [reviewed source snapshot](../test-evidence/frontend-day-2026-11-02/visual-detail-source-inputs.json).
