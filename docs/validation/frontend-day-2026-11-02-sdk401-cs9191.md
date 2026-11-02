# SDK401 single-pass failure and source-only CS9191 correction

Historical first-pass report. A separately authorized second pass subsequently validated the corrected source; see [round-2 results and remaining gaps](frontend-day-2026-11-02-sdk401-round2.md). The failure and NotRun states below describe the first pass and correction phase and are retained unchanged as historical evidence.

The one authorized supplemental pass targeted `60bed19e69351ae31c8c3924ea7514db468a0384`. App compilation failed at `NativeTextScale.cs:26` with CS9191: `Marshal.QueryInterface` takes an `in Guid`, while the call supplied `ref iid`. The repository treats warnings as errors. The follow-up changes only that argument modifier to `in iid`; it does not change the interface identifier, ABI, ownership or control flow. Compilation and all tests after the correction are **NotRun**.

## Actual execution

- SDK 10.0.401, selected from the temporary working directory without modifying the repository's SDK 10.0.402 lock. Zero restore or download commands; no dependency repair.
- App build exit 1, MSBuild elapsed 29.61 seconds; the complete supervised execution lasted 30.301053179 seconds. Provider compilation and all eight new cases were **NotRun** after serial fail-stop. The previous 340 passing cases belong only to `80dcecd`.
- All 563 source hashes matched before and after execution. The 87 reused-reference source inputs, resolved package files and project assets remained unchanged. Core actually recompiled before the Windows project failed; its new hash is recorded in the receipt. Provider tests never ran, so no stale test DLL was accepted.
- The owned process group was empty after exit. No file cleanup, protected-file changes, SDK402 download, Windows operation, Core23 run or automatic second attempt occurred.

## Separate incremental account

The user approved this one pass with a 134,217,728-byte incremental logical-output budget, a 100,663,296-byte early-stop threshold and a 120-second overall deadline. Stage limits were App 45 seconds, Provider 45 seconds and the eight cases 15 seconds, with a two-second termination grace. This consumed one attempt; unused time and bytes do not authorize another pass.

The in-run sampled positive-path-growth high water was **520,716 bytes**; sampled net-growth high water was **371,577 bytes**. Final inventory and receipt writes followed that sample, so post-accounting explicitly includes them and its own file: the conservative observed positive-growth charge is **810,951 bytes**. There were 116 samples, with a maximum observed sampling gap of 0.986 seconds. Per-path positive high water does not refund deletion or shrinking.

These are observed amounts, not a hard aggregate quota or proof of instantaneous peak. Unsampled transient files, unlinked-open files, other write paths and filesystem metadata/allocation can be missed. Enumeration of one platform-private temporary directory received an OS permission denial; its contents were omitted, no retry or alternate route was attempted, and its private pathname is excluded from repository evidence. No wire-byte counter was collected; unchanged package/cache files do not establish an exact network-traffic total.

The original content balance remains zero, with the prior known excess lower bound of 112,753,763 bytes retained. The original 900-second Core account and inherited remaining upper bound of 622.532 seconds are unchanged. No cleanup credits, protected-file rehash claim or budget reset is made. Original raw evidence remains preserved; repository evidence contains selected safe fields and a normalized build log, plus hashes of the originals.

## Independent static review

The independent read-only reviewer confirmed the product diff is exactly one line, `ref iid` to `in iid`, matching the reported CS9191. The IID, vtable slot 6 and `out double` signature are unchanged. Successful RoInitialize results, including S_FALSE, retain a matching RoUninitialize; activation and QueryInterface references are released separately; the HSTRING is released in finally. No ownership imbalance was found in the normal HRESULT paths within this static scope. The reviewer did not compile, test, restore, download or retry denied paths. This conclusion cannot establish successful compilation or Windows behavior.

## Exact next verification scope — not executed or automatically authorized

If another pass is approved, use the corrected source commit and refresh its source/dependency/output baseline first. Reuse SDK401 and existing dependencies, with zero restore/download, one attempt, serial fail-stop and the same 128MiB/96MiB polling limits and overall 120-second deadline plus two-second kill grace. No isolation or security configuration changes are needed or proposed.

1. App cross-build, up to 45 seconds: `dotnet build <repo>/src/KuaiYi.App/KuaiYi.App.csproj --no-restore -p:EnableWindowsTargeting=true -m:1 -nodeReuse:false -p:UseSharedCompilation=false`.
2. Only after success, Provider test compilation, up to 45 seconds: `dotnet build <repo>/tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -p:EnableWindowsTargeting=true -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false`. Confirm reused-reference source hashes and the test output's Core DLL against the current rebuilt Core; require current source-linked test compilation.
3. Only after success, `dotnet vstest <repo>/tests/KuaiYi.Provider.Tests/bin/Debug/net10.0/KuaiYi.Provider.Tests.dll` for up to 15 seconds, filtering precisely the methods below and writing a new TRX into a fresh owned result directory. Require exactly eight executed cases; retain any failure and do not retry.

```text
FullyQualifiedName~KuaiYi.Provider.Tests.FrontendControlEdgeTests.IndependentTextScaleKeepsCriticalActionsBoundedAcrossAnchorAndFullRows|FullyQualifiedName~KuaiYi.Provider.Tests.FrontendControlEdgeTests.EnlargedAuxiliaryRowsDoNotCollapseAfterAnimationOrMoveAndReanchor|FullyQualifiedName~KuaiYi.Provider.Tests.MarkdownSourceTests.OriginalCopyDoesNotConfirmAfterEpochRevocationAndRestoration
```

Stop on nonzero exit, timeout, output threshold, source/dependency mismatch, missing dependency, request for restore/download, unexpected test count, or unsettled owned processes. Do not enter the denied temporary path or the 23 blocked Core paths. SDK402 compilation and genuine Windows text scale, notification, popup, focus, DPI and clipboard acceptance remain separate **NotRun** gaps.

Evidence: [safe receipt](../test-evidence/frontend-day-2026-11-02/sdk401-single-pass-60bed.json), [normalized App log](../test-evidence/frontend-day-2026-11-02/sdk401-single-pass-60bed-app.log).
