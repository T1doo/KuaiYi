# Installation cancellation-readiness fixture diagnosis

Base: `2a4bed2a5d9c1a386368f32ce4e4ca286c95ab4d`, normal `dev/p7-fixed-signoff`. The only code changes are InstallationApplicationControlTests and InstallationAppControlFixture. All production source, SDK, package locks, permissions and 250/600ms contracts are unchanged.

## Original failure and proven limits

The once-only fixed-e2 full Core result remains 2090P/1F/0skip. Its exact failure was `CloseRetainsTheOriginalBlockedCancellationWorkAndReaderUntilBothActuallySettle`, 0.2255300s, IOException Broken pipe (inner SocketException), Peer.Send:42 / Ping:44 / body:183. Cleanup recorded the actual original child exit0 and original IO settled. Those original logs do not record READY/Read/callback phases, so neither this diagnosis nor the green run establishes its historical unique cause. Original full TRX and raw failure XML remain in the prior fixed-signoff archive; an exact XML copy is also included here.

READY reports protocol readiness; it does not promise the test port has a held cancellation registration. Two deterministic fixture-only schedules prove the old test's stronger assumption fails without a protocol regression:

1. Write and flush real READY bytes, then hold that same WriteAsync until its original stop token cancels. RunAsync has not returned from Send and cannot enter command Read. Close can settle cleanly before the old Ping.
2. Enter the actual cancellable Read and register the original held callback, then register a later LIFO callback on the same token. Hold that callback until the old Read disposes its held registration after stdin closes. The held callback is removed before entering; original cancellation and Read can again settle cleanly before Ping. This is a deliberately forced callback schedule, not an assertion about the historical OS callback order.

Both old-expectation executions fail at Peer.Ping with Broken pipe, actual exit0 and original IO settled. The original loop and settlement Tasks complete in both. r01 preserves red source, command, full log/TRX and both exact UnitTestResult XMLs.

## Minimal fix and preserved obligations

The original test waits on private WAIT_READ_ARMED before CLOSE, after the exact cancellable inner Read ValueTask has been created; admission's noncancellable Read cannot satisfy it. The normal held port retains its exact registration on cancellation until its own per-Read callbackStarted Task completes. Dispose then joins that same callback's return. A prior Read cannot satisfy this local fence, and noncancel paths do not wait for a callback. The two explicit counterexample modes retain the old registration lifecycle solely to verify the old assumption's failure.

The original 474-byte poll/assert/release/exit block is byte-for-byte unchanged (SHA256 `0c0a872d5feda319d690ff2bb4525d414fa1c3c0dc4109cf81b2d527de5d905c`). Its 5s poll, 6s peer/exit, existing 20ms poll and 40s fixture guard are unchanged. Original owner-retention, no-early-exit, release and actual exit assertions remain. Cleanup joins the original coordinator and actual child IO; body and cleanup exceptions are both retained. IOException remains a failure of the original fact. The two newly named passing counterexamples explicitly assert the old false expectation's IOException and phase evidence; they are not product red-to-green claims.

## Actual validation

| Run | Scope | Actual result |
| --- | --- | --- |
| r01-controlled-red | Two old-expectation controlled schedules | 0P / 2F / 0skip, exit1; both exact Peer.Ping Broken pipe |
| r02-targeted-green | Original fact plus two named counterexamples | 3P / 0F / 0skip, exit0 |
| r03-installation-regression | All Installation cases sharing the fixture | 163P / 0F / 0skip, exit0; previous161 multiset retained +2 |

Each run used Release/no-restore/nodeReuse:false/UseSharedCompilation=false through the approved pinned session SDK. Each run's 812 inputs were unchanged during execution; 810 base inputs are unchanged and only the two test/fixture files differ. Full commands, logs, TRX, exact run source snapshots, source and 330-entry DLL catalogs are archived. Runs overlap and are not added together. Production code is unchanged; Provider/Codec/App were not needlessly rerun in this diagnosis.

Independent read-only review ACK covers both mechanisms, final source, exact original assertion block, r02/r03 original TRX/source snapshots and actual child/IO traces; reviewer did not test/build/write. It supports one fixed-commit full Core run, not a prediction of that result. At this checkpoint that full run has not yet executed.

P7-01 remains InProgress. Existing-instance authenticated transfer is deferred while closing the Core gate. Real Windows modern menu to WPF/file Worker, installation/source identity, OLE/COM/OCR, quality, 250/600ms, 600MiB and full P6/P7/106/Beta remain unverified. Default Off, PackageHold, production reuse Off, odd WebP payload rejection and all source/password/integrity/original SHA/cancel/cleanup/actual exit/send fences remain. No new dependency/license/credential/security permission/cost, real model, user content/desktop, CI, main change, force push or deployment.

## Subsequent fixed-commit acceptance

After final read-only source and targeted-originals ACK, exactly one full Core run executed at clean `9877e5e3bcc9750bb0faf60ad040e9af7fca07f0`: **2093P/0F/0skip**, exit0, started 2026-10-21T04:28:10.322934Z, 94.890 seconds total. The previous2091 case-name multiset is fully retained plus the two explicit counterexamples. All812 input hashes are identical before/after; HEAD remained fixed and dirtyAfter was empty. Release DLL inventories each contain330 entries with45 normal-build changes, separately recorded; not every inventory DLL is asserted to have executed. Generated AssemblyInfo for Core, tests and fixture binds the fixed SHA. No full-suite retries or Codec/Provider/App reruns. The prior failure and historical uncertainty remain as reported above. The following evidence commit changes only reports/evidence, not source/SDK/locks. Full originals are indexed in [fixed acceptance](../test-evidence/2026-10-21-cancellation-fixed-signoff.json).
