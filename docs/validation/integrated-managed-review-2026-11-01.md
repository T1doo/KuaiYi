# Integrated managed review build — 2026-11-01

Status: **Pass for source-bound managed review only**; Windows execution/production installation **NotRun/Blocked**. This is a local preservation candidate, not a new Release or replacement for the old self-contained preview.

## Exact inputs and actual execution

Build source commit `58b9bf2d59531cc86b935a87905563fb722e2d7a`, descendant of product fixes through `4ce04f61ff51b17f2ecf8cec65bc3a1593092ae8`. SDK `10.0.402`, runtime/reference packs `10.0.13`, assembly version `1.0.0.0`; every one of 42 managed assembly copies has informational version ending with this exact source SHA. This validation document is a later result record; it was not compiled into that candidate.

Original local evidence root: `artifacts/2026-11-01-integrated-managed-review`. One invocation of `python3 -B scripts/build-managed-review.py artifacts/2026-11-01-integrated-managed-review` performed metadata-tool compilation, eight locked restores and eight **full project-reference** Release builds, metadata-only PE inspection and two original NuGet signature/content-hash verifications. Command interval: **18:11:26.079767–18:13:09.669741 UTC**, 2026-11-01. All 20 command receipts have exit0 and `LeaderJoined_GroupEmpty`; eight role build logs report 0 warnings/0 errors. No product executable or test suite was run. Earlier directed regressions remain their original separate evidence; they are not new passes in this round.

Roles: App, NativeHost, CaptureBroker, ClipboardHost, CaptureHost, OleReceiver, ImageWorker, Launcher. Every runtimeconfig is framework-dependent and requests Core `10.0.13`; App/CaptureBroker/ClipboardHost/CaptureHost/OleReceiver also request WindowsDesktop `10.0.13`. No runtime implementation is bundled, no native product DLL was built, and actual runtime selection/roll-forward remains unverified.

454 tracked input files match before/after manifests byte-for-byte; both manifest SHA256 values are `450934d91c3e37e3c34cc8f6999e51a6b945d8411c356cf0664880c1540b0229`. All eight Core DLL copies have SHA256 `d0ac41b40240a12419ce95c4eb3ca08b2e865f168621acd86fb1ddd8cb0c1f1b`. PEReader checks assembly metadata and embedded prompt/fixed vision sample/bootstrap bytes without loading product assemblies. Dependency lists admit project version1.0.0 and pinned Skia4.153.1 only; Skia outputs match their original pinned archives and original signed content hashes. Tool/helper/document bytes are separately fixed to the build commit.

## Local artifact and content checks

- ZIP: `KuaiYi-managed-review-58b9bf2d5953-framework-dependent.zip`.
- Size: **11,553,741 bytes**, 109 members.
- SHA256: `ac1c36103c9d9176d0825f7255977b08c22d1e0917884d532c0b6c96d20f2b95`.
- Separate role closures, two committed limitation/gap documents and `review-manifest.json`; no adjacent-role runnable product layout. ZIP CRC, exact membership, lengths and every recorded file hash read back successfully.
- Fresh build-output allowlist excludes PDBs, settings, logs, captures, TRX and user input directories. Private-key/GitHub-token/project-key signature scan found no hits. Pattern scanning is only supplementary: provenance is the fixed tracked inputs, clean source snapshots, signed dependency archive checks and exact output/member inventory; it is not a universal secret detector. The ZIP and machine receipts remain local and are not committed/uploaded.
- The old 130,847,087-byte preview ZIP remains unchanged, SHA256 `fd4b8a0e970233c0854bf864c34d66e7f67a8dbc140d3f366905410985e546cd`.

## Resources and protected originals

Original self-contained publisher requires 7GiB free; it was **not invoked**, and its gate was not reduced. This different ordinary framework-dependent build uses fresh `--artifacts-path`, role output/temp/HTTP/plugin/scratch directories, existing package cache and original SDK trust environment. Admission: 256MiB observed growth budget plus 1GiB free floor; 600-second execution deadline with original child settlement, strict1MiB command logs and retained failure receipts. These are monitored bounds, not a filesystem quota or a static peak proof for the blocked Core tests.

Initial free: 1,647,980,544B. Maximum observed workspace growth: 151,666,688B; maximum observed staging bytes: 150,077,348B. After the later integrity receipts, free space was 1,496,199,168B. ZIP raw-size/header allowance and final receipt reserve were admitted before archive creation. No cleanup/deletion was needed. The protected14 fixture manifests were rehashed: **5,894 files / 918,676,127B, zero mismatches**, no moved/compressed/deleted originals. Unknown directories/locks/processes were untouched.

## Remaining product gates

The [functional gap inventory](../release/FUNCTIONAL-GAPS-2026-11-01.md) covers first configuration, settings search, selected-result copy, text cancel/clear/disposal, three memory versions and image regeneration separately. Managed integration is now evidenced; whole-product delivery is not.

Normal production image reuse remains disabled: retained-image authority/lifetime and fresh OCR/vision approvals are still a real implementation/source-contract gap, not merely an unrun UI test. Trusted production issuer/pins, four product native DLLs, G/B/installer provenance and Windows platform lifecycle/capture/clipboard/identity/visual evidence remain incomplete. The 23 blocked Core cases remain unrun; their original resource proof/readonly-root isolation requirements and cumulative budget (at most621.532s) are unchanged. No refused UID-map or old forbidden log/artifact action was retried. No Windows/CI/user-PC/model/capture/registry action, main update or Release occurred.

Independent readonly review inspected the current tool chain before execution and then the original logs/receipts, actual staging/ZIP bytes, manifest/source bindings, metadata/resources/dependencies and final report. Final limited ACK: no P1/P2 within this managed-review scope. It did not run the product/tests or qualify Windows/Release. The normal preparation branch matches none of the13 configured push workflow branches; no CI trigger is part of this change.
