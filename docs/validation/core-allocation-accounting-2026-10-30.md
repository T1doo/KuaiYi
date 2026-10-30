# Core accounting continuation — preparation only

Baseline: `6e986874913f0bae15d666b9375993182f0b6820`. No Core test, namespace setup retry, Windows action or CI was started. The original uid-map mount remains readonly by a fresh `statvfs` check; the prior bwrap setup refusal has no materially changed runtime prerequisite.

## Actual correction

The previous `size()` returned only regular-file logical length/count. A retained synthetic comparison runs that exact Git baseline function and the new inventory over the same directory: one byte in one file, two directories including the root, one FIFO and one Unix socket. The old result was `(1,1)`; the new result reports all five entries, each type separately and 4,096 allocated bytes on this filesystem. This demonstrates an accounting omission, not an actual Core failure.

The runner now counts both fixture and controller-output domains, including root directories, FIFOs and sockets; records observed `st_blocks*512` independently from logical lengths; and checks total entries and allocated bytes as well as the existing logical limits. Hardlink names are conservatively charged separately. Vanished/unreadable entries fail accounting instead of being skipped. Settlement retains a full fixture inventory; unavailable accounting stays null and preserves the original error. No product/test assertion or cleanup/retention policy changed.

Three new checks cover mixed entry types, sparse logical versus allocated storage, and a linked directory. Three existing directly affected checks cover missing-root settlement and the two closed execution gates. The six targeted checks passed in 0.003 seconds; source compilation via `py_compile` passed. This is six infrastructure checks, **zero Core executions**. Raw comparison, targeted output and protected-hash receipts remain local under `artifacts/2026-10-30-core-allocation-accounting/`. An initial check run failed because the new accounting API did not yet exist; no claim of an old Core red/green is made.

## Formula boundaries that remain open

The reviewed product content term remains 27,946,985 bytes. The parent-captured TRX and stdout each have a strict 1 MiB retained-prefix cap. Receipt serialization has a 1 MiB content cap. Those terms alone do not bound the entire case: controller configuration/receipts (including the final receipt written after the last monitor), the fresh parent directory outside the fixture, directory/index allocation, socket/FIFO allocation, intermediate control-record coexistence and maximum pathname count still need a complete source/lifecycle formula. Observed `st_blocks` excludes some filesystem-wide metadata and open-unlinked data; it is not an upper bound on every filesystem cost.

The inventory is a non-atomic observation, not a hard quota or security boundary. It does not resolve concurrent replacement/growth or establish a peak by polling. The readonly-root sandbox and independently reviewed static admission proof remain necessary. Candidate 128 MiB / 416 entries stays **unapproved**, `RESOURCE_PROOF_COMPLETE=False`; stronger observations must never be used to turn that flag on. There is no force flag, alternate execution route or isolation reduction.

The minimum missing runtime condition is platform-supported operation of the **original** readonly-root/single-write-domain isolation channel. The task cannot restore its readonly uid-map mount without a host/runtime change outside the permitted security scope. Extra disk bytes alone cannot fix this. No new privilege, quota, container route, host configuration or installation was attempted or requested by the runner.

## Preservation and budget

All 5,894 protected files were read and hashed against their existing manifests: 918,676,127 bytes, zero mismatches. None was deleted, compressed or moved. Current workspace free space before these small receipts was 1,662,943,232 bytes; physical free space is not the same as the frozen aggregate headroom of 423,501,153 bytes. The protected originals remain outside this candidate's write set.

The original 216.468 seconds plus previous conservative preparation charge of 60 seconds remain charged. The new small synthetic execution is conservatively charged another one second, leaving at most **622.532 seconds** of the original 900. Readonly hashing is not a Core execution. Historical qualification remains 28/51 on the old DLL; 23 remain unverified and the current test DLL still has zero Core runs. Main, publication, password/source/integrity/cancellation gates and 250/600 ms are unchanged.

## Independent review

The existing independent readonly reviewer checked all four changed paths plus the local comparison, targeted log and protected-hash receipt. Limited ACK covers preparation only, with no new P1/P2 blocking this slice. Execution remains NoGo: the reviewer explicitly retained the parent-directory/final-receipt terms, complete lifecycle allocation/count formula, open-unlinked/concurrent peaks and filesystem-global metadata as future admission gaps. No Core execution or 128 MiB approval is inferred.
