# Settings category search — product slice

Baseline `172aaa06eb3de64fa94d2026bb1de1495250dc24`, original preparation branch. User authorized independent product work while the23 installation Core cases remain skipped. No UID mapping probe, namespace fallback, Windows/CI window, user-PC operation, model, network request or permission change was made.

## Delivered behavior

The real WPF settings header now has a themed category search, a clear button, result count/empty feedback and up to seven category jump buttons. The existing warm palette, responsive layout, navigation and all floating-window geometry/fade/growth timing remain unchanged. Searches such as 字号, API, 隐私, 提示词 and 浏览器 jump via the original navigation selection, preserving draft state and original masking behavior. Refreshing a query does not change the selected page.

Only fixed category names and product keywords are indexed; profile values, addresses, keys, prompts, current content and translations are never read by this catalog. Whitespace-separated terms must match one category, ordinal case-insensitive. The pure catalog refuses queries over96 characters; the UI field has MaxLength96. Blank/unknown input does not suggest unrelated pages. Results retain the original seven-page order. Query text is ephemeral, cleared on close, and never logged or serialized. There is no save, provider or capture operation in the search path.

Ctrl+K focuses/selects the search unless a hotkey-recording field owns keyboard focus. Down without modifiers moves to the first result. Tab/Enter use the normal buttons. Escape anywhere in the search region clears a nonempty query and returns focus without invoking the window's cancel action; an empty query preserves the original cancel contract. Independent source review caught and corrected both a result-focus Escape/draft-cancel issue and Ctrl+K stealing a recorded hotkey before final build. Real WPF event routing is still NotRun.

## Actual validation

Installed locked .NET SDK10.0.402 and existing dependency cache were reused; no restore/install/download. The Provider test project builds with project references disabled, shared compilation disabled and node reuse disabled. The13 exact SettingsSearchTests cases ran once:13P/0F/0skip,42ms reported test duration, original VSTest process1.959s, exit0/GroupEmpty. They exercise six direct categories, cross-category AND/Unicode whitespace, null/empty/unknown queries, overlength refusal, stable page indices and catalog-copy isolation. This is neither full Provider nor Core qualification.

The first App command omitted EnableWindowsTargeting and failed NETSDK1100,0W/1E, exit1/GroupEmpty, before WPF compilation. That original log is retained. The corrected command uses `-p:EnableWindowsTargeting=true`; initial and post-review App cross-builds both completed0W/0E, exit0/GroupEmpty. The final compiler reported5.02s (owned process5.518s). No application was launched. The thirteen tests were not repeated after WPF-only keyboard fixes; the exact catalog/test source and Provider DLL remained unchanged. The final App DLL includes those fixes.

Commands use `--no-restore -c Release -p:BuildProjectReferences=false -p:UseSharedCompilation=false -nodeReuse:false -maxcpucount:1`; App additionally uses EnableWindowsTargeting. The actual test command uses SDK vstest.console.dll, the compiled Provider test DLL, filter `FullyQualifiedName~KuaiYi.Provider.Tests.SettingsSearchTests`, and the original TRX logger. Each newly created command group settled empty, stdout closed confirmed and its1MiB log cap was not exceeded.

Local original logs, command receipts, original TRX,13 exact IDs/names and source/binary hashes are under `artifacts/2026-11-01-settings-search/`. No binaries or originals are committed/uploaded. Final App DLL SHA256 `7da2c3a83c8aeb11b0bd4ecd23e7538efc65de9e932dd53ec53c64fe9eafbbdc`; tested Provider DLL SHA256 `abd3c5c639996f8ff142a78517d7a4152bcf7dba6d07f0b5c293cd8d671de2d6`. The existing independent readonly reviewer read the real TRX, checked the13 identities and all seven source/DLL hashes, and gave limited ACK after the two keyboard fixes. Review did not execute tests or a window.

## Limits and preservation

Real Windows pixel, focus, keyboard-routing, narrow/large-font interactive behavior and draft-edit interaction remain NotRun. No HTML substitute or synthetic screenshot is offered as WPF evidence. The old release ZIP is unchanged and does not contain this feature; source/cross-build is the delivered candidate, not a newly qualified download-ready release. Source/password/integrity/cancellation/cleanup gates and the250/600ms contract remain unchanged.

All5,894 protected files were rehashed:918,676,127 bytes, zero mismatches, no deletion/compression/movement. Workspace free space after builds was1,662,099,456 bytes; this slice's local evidence was28,759 bytes before the final hash receipt. No unknown process/lock was touched. The old23 cases remain unexecuted, their resource-proof gate remains false,128MiB is unapproved and the original cumulative diagnostic allowance is not reset by this distinct product slice. Main, Release and deployment are untouched. The preparation branch does not match any existing workflow push branch/path condition; no CI is intentionally triggered.
