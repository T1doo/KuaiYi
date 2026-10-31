# Runtime mapping diagnosis and accounting race correction

Baseline `4952e3d14ec660579b9692d668ff4dc6af0239d8`. Ordinary preparation branch only; no Core, Windows, CI, namespace setup attempt, privilege expansion or alternate execution channel this slice.

## Verified environment cause

The selected original environment resumed successfully. Current platform status is running/connected with enforced network policy and specification revision84. Its capabilities list is empty; that is **no advertised nested-isolation capability**, not proof that every possible platform implementation is unsupported.

Fresh `/proc/self/mountinfo` selects the enclosing `/proc` mount: `ro,nosuid,nodev,noexec,relatime`, filesystem type `proc`; its superblock options are `rw`. `statvfs` independently reports ST_RDONLY. UID mapping is beneath that readonly mount. This accounts for the earlier `setting up uid map: Read-only file system`: writable superblock metadata does not override the readonly mount visible to the process. The current process ancestry also includes the platform's outer `codex-linux-san` and `bwrap`. We have **not** inferred the exact platform configuration rule that set this mount or attempted a mapping write to reconfirm the error.

`inspect` now reports only existing mount metadata: component-aware longest mount match, standard escaped-path decoding, mount options separate from superblock options, and `SandboxSetupAttempted=false`. Missing/malformed metadata is unknown/error. This diagnostic cannot declare nested isolation available, trigger it, enable the resource gate or grant permission. The existing execution uid-map prerequisite and `RESOURCE_PROOF_COMPLETE=False` are unchanged.

## Implemented independent correction

Previous inventory used `lstat(path)` then `scandir(path)`. A deterministic test replaces an empty owned child directory with a link to another owned synthetic directory at that boundary. The original code accepted and counted the outside marker:17 logical bytes, one outside-directory scan. The corrected code opens directories with `O_DIRECTORY|O_NOFOLLOW`, binds to their device/inode, traverses child names relative to that descriptor, and checks the original name after traversal. It refuses the replacement with zero outside-directory scan requests. The original scan failure and a simultaneous directory-close failure are both retained in primary-first order.

This fixes link traversal during accounting. It is **not** an atomic tree snapshot, a quota, a peak allocation proof, a protection against all concurrent file-size changes, or a substitute for the runtime sandbox. Product source, assertions, installation/source/password/integrity gates, 250/600ms and retained-fixture policy are untouched.

Actual local checks: the new replacement regression first failed1/1 in0.001s; after correction, five directly related checks passed in0.003s. Three mount-parser checks passed in0.000s; the independent directory-close failure check passed in0.001s. Total9 relevant infrastructure passes, **zero Core runs**. No old full suite was rerun. Local evidence is `artifacts/2026-10-31-runtime-diagnosis/`: `directory-race-comparison.json` is a real side-by-side execution of the exact Git baseline/current inventory over owned synthetic files; `runtime-mount.json` is the actual current readonly observation; `observed-test-results.json` is explicitly a transcription of tool output, not raw TRX or a replacement test result.

## Minimum platform restoration handoff

1. Retain this existing workspace and all protected originals; preserve current source/manifest hashes. No replacement workspace or additional writer has been created.
2. The platform owner must restore **the original supported isolated execution channel** so its UID mapping setup can complete while retaining the readonly-root/one-fixture-write-domain contract. This requires a runtime-supported mapping setup order/capability; task code cannot make an outer readonly proc mount writable. The precise host implementation is not exposed by environment_status. This is a platform prerequisite report, not a request to remount proc, elevate the task, grant new security permissions or select another execution route.
3. After a real platform change, inspect metadata and capability readiness first. A restart at the same current configuration was already observed to leave `/proc` readonly. Do not repeat bwrap on unchanged evidence. Any actual diagnostic retry requires materially changed relevant evidence and must remain within the existing isolation/authorization contract.
4. Runtime restoration alone does not admit Core: complete the reviewed lifetime count/allocation formula (including parent directory, final receipt, open-unlinked/concurrent peaks and metadata) before changing the currently false source gate.128MiB/416 remains unapproved. Only then can the original ordinal29 and remaining fixed list proceed under the original cumulative budget.

Extra disk alone cannot repair UID mapping. There is no justified numeric extra-space request that substitutes for these prerequisites, and no local source change can restore an outer mount under the present security boundary.

## Preservation and budget

The5,894 protected files were rehashed against the existing manifests:918,676,127 bytes, zero mismatches. No deletion, compression, movement or unknown-process/lock action occurred. Initial free workspace space was1,662,607,360 bytes; `/tmp` was9,441,292,288 bytes. Those are current capacity observations, not a new allowance. The frozen aggregate content headroom stays423,501,153 bytes.

The prior budget ceiling622.532s is reduced conservatively by one more second for this small synthetic execution, to **621.532s**. No clock reset or extension. Historical28/51 directed passes remain on old test bytes,23 still unverified; current test DLL has zero Core runs. Main/Release remain unchanged and blocked under the current task limits.
