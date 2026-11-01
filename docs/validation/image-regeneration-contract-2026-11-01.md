# Image regeneration: classification-reuse contract

Baseline `14edb8a2b9d9a1161763ee1be69ee4aff567874f`. SPEC7.6 requires regeneration to reuse the current image/classification/override unless the user explicitly requests image rejudgement. This slice closes an independently testable offline contract defect; it does not enable normal Shell image replay.

## Dependency and authority boundary

The normal ProductionScreenshotCoordinator admits an original image once and records the consumed SourceVersion/SourceId. CanUseSource, IsOfferCurrent and Prepare all retain the original consumed/IMAGE_REUSE_DISABLED refusal. Shell RegenerateResult also refuses image reuse. None of these production files or normal UI routes changed (working bytes were compared to the baseline and recorded locally).

Normal replay therefore needs more than removing a boolean: a reviewed retained-image ownership/lifetime path, fresh per-request confirmation/binding and correct handling of single-use OCR/vision approvals, classification/normalization mapping and cancellation. An old receipt, path, version ID or classification result cannot supply that authority. Original encoded request bytes are cleared/disposed by the send job and must not be reused through stale references. Those production dependencies remain unimplemented/unverified here; no new source issuer or permissions were invented.

OfflineScreenshotCoordinator is the existing separate P6 protocol slice, never installed in the normal Shell. It already requires immutable credential-free loopback fixture authority and owns a fixed final screenshot owner. Its explicit regenerate/reclassify actions can be exercised with generated8x8 public pixels and local protocol replies, without reading a real screenshot/file or contacting a model.

## Real defect and implementation

Previously, TryStart(regenerate:true,reclassify:false) fell back to a new classification when the selected generation had no committed Classification. A pending classifier could be cancelled/replaced, or an unsuccessful classification could be silently resubmitted as part of the default regenerate command. That loses the distinction between reuse and explicit rejudgement.

The command now checks before ticket creation, commandRevision increment, version allocation or cancellation: the selected snapshot must match the original current owner's SourceVersion and SourceId; default regeneration also requires a completed, locally validated classification. Missing/failed/pending classification refuses without replacing Running, the canonical identity, snapshot, old failure or result. It does not reinterpret the absence as a reclassify command.

Explicit reclassify still follows every original source/settings/profile/binding/consent/user-action check and validates a full completed classifier response before committing cache. Valid classification with unknown token counts remains reusable: token Usage Unknown is not classification failure or new authority. Likewise, the valid typed ImageKind.Uncertain result remains a classification, with its existing uncertainty warning. Current mode/style, three-version retention, normalized mapping validation and the original classification receipt reference remain unchanged. No retry loop or new automatic request was added.

## Actual red and green evidence

Six new cases run both OpenAIChat and AnthropicMessages. Baseline results were **4 failed,2 passed**, exit1/GroupEmpty: default regeneration was incorrectly accepted for pending/failed classification; known classification with unknown usage already reused successfully. Original sources/test source and actual App/test DLLs are preserved with hashes in red-inputs, alongside the real red TRX/logs.

After the fix, the actual related run is **218P/0F/0skip**:163 OfflineScreenshotRoutingTests (including the6 new cases) and55 existing classification/parser/planner cases. Old rounds are not added to218. The new cases assert the original task/identity/snapshot are retained on refusal, exact request counts, failed-version retention, same source IDs and original classification-receipt reference. Explicit rejudgement adds exactly1 classification+1 generation; subsequent reuse adds exactly1 generation and does not recount/recreate the classification usage receipt.

Protocol calls use only the existing fixed local mock server, no external API/model or credential. Synthetic pixel ownership and protocol correctness do not prove Windows capture or production source lifetime. Provider-test and real App Windows-target cross-builds completed0 warnings/0 errors, exit0/LeaderJoined_GroupEmpty, stdout closure confirmed. Compiler times13.98s/4.81s; TRX reports about5s. Existing SDK10.0.402/cache was reused without restore/download; commands had120s and1MiB-log limits, shared compilation/node reuse and project-reference rebuilding disabled. No Windows app launched.

Original receipts/logs, red/final TRX,218 identities and4 source/DLL hashes remain local under `artifacts/2026-11-01-image-regeneration-contract/`. Final App DLL SHA256 `3f2d2f89b24faad5dda4dc413890f4ee9e95cdef8d8a86510beb1d5dc25280cd`; Provider test DLL (KuaiYi.Provider.Tests.dll) SHA256 `9823188130e2ef7ab02b6fd38a8d8e7f3492d994298b5bb4aa0d1a864dd1c24c`. No binary/log archive is committed/uploaded. Independent readonly review approved design and source, read original4F2P/final218P and checked all4 hashes; it did not run tests.

## Limits and preservation

This is a repaired offline contract segment, **not completed production image reuse/regeneration**. The normal consumed/IMAGE_REUSE_DISABLED gate and UI explanation remain intact. No source receipt is promoted to authorization. Windows/private images, actual capture, model/credential access, native UI and production replay remain unexecuted. UI palette/growth/250/600ms, memory-only history, main and old release ZIP remain unchanged. No CI/window/user-PC action, new permission/credential/fee or isolation fallback occurred.

The23 blocked Core cases, UID-map refusal and original resource-proof/cumulative-budget boundaries remain unchanged. All5,894 protected files were rehashed:918,676,127 bytes, zero mismatches, no deletion/compression/movement. Observed workspace free capacity was1,648,553,984 bytes. Unknown processes/locks were untouched. Ordinary preparation-branch source push does not match any existing CI trigger.
