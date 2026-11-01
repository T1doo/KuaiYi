# Translation cancellation handoff and clear settlement

Baseline `a8be3bd0bad776aac18033e21c9f3b133e129ad3`. This is a functional coordinator fix for SPEC9.9,10 request cancellation/terminal recovery,12 session memory and14 privacy. No visual redesign or disk history was added. Current-source memory still holds at most three result versions; failure/retry uses existing snapshots and authorization.

## Reproduced defects

RejectConfirmation and SetSessionStyle called StopRequest before retiring their original offer/style state. Original cancellation callbacks can synchronously prepare/submit a new valid request. On return, the old operation could overwrite that new winner. A throwing cancellation could also leave the rejected offer current.

Clear similarly stopped the original request before clearing its fields/results. A callback could start a newer request during the clear, then the original clear would erase its tracking while the canonical gate still recognized the newer request. Separately, ClearAsync skipped original-job waiting if Clear threw. Capturing jobs only after cancellation could miss a task that completed/was removed during the callbacks.

The real production coordinator, original gate and linked cancellation tokens reproduce these failures: the baseline run of seven new cases was **6 failed,1 passed**, original exit1/GroupEmpty. This was not inferred from UI behavior. Red test inputs include the exact coordinator/test source and the two actual DLLs retained locally with hashes. The first attempted test build had CS0104 from an ambiguous test-only SecretLease name; that failure is retained, and no tests ran until the type was qualified.

## Implemented handoff

RejectConfirmation first retires only its matching offer and matching submitted pointer, then stops the captured original request ID. SetSessionStyle commits the selected session's next-style choice and retires the matching unlaunched offer before cancellation. Neither writes those fields after callbacks return. Thus a callback's newer valid request/style survives; already launched snapshots remain frozen. Stale offer rejection is a no-op. Canonical StopRequest still targets only the original ID.

Clear has a temporary volatile clearing flag. It clears its own session/style/offer/snapshot/result state before stopping the captured original request. Prepare, regeneration, submit, offer-current and style commands refuse entry during that synchronous clear; recursive Clear returns boundedly. The flag resets in finally even if cancellation throws, so later legitimate requests are allowed. The existing background Eligible predicate also rejects during clearing, with cross-thread visibility. This does not promise to recall a send already admitted before clearing or undo a request already sent.

ClearAsync captures the original jobs under the existing lock **before** cancellation. It independently attempts Clear and awaits those original jobs even if cancellation throws. The original clear error is first; all task faults remain available, while a single error retains its original stack via ExceptionDispatchInfo. A later request is independent of that frozen task set. Field clearing alone is never described as completed job settlement.

No Gate permission, source provenance, profile/secret binding, consent, latest-wins policy or automatic retry limit was relaxed. No new owner/lease/authority system was introduced. The change concerns command lifecycle only. The UI, warm palette, growth/fade geometry,250/600ms and native clipboard path are unchanged.

## Actual final validation

Seven new cases exercise callback winner/current identity and duplicate-running reuse, style order/callback choice, cancellation exception/offer retirement, stale rejection, callback admission refusal and nested clear, and a held original cleanup barrier proving ClearAsync waits despite a cancellation exception. The test connection callback refuses every dial, secret opening is held until original cancellation, and each rig settles its owned tasks. They assert zero connection attempts and use only fixed synthetic sources; there is no model or external service.

After the primary fix,7/7 passed. Independent review required capturing jobs before Clear and preserving multiple job faults; the final cross-thread clearing eligibility addition then received its own final build/regression. The final r02 run is **221 passed,0 failed,0 skipped**, not a sum of rounds:

| Actual selected class | Cases |
| --- | ---: |
| TextCancellationHandoffTests | 7 |
| TextTranslationTests | 54 |
| TextTerminalFailureTests | 87 |
| TextTerminalExceptionTests | 4 |
| OverlayClearContentTests | 41 |
| OverlayStopFlowTests | 28 |

The existing protocol/recovery cases use their fixed local HTTP fixtures, not real APIs/models. No InstallationDeploymentSession test or any of the23 blocked Core cases was selected. No full Provider/Core suite was run. Test, Provider product build, Provider-test build and App Windows-target cross-build all exited0, stdout closed confirmed and original command group empty. The final compiler durations were3.27s/1.06s/0.99s, respectively. Final TRX reports about2s test duration. SDK10.0.402 and existing dependencies were reused with no restore/download, shared compilation/node reuse disabled, each command bounded120s and its retained log1MiB. Windows application was not launched.

Actual originals live only under `artifacts/2026-11-01-cancellation-handoff/`: initial failed build, red-r02 original TRX/logs and red-inputs, green/prior-final rounds, final r02 TRX/commands,221 exact IDs/names and `r02-final-bytes.json`. Prior `final-*` belongs to the preceding candidate and was not overwritten or presented as the final bytes. Final provider DLL SHA256 is `a87dca8d1a1bcf3fd054e5b4e2f447af4a6062ace7afd9f85a681e113c6e6e86` in both the actual tested and App output paths. Final test DLL is `219defa36fbdde65b2ede5e11cefea2d85d5b7bb04791628c7d5580d2c035c5d`; App DLL is `a86988b8017818a03f8a54639b1a2167a2cd9c8908c770419d0be67f1b59f526`.

Independent readonly review approved the design, caught the pre-clear task-snapshot and multi-fault settlement gaps, then checked final source, all six source/DLL hash entries and original221 identities/results. The reviewer did not run tests. No binary/log archive is committed, uploaded or used to bypass earlier access denials.

## Preserved limits

Windows focus/clipboard/capture/real desktop behavior and production issuer/install qualification remain unverified. This slice does not claim those gates or a download-ready release. Old release ZIP remains unchanged. No user computer, UID-map probe/fallback, Windows/CI window, main, Release, new credential/permission/fee or real model was used.

All5,894 protected files were rehashed after this work:918,676,127 bytes, zero mismatches, no deletion/compression/movement. Final observed free workspace capacity was1,656,926,208 bytes. Unknown processes/locks were untouched. The old23-case resource-proof gate remains false and its original cumulative budget is not reset by these distinct product tests. Failure originals and prior limitations remain retained.
