# Full lifecycle validation: local preparation after SourceHead Pass

This task is local development/tests only. No new upload, push, Windows/CI run, real model, credential or network/security permission change is authorized or performed. Remote base remains `b39a9076e69bec3c277e5664428ff142eaaef9f5`, tree `2bcfa3dd1a23c0da085f9d5303d491354e668686`.

## Evidence preserved and meaning

The only SourceHead Windows run38524993308/job115723148036/attempt1 completed success at b39a9076: actual creation/resume/root exit/Job empty/cleanup confirmed, Exit0, no rescue, HEAD matched. It did not compile either lifecycle tool, execute the four cases, verify locked ancestry, or pass UIA/matching/performance. Original run38443925339 failed before compilation; its receipt remains exact SHA25611342729e0e4f79f856c620158417147856ef466c8c29f75c0d66c28ec0cbde5. Its detailed failing stage was absent. The multiple-program selection defect is demonstrated locally; neither the old Windows candidate list nor its exact root cause has been proven.

## Proposed exact identity and history

A future approved delivery must be exactly one child of b39a9076. The chain is future →b39a9076→4bc4ee3→ae965898→locked3ae694. Hence depth5 is proposed for that precise topology; depth4 has been experimentally rejected. If another remote commit arrives, stop and recompute/review the ancestry rather than relying on the number5. Checkout pins the exact event SHA with the existing immutable checkout action, no credential persistence and read-only permission.

The full runner independently executes bounded SourceHead, SourceParent, SourceAncestry and SourceLockedTree checks. HEAD must be a complete lowercase40-hex record matching GITHUB_SHA; rev-list must contain exactly HEAD and the reviewed parent, rejecting merges/extra parents; locked3ae694 must be an ancestor; its tree must equal870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b. Clean tracked state and exact Git diff of the fixed eight runtime inputs are required before compilation. Public booleans ParentVerified/AncestorVerified/LockedTreeVerified/SourceEquivalent become true only after their actual checks. Raw Git output stays private. The existing Job and stream/protocol safety gates are unchanged.

Executable selection for git, cmake and dotnet now uses one finite resolver and the first ApplicationInfo. A missing or invalid first candidate fails; it never joins multiple paths or silently skips an invalid first candidate. The script still invokes the newly built managed DLL through the selected dotnet executable (`dotnet DLL --supervise nativeEXE`), preserving Environment.ProcessPath semantics; no managed apphost is substituted.

## Minimal future Windows contract

Future execution requires a separate explicit bounded approval and exact-file public-upload permission. Standard public-repository windows-2022; one attempt, no automatic retry, job10min, runner step8min. Fourteen sequential external operations retain the existing30-second successful-completion budget each and unchanged rescue observation limits (root2s and Job-empty2s). Synchronous setup calls have no newly introduced interruptible watchdog; outer CI limits are not cleanup proof. Existing helper loading uses PowerShell Add-Type; no SDK/dependency installs, package-source expansion or new legal acceptance is included.

Compile only `tools/KuaiYi.ReuseLifecycleNative` x64 Release (preinstalled VS2022/MSVC14.44/SDK26100) and package-free `tools/KuaiYi.ReuseLifecycle` using existing SDK10.0.401, locked local-empty-source restore and non-reused build nodes. This is not a full KuaiYi App build. Nonzero exits, timeouts, rescue, missing binaries, unsettled processes or missing/invalid evidence fail and stop the attempt.

Exactly Normal, BoundParentDeath, BeforeBindParentDeath and Watchdog, in order. The old acceptance predicates are retained: same held process exit proofs, original IO/task settlement, no rescue; Normal final release calls and EOF, death cases their termination stages, Watchdog expired/kill-issued/settled and EOF. Missing proof, natural exit or kill-not-issued cannot pass Watchdog. Production request Soft250ms/Hard600ms/Idle120ms/Settlement2000ms, Win32 handle ownership, Job isolation, COM lifetime and protocol source are byte-unchanged. There is no target-text selection, production reuse, physical GUI interaction, actual user input or model call.

A separate new workflow `windows-lifecycle-validation.yml` triggers only on its own path. The already-delivered SourceHead workflow is untouched, so a future approved push of this candidate does not also rerun SourceHead. Pushing this new workflow would start the proposed four-case validation and must not occur under the consumed SourceHead-only authorization.

## Actual local validation

New contracts72/72 passed: three multi-candidate scalar selections, actual installed git/dotnet selection, absent cmake fail-closed rejection, malformed/extra identity/merge rejection, receipt kind/scoped workflow checks, one actual Linux full-runner Blocked entry, and real local shallow clones. Depth4 cannot access locked3ae; depth5 validates the unique reviewed parent, ancestor, locked tree and unchanged runtime diff. Actual fixture mutations to global.json and the app manifest make that diff fail, and exact-byte restoration returns it to clean. No production file is mutated by these fixture checks.

Existing wrapper contracts45/45 passed without deleting or weakening assertions. Existing Windows SourceHead and old lifecycle evidence retain their original scope; no Windows result is claimed from these local tests. Initial new harness assumed cmake existed and failed; its exact source was preserved privately. It was corrected to verify rejection when absent, without installing software or pretending to have tested a real CMake executable. Two real installed application lookups and one real missing-application rejection are reported separately. No product build was run locally for this script-only change.

## Candidate files and publication boundary

1. .github/workflows/windows-lifecycle-validation.yml (new scoped workflow).
2. tests/scripts/run-lifecycle-windows-ci.ps1 (scalar resolver plus exact source/ancestor gates).
3. tests/scripts/lifecycle-ci-evidence.ps1 (three finite source-operation labels).
4. tests/scripts/lifecycle-runner-contracts.ps1 (pure selector/identity/runtime-input contract).
5. tests/scripts/lifecycle-runner-contracts.test.ps1 (new local contracts).
6. docs/validation/lifecycle-full-next-validation.md (this plan/results/limits).
7. docs/validation/source-head-next-diagnostic.md (append actual one-run SourceHead status).
8. docs/progress.md (append actual SourceHead result and local full-runner preparation).

The publication scan found no credential-like tokens or private-key headers, no real account/home paths or personal records in this candidate. The new test uses only a deliberately synthetic fixture@example.invalid Git identity and synthetic sentinel text. Documents contain public repository/commit/run identifiers, engineering versions/error categories and relative artifact paths. They are not production dependencies. No raw private log or user text/image is included in the candidate tree. Future exact-file fingerprints must be approved/reverified before upload.

Current status remains: original Windows failure root cause unknown; lifecycle native/managed compilation and four cases NotRun; UIA text/matching/performance NotRun; ProductionReuseOff, ModelCalls0, P0/E06Blocked. A successful future limited contract would still not constitute cross-application/UI/installation acceptance.
