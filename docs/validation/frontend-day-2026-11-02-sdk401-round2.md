# SDK401 round 2 — eight specified frontend cases passed

Tested source commit: `55a24b78b49767565c71a5efdd6646f1e6d072e4`. The user separately approved this second, single execution after the first pass failed with CS9191. This round completed with **App and Provider builds successful, eight tests passed, zero failed and zero skipped**. It does not establish SDK402 or native Windows acceptance, and no third pass was started.

## Source, dependencies and fresh outputs

The working tree was clean. All 563 source inputs matched the corrected source snapshot before execution and remained unchanged afterward. The snapshot updates only the NativeTextScale argument-modifier file relative to the previous 563-input snapshot; its full SHA256 is `9d85af343d8c0ca27f2b74158da1ed619d4dd6fd24d290ae01c1bdeeb076ebdd`.

Only the installed SDK **10.0.401** was used, selected from the temporary working directory. The repository still pins **10.0.402** with roll-forward disabled. No restore, package recovery or download command ran. All resolved dependency files and assets were present before execution and had matching hashes afterward; package and CLI cache inventories were unchanged. No wire-byte counter was available, so this is not a measured network-traffic total.

The installed compiler targets' `NonExistentFile` output property forced fresh Core/App and test-project compilation without deleting build files. Core and App intermediate DLL write times advanced during this round; App's copied output matched its intermediate DLL. The Core output hash was `120355fd691af8545688a8a56a121b9597c7dcb4b6b382911c0b0525a74166e6`; the test output's Core DLL matched it. App's output hash was `a85d6f5c6a484994613047fa4e97bf1f5ca23d26931921d7e63a58ef4da35552`.

Provider compilation used `BuildProjectReferences=false`. The 87 unchanged-reference source inputs matched their earlier tested snapshot. ImageWorker and MockProvider DLLs retained their prior hashes; test-output copies of Core, Providers, Imaging, ImageWorker and MockProvider each matched the corresponding current production output. The missing Win32 Skia package was not restored or reconstructed, and ImageWorker was not rebuilt.

## Exact execution and scope

The safe receipt records the complete commands, filter, source/dependency hashes and per-case results. All steps were serial, with failure stopping later steps:

| Step | Limit | Actual supervised time | Result |
|---|---:|---:|---|
| App cross-build, no restore | 45s | 39.763s | exit 0; MSBuild 39.38s, 0 warnings/errors |
| Provider test compilation, no restore, no reference builds | 45s | 23.304s | exit 0; MSBuild 22.94s, 0 warnings/errors |
| Specified new tests | 15s | 3.195s | exit 0; 8 passed, 0 failed/skipped |

Whole supervised execution, including its preflight and bookkeeping: **66.848927769 seconds**, within the overall 120-second deadline. All three owned process groups were empty afterward; no termination or file cleanup was needed. Pre-existing unrelated zombie processes were not touched.

The eight cases are six parameter cases of `IndependentTextScaleKeepsCriticalActionsBoundedAcrossAnchorAndFullRows`, one `EnlargedAuxiliaryRowsDoNotCollapseAfterAnimationOrMoveAndReanchor`, and one `OriginalCopyDoesNotConfirmAfterEpochRevocationAndRestoration`. They check pure layout/lifecycle behavior and the linked source-copy guard through explicit Linux test ports. They do not execute WinRT UISettings or measure real WPF text. The previous 340 cases were not rerun or relabeled as passing on this source.

## Separate resource account and omissions

This round had its own **134,217,728-byte** incremental logical-output budget, **100,663,296-byte** early-stop threshold and 120-second process deadline, with two seconds for forced termination when necessary. The monitor counted positive per-path growth without credit for deletion or shrinking. Baselines covered project bin/obj, owned validation output/packages/results/logs, CLI home/cache, owned test TMP and readable platform TMP regular files.

There were 260 samples; the maximum observed gap was 0.588 seconds. In-run positive-path-growth high water was **668,653 bytes**, net-growth high water **430,463 bytes**, and sampled net allocated growth **413,696 bytes**. Final raw receipt and inventory writes followed the final in-run sample. Including those and the post-accounting record gives a conservative observed execution-record charge of **1,037,938 bytes**. Repository archival is [accounted separately](../test-evidence/frontend-day-2026-11-02/sdk401-round2-archival-accounting.json) and Git object growth is outside this monitor.

Monitoring is not a hard quota or proof of an instantaneous maximum. Unsampled transients, unlinked-open files, unlisted paths, filesystem metadata and allocation peaks may be missed. The previously denied private temporary subtree was excluded lexically before stat or traversal: **no read or retry was attempted**, and no new inventory error occurred. Its private pathname is excluded from repository evidence. Raw originals are retained; public logs/TRX normalize checkout/tool paths and host/user/deployment metadata, with original hashes recorded.

The first round remains **Fail**, **30.301053179 seconds**, **810,951 observed bytes**, zero executed tests. The two execution rounds total **97.149980948 seconds** and **1,848,889 observed execution-record bytes**, without resetting or refunding either round. This does not include repository archival, Git objects or unknown earlier output. The older content excess lower bound of 112,753,763 bytes and original Core remaining upper bound of 622.532 seconds are unchanged. Protected files were not deleted or rehashed here.

## Remaining acceptance

SDK402 compilation, real Windows pixels, WinRT text-scale notifications, exact 200/225% glyph layout, popup/focus/mixed-DPI/clipboard behavior, the 23 blocked Core cases and full frontend/native acceptance remain **NotRun**. No Windows, CI, security-setting change, deployment, product merge to main or Release was performed. This second authorization is consumed; unused time/bytes do not authorize another execution.

Evidence: [safe round-2 receipt](../test-evidence/frontend-day-2026-11-02/sdk401-round2-55a24b7.json), [normalized TRX](../test-evidence/frontend-day-2026-11-02/sdk401-round2-new8.trx), [App log](../test-evidence/frontend-day-2026-11-02/sdk401-round2-app.log), [Provider log](../test-evidence/frontend-day-2026-11-02/sdk401-round2-provider.log), [source snapshot](../test-evidence/frontend-day-2026-11-02/tested-source-inputs-round2.json). The [first failure](frontend-day-2026-11-02-sdk401-cs9191.md) remains preserved separately.
