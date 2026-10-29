# Remaining Core resource and settlement audit

Baseline: `25f31f8f21e01c1576ea41b2e228c6dc3304f80c`. Complete-case admission remains Blocked; the product-file sub-bound and a separate supervisor defect below have been independently reviewed. No remaining Core case was started.

## Fixed remaining shapes

Original ordinals refer to the preserved 51-failure identity list, not a new selection:

| Shape | Original ordinals | Count |
| --- | --- | ---: |
| Ordinary bootstrap, direct compensation | 29, 34, 35, 36, 38, 43, 44, 50, 51 | 9 |
| Ordinary bootstrap, restored session | 30, 47 | 2 |
| Ordinary bootstrap, Operation | 31, 41, 42 | 3 |
| Ordinary bootstrap, Installer early path | 32, 40, 49 | 3 |
| Self-contained bootstrap, StartupSettings | 33, 37, 39, 48 | 4 |
| Self-contained bootstrap, Startup | 45, 46 | 2 |

## Next ordinary case: product files

`CompensationCancellationAfterTemporaryPreparationCannotReplaceFirstRecord` calls `FailedDeployment(r,true)` and then cancels at the first compensation temporary record's flush. Sources/Commit, Store.Apply, generation/bootstrap candidate preparation, publication, original session and compensation were traced. It uses the ordinary five-file bootstrap contract, not the 189-file runtime extension.

Fixed source closure is 4,401,512 bytes, including its source catalog and portable driver. Ordinary bootstrap plus catalog is 1,462,587 bytes. Source plus two installed generations and source bootstrap plus two installed bundles give `3*4401512 + 3*1462587 = 17592297` bytes. Staging is published with Directory.Move, so it is not another simultaneous full copy. Two installed catalogs at most 1 MiB each and three derived records at most 64 KiB each per generation add 2,490,368 bytes. Payload/derived sub-bound: **20,082,665 bytes**.

The following union deliberately counts both move names and unreachable failure archives. It is a conservative explicit-path bound, not a measurement of a previous fixture:

| Control/marker path group | Count |
| --- | ---: |
| Fixed owner, four locks, current/bootstrap/host/pending | 10 |
| Store current temporary names | 2 |
| Previous bootstrap pending/active temporary/prior/finished/failed | 5 |
| Previous host registration pending/finished/failed/withdrawn | 4 |
| Deployment before/generation/bootstrap/prepared/terminal/ticket/failed/committed | 8 |
| Publication steps 1/2/5, forward/reverse/removed | 9 |
| Execution owner/finished | 2 |
| Three launch names and four browser-host names | 7 |
| Five compensation fixed names and steps 1/4/5 temporary/removed | 11 |
| PUBLIC_OFF and PUBLIC_SHELL_CREATED | 2 |
| Total | 60 |

Actual metadata gates are 128 KiB for before/prepared/compensation-plan; 16 KiB for tickets, terminals and G/B intent; 4 KiB for current; generally 8 KiB for bootstrap/host records; 49,152 bytes for launch; up to 64 KiB for browser-host; 1 KiB for execution. Charging all sixty at 128 KiB gives **7,864,320 bytes**. The reviewed product explicit-file sub-bound is therefore **27,946,985 bytes**. This is logical file content, not an allocated-block or complete-case bound.

## New settlement defect and actual regression

A synthetic leader exited 0 after spawning one child with stdout/stderr closed. The child wrote a 300-byte file after 0.3 seconds, then exited. With the original 100-byte work budget, the supervisor waited for the child but accepted its oversized output: the new regression failed with `RuntimeError not raised` in 0.433 seconds.

The supervisor now rechecks the same original work scope after group settlement and records `settledOutputBytes`. Excess fails and retains original files. This does not lower any limit or replace the original in-flight/free-space monitor. It closes false acceptance after settlement; it is **not a hard bound on peak disk growth**.

Four directly related tests passed in **0.502 seconds**: late child output, original non-log output monitor, strict log prefix, and independent pipe-close error preservation. Actual command selected those four individual methods in `scripts.test_managed_publish.ManagedPublishTests`; raw log remains local at `artifacts/2026-10-29-core-budget-closure/targeted.log`. The prior 22-test suite was not repeated or counted as new. No product process, Core case, Windows, CI or remote log was executed/read.

## Exact unresolved peripheral contracts

1. Nonfixture files/random directories under TMPDIR created by VSTest, testhost and the runtime. A maximum regular-file count and the actual possible write set are not established. Socket/FIFO counts are separate from regular-file lengths.
2. Results-directory paths other than the single requested TRX, including possible run/attachment directories. Disabling collectors/blame narrows behavior but is not itself a proven file-count contract.

The installed SDK console and TRX logger identify as **18.7.0-release-26505-111**. The locked NuGet testhost is **18.10.1**, repository commit `87dfd4b2d2bacd91ad69e009ade6f4715b3b46ec`; xUnit adapter **3.1.5** declares commit `1b188a7b0a069d7fc94ae3c0b251f1302b602b63`. These were read from existing DLL strings/nuspec files. An arbitrary current upstream source tree cannot establish the installed binaries' output contract. No adapter/runtime was replaced.

A proposed 32 MiB RLIMIT_FSIZE was **not applied**: a per-file limit does not bound file count, directories, sockets or aggregate storage. Likewise, the strict 1 MiB captured-log reservation alone cannot fill these gaps. No guessed 96 MiB reservation or former 512 MiB reservation has been promoted into a complete-case proof.

## Current capacity and preservation

Initial workspace free space this slice: **1,672,286,208 bytes**. Frozen aggregate content allowance remains 1,342,177,280 bytes, protected content 918,676,127 bytes, leaving **423,501,153 bytes**. The 5,894 protected files were rehashed after the targeted supervisor tests with no mismatches; no protected or unknown data was removed, compressed or moved. The original cumulative Core diagnostic time remains 216.468 seconds, leaving at most 683.532 of its 900 seconds. New sub-second supervisor regressions are not Core runs and do not reset that budget.

The next case is not currently blocked by a proven physical ENOSPC event. Its complete storage requirement cannot yet be stated because the two peripheral counts are unknown; adding an invented number of bytes would not establish safety. A concrete conditional lower bound for later self-contained upgrade cases is available: two roots that both must remain live/retained require at least six runtime copies, `2*3*80453412 = 482720472` bytes, exceeding the current aggregate headroom by **59,219,319 bytes before other files**. This does not assume that all six self-contained cases retain their roots; each lifecycle must determine that.

No remaining test was admitted on a partial proof. Qualification is still historical **28/51 passed, 23 unverified**, not complete Core. Main, Release, original Windows windows, source/password/integrity/cancellation gates and the 250/600 ms contract remain untouched.
