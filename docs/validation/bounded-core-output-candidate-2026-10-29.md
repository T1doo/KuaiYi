# One-case bounded output candidate — execution blocked

Status: **BlockedRuntime and incomplete resource-proof admission**. The fixed original case has not run. This is an implemented, locally compiled preparation, not a Core pass.

Baseline `6e2781efa2b9c748b21719c6a965f41e50561904`. The only selected case is `KuaiYi.Core.Tests.InstallationDeploymentSessionTests.CompensationCancellationAfterTemporaryPreparationCannotReplaceFirstRecord`, original ordinal29. No other filter or fallback runner is exposed.

## Implemented write domains

`scripts/run-bounded-core-case.py` defaults to read-only inspection. Preparation builds only the test project with project references disabled. Execution proposes the installed bwrap with readonly root, dropped capabilities and one newly created fixture directory bound writable. Repository/cwd, unchanged HOME, user configuration and SDK remain readonly; TMP/TEMP/XDG/bundle-extraction directories are private readonly paths. No unsandboxed fallback, force flag or automatic retry exists. This is essential: chmod0500 on a temporary directory alone is not a security sandbox and does not close other writable fallbacks.

Only the test Rig gains an opt-in path selector. The environment key is not read by production. Without it the original random root and pipe names remain unchanged. An explicit invalid value fails rather than falling back. The root must be a precreated empty0700 canonical Linux directory, with a GUID name, no linked ancestor and a bounded absolute Unix pipe path. Only one Rig can claim it per test process. Assertions, product preparation, authority checks and original cleanup/retention code are unchanged. The proposed namespace bind pins the selected root, while the controller records/checks its device/inode. The entry remains disabled pending complete review of the reservation and file-count/allocation formula.

The actual SDK TRX logger writes to one precreated FIFO in a readonly results directory. A parent-side reader captures only the original emitted bytes, capped at1MiB; stdout/stderr has a separate1MiB cap. Neither stream is a replacement result. Completion requires the original process group, reader completion and true EOF, no truncation/error, parseable XML, exactly one original ID/name, Passed, exit0 and GroupEmpty. Missing writer, open writer after settlement, overflow, malformed output or unknown closure fails. Reader, directory accounting and receipt write/close settle independently; failed accounting is null/Unknown and the original error is preserved first.

## Fixed implementation/source evidence

The existing SDK `.version` binds its source to `dotnet/dotnet` commit `a8dd0577c5c13f43a2f57fe33b4ff11b90f6371e`. Its [TRX logger](https://github.com/dotnet/dotnet/blob/a8dd0577c5c13f43a2f57fe33b4ff11b90f6371e/src/vstest/src/Microsoft.TestPlatform.Extensions.TrxLogger/TrxLogger.cs) uses the explicit filename overwrite path and FileMode.Truncate. Its [artifact manager](https://github.com/dotnet/dotnet/blob/a8dd0577c5c13f43a2f57fe33b4ff11b90f6371e/src/vstest/src/Microsoft.TestPlatform.CrossPlatEngine/PostProcessing/ArtifactProcessingManager.cs) returns when `VSTEST_DISABLE_ARTIFACTS_POSTPROCESSING` is set. Sources were read from that fixed revision, not arbitrary upstream main, and retained locally with their license headers. xUnit adapter source is pinned by its existing nuspec to `1b188a7b0a069d7fc94ae3c0b251f1302b602b63`.

Local evidence is under `artifacts/2026-10-29-core-output-contract/`:

| Evidence | Actual result |
| --- | --- |
| Initial File.Open/XmlWriter FIFO probe | exit0/GroupEmpty,70 bytes,0.981s including compilation |
| Readonly TMP / absolute NamedPipe / readonly-results FIFO probe | exit0/GroupEmpty,69 bytes,1.045s; first compilation had a missing brace and is retained separately |
| Actual SDK TrxLogger ReserveTrxFilePath + PopulateTrxFile, synthetic XML only | exit0/GroupEmpty,70 bytes,1.090s; actual overwrite branch executed |
| Exact linked test-helper policy probe | 10 checks passed,1.295s including compilation; invalid/default/duplicate-claim paths |
| New Python stream and launch-gate tests | 5 passed,0.024s |
| Independent-settlement and disabled-proof-gate tests | 4 passed,0.003s |
| First test-only build | compiler exit0,0W/0E; reused nodes failed settlement and were terminated in the original group; receipt records exited unreaped zombies, not GroupEmpty |
| Corrected test-only build | exit0,0W/0E,GroupEmpty,1.787s; node reuse/shared compilation explicitly disabled |

These are infrastructure checks, not nineteen Core passes. The earlier22-test publisher suite was not repeated.

Current test DLL SHA256: `3477b305f80de87cc7bd622519740ca1e498c0f5301c8dd491548b350f0ace2c`. No Core case has run on these bytes. Old `174de8f6e8c23253f2004dd23034816f6fae97894b5d52148106428885bcb35e` is retained at `prepared/original-test-binary/KuaiYi.Core.Tests.dll`. The r02 manifest's legacy `OriginalTestDllSha256` field is3477: it means the value immediately before that second build, not the historical174 baseline. The loader checks the manifest kind, successful original build/GroupEmpty, exact two-source set, charge and DLL hash. The449 product inputs still match the prior snapshot. Product Core/portable driver DLLs also match the prior protected fixture hashes.

## Actual runtime failure and minimum condition

The single harmless sandbox setup probe was:

`bwrap --unshare-user --cap-drop ALL --ro-bind / / -- /bin/true`

It failed with `bwrap: setting up uid map: Read-only file system`. `/bin/true` did not start; neither did VSTest or the original Core case. No privileged retry, alternate namespace/tool route, system/proc change, permission expansion or Windows action was attempted. Read-only inspection confirms the uid-map filesystem is readonly. The minimum runtime prerequisite is a supported readonly-root execution mechanism with exactly the single fixture write domain and original bounded output streams. More disk space does not resolve this failure. The present task cannot enable that prerequisite by changing host security permissions.

`RESOURCE_PROOF_COMPLETE` deliberately remains False. Even a changed uid-map configuration cannot silently enable the proposed128MiB/416-file admission. Those numbers are candidate values, not independently approved whole-case bounds. The fixed original60-path/27,946,985-byte product sub-bound remains separate from total output/block-allocation proof. This gate must stay closed until the complete formula is reviewed.

## Preservation and budget

The5,894 protected files were rehashed after the preparation/checks, totaling918,676,127 bytes with zero content mismatches. They were not deleted, compressed or moved. Old failure receipts, failed probe compilation and failed build settlement remain local. No main/Release/CI/Windows/model/capture action occurred.

Historical qualification stays28/51 directed cases on the old test bytes, with23 unverified. The new test bytes have zero Core executions. Original consumed time216.468s is retained; all this preparation is conservatively charged once as60s, exceeding the measured diagnostic/build command time (under30s). The remaining budget is at most623.532s; no reset or expansion. The1,342,177,280-byte aggregate allowance and423,501,153-byte remaining allowance are unchanged. No case reservation was consumed by a Core run.
