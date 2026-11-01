# Translation disposal settlement — functional continuation

Baseline `679b9ecf671e690561a2aa2775fa1110a87eda36`. The product check covered the real translation entry, selected results, session versions, settings draft/commit and retry/cancellation code. SPEC9.9/12/14 still require memory-only history by default; no history persistence was invented. No new defect was established for endpoint or persistence policy, so those implementations were not rewritten. The concrete remaining defect was original-request settlement on disposal after a cancellation callback throws.

## Reproduced defect

The old DisposeAsync permanently disabled admission, then invoked StopRequest before waiting its jobs or clearing snapshots. A throwing original cancellation callback skipped all later settlement. Repeated and reentrant Dispose calls also created independent completion tasks: one could report success even though the original disposal had failed. Field retirement and source release could therefore not be inferred from a completed disposal call.

Two real coordinator regression cases (normal cancellation and throwing cancellation) failed on the baseline. The held secret-cleanup barrier makes original job progress explicit; nested/repeated calls must remain incomplete until that barrier is released, share one completion task, and observe the same exception when applicable. The old cases failed on separate task identity and the lost exception on a later call. Red inputs retain exact source/test source and actual Provider/test DLLs plus hashes, not reconstructed substitute artifacts.

## Implemented behavior

DisposeAsync publishes one TaskCompletionSource-backed completion under a small existing-object-local lock before invoking any user cancellation callback. The disposed flag is permanently set with volatile visibility. Reentrant, repeated and concurrent disposal callers receive this same task, including the same failure; they neither restart cancellation nor accidentally declare a new success. This is operation completion tracking, not a new source authority or request owner.

The one disposal worker captures the original task set and request ID, retires its own offer/submitted/style/live metadata, then stops only the captured original request. Cancellation failure is collected first. Every captured original job is independently awaited, with all multi-task faults preserved. Snapshot cleanup occurs after original job settlement even when cancellation or a job failed. The original prior destination-delivery failure contract remains reported, without duplicating the same already collected fault. All known errors complete the shared task as failure; no error is logged as user text, silently swallowed or used to reopen the coordinator.

Prepare, regeneration and submit cannot enter from disposal callbacks or after failure. Tests reject late result application after disposal. No source consent, profile binding, retry count, API endpoint, credential access or privacy gate was relaxed. Shared result ownership is preserved: disposal releases this coordinator's snapshots rather than clearing another coordinator's shared result store. Native clipboard, settings persistence, palette, focus and growth/fade/250/600ms code are untouched.

## Actual validation

The test rig uses the real production coordinator, RuntimeGate and linked original cancellation tokens. Only secret completion and connection ports are synthetic; connection attempts throw and must total zero. No model or external API is called by the two new cases. Original cleanup is held by a deterministic TaskCompletionSource barrier, then explicitly released and awaited. An expected disposal error may be accepted during test teardown only when it is the exact exception object already asserted by that test; existing cases have no such exception allowance.

Red:2 failed/0 passed, exit1/GroupEmpty. Final related run: **223 passed/0 failed/0 skipped**, not a sum of rounds:9 cancellation-handoff cases (7 retained plus2 new disposal cases),54 text translation,87 terminal-failure,4 terminal-exception,41 clear-content and28 stop-flow. Existing protocol fixtures are local synthetic HTTP, not real service calls. No InstallationDeploymentSession or any of the23 blocked Core tests was selected. No full Provider/Core rerun occurred.

Provider product, Provider test and App Windows-target cross-builds all completed0 warnings/0 errors, exit0/LeaderJoined_GroupEmpty, confirmed stdout closure. Final compiler times were2.97s/13.10s/4.58s; original final TRX reports about2s test duration. SDK10.0.402 and its existing dependencies were reused with `--no-restore`, disabled project-reference rebuilding/shared compilation/node reuse,120s command limits and1MiB retained-log limits. No Windows application launched.

Actual originals are local under `artifacts/2026-11-01-disposal-settlement/`: red-results/red.trx, red-inputs source/DLL manifest, final results/related.trx, command receipts/logs,223 exact IDs/names and final-bytes.json. Provider DLL SHA256 `fe885fa8aafc5600f0b3ce5e061152ac49269a2a6c2fe17da5a1c0d0fe17e2b6` is the same in the tested and App output locations; test DLL SHA256 `f5ad9829e95b70a239a906397175efaff790239eeb5d150b251ce22c5d13d797`; App DLL SHA256 `1ad66efec9a7dff64db9aead78531eb7c0432bd49025cbedb94405c8c053ac81`. None of these binaries/logs is committed or uploaded.

Independent readonly review approved the design and final source, read the original2F and223P results, checked all six source/DLL hashes and confirmed group settlement. The reviewer did not execute tests. Final text/range review precedes normal preparation-branch push; branch/path triggers do not match any CI workflow.

## Remaining qualification and preservation

This closes a proven coordinator disposal defect; it is not whole-product or Windows exit qualification. Native clipboard/focus/capture, positive production startup issuer/install sources and the Windows release/packaging gates still need their original actual evidence. Old release ZIP remains unchanged. Default session history still lives in memory only. The sampled endpoint/settings-persistence review is not a new full qualification result.

All5,894 protected files were rehashed:918,676,127 bytes, zero mismatches, no deletion/compression/movement. Free workspace capacity after builds/evidence was1,653,227,520 bytes. Unknown processes/locks were not touched. No UID-map retry, alternate isolation route,23-case Core execution, user PC, Windows/CI window, main, Release, new permission/credential/fee was used. The original resource-proof gate and cumulative diagnostic budget remain unchanged/unverified; distinct product tests do not reset them.
