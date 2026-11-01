# Synthetic G/B maintenance chain — 2026-11-01

Base: `6e11aa0b8899ab5c3093eadd160bda1b8b91384d`, development branch `dev/ui-polish-visual-r03-preparation`. No production source or authority gate changed. This closes the previously unexecuted **complete synthetic filesystem chain** of the original maintenance implementation; it does not qualify runnable Windows G/B, actual registry, installation or whole-product uninstall.

## Actual chain and independent reads

The explicit opt-in `InstallationMaintenanceFullChainTests.FixedSourcesRepairKnownFailureRollbackRetireAndReopenActualFiles` ran once, Passed. It uses the unchanged original `MaterializedPayloadSource` and `BootstrapSource` suppliers. Actual managed role builds were refreshed from the same base; eight role builds, including the WPF cross-build, exited0 with0 warnings/errors. The original G supplier's absent native/runtime/third-party assets are **neutral512-byte PE test placeholders**, not runnable production binaries. B contains all189 official pinned runtime inputs,80453412B, verified individually before copying; no hashes or required files were dropped. Three Windows values are represented by a persistent, flushed file-backed registry surrogate, not OS registry calls. The native-host/startup ownership records are fixture-created inputs, not a production issuer.

In one newly created UUID temporary directory, the test actually:

1. Validates immutable G/B sources, installs G with the original store and deploys B with the original bootstrap deployment. Creates synthetic ownership records and the three surrogate values.
2. Damages its own installed App DLL and removes its own extension HTML. Production repair validates pinned sources, stages G/B, withdraws the original seven references, retains the damaged originals, replaces the canonical directories and republishes the original bindings. A **new existing-only reader** reopens and verifies active G and bootstrap; the DLL and HTML match source bytes. Original damaged bytes remain in recovery.
3. Starts a separate original uninstall transaction with a known injected `AfterEffect` exception at step4, before file mutation. Original reverse compensation restores the references. A **new reader** verifies the same G/B and exact original reference bytes. Status is Refused, cleanup confirmed, prefix/attempt zero, file mutation false; the expected IOException is retained. This is not an actual Move-UNKNOWN case or proof that UNKNOWN may compensate.
4. Starts a fresh explicit uninstall. All seven references are withdrawn and current G/B are actually moved to exclusive retired directories. A **new reader** observes no active generation; original installed readers validate retired G/B bytes, using the original canonical path binding where required. Synthetic user settings and a neighboring product sample are unchanged. Pending is absent; two maintenance-finished records and one compensated withdrawal-aborted record remain.

`PayloadRepaired` and `FilesRetired` are component outcomes. There is no permanent purge, full-uninstall success or enabled production source pin. All sample directories and bytes are retained on both success and failure, not deleted to make space for a repeat.

## Actual result and byte binding

Execution binding SHA256: `a45423595595e181b40ec6d3978b8cdf01fbbc6b3afcf9ffa52a4d57b40937b4` (168 input rows). Original process group46080, start `2026-11-01T21:35:30.970263Z`, end `21:35:46.303455Z`, wall15.333192s, exit0, `LeaderJoined_GroupEmpty`, stdout closed,447B non-truncated log. TRX reports one exact test Passed,12.7426376s. A prior budget-only Fact passed; an initial CS1061 test-build error was corrected before execution and its log retained. No failed build was followed by stale-binary testing.

After the run, a default-test safety improvement excludes the large class and its two suppliers unless `KuaiYiMaintenanceFullChain=true`. That opt-in selects `bin/MaintenanceFullChain/net10.0`; the dedicated runner supplies the property explicitly. The **entire106-file actual tested closure,11605813B**, was preserved there before rebuilding. The explicit-property build was then compared against every preserved length/hash: all106 identical. The large case was **not rerun**. Default build and actual discovery exclude both FullChain facts. Its original maintenance filter ran45/45 Passed, exit0/group empty; these are a regression round, not45 new cases. Python runner guard tests4/4 passed without CLR/product effects.

The runner binds the csproj, actual loaded testhost closure, actual Core and Launcher Core, fixed role inputs and supplier sources. Consumption identity normalizes DLL names relative to the output root. Changing default to dedicated output cannot evade the already-consumed marker. Missing/damaged consumed binding fails closed. The actual existing marker was checked read-only and correctly refused replay. A source/body change needs fresh review and resources; this report grants no new execution.

Original evidence stays locally in excluded `artifacts/2026-11-01-maintenance-full-chain/` and `artifacts/2026-11-01-maintenance-full-chain-finish/`; no raw archive, testhost or sample is committed/uploaded. Useful original hashes:

| Original | Bytes | SHA256 |
| --- | ---: | --- |
| Full-chain TRX |3422|`dddd10eeccc1c3f20c4ae32611910e794c93578d795a5f737df0482b90f0bb8a`|
| Original execution receipt |1330|`7882c99299d54b3f1308133a3436c834a47e065477b6bf3103bd5f80172cd4d6`|
| Nine-phase chain inventory |322613|`426e53e94561429854e273f0dff48406189619342b848ade0314d02807edd831`|
| Default maintenance45 TRX |66245|`d9ea40498ec5316251c735c14a66b1f8b15fefb9022079d9ebe08fe1dbe948d6`|

Independent readonly review directly checked TRX, receipt, phase records, retired G110 files and two B195-file copies against their recorded hashes, pending absence, original archives and preserved synthetic data. This is direct local evidence review, not an author-only test summary.

## Resource contract and preservation

This newly authorized synthetic sample is separate from the frozen23-Core diagnostic ledger. No historical900s budget was reset/transferred. Input-derived conservative **logical** coexistence calculation is297200760B: source G upper5546191B, B upper83034969B, three source/original-or-recovery/stage-or-canonical copies plus4MiB derived metadata269937784B, three bounded transaction records18874368B, tooling/evidence8388608B. This formula is not an OS allocation quota.

The dedicated observer limits logical/allocated/global growth to352MiB, free floor1GiB and original process-group time180s with1MiB log cap. It is sampled supervision, not a hard filesystem quota. Actual observed peaks: logical259150224B, allocated261750784B, global growth261459968B; minimum free1215414272B. Full outputs are retained. Default integration builds/tests used a separate64MiB/1GiB/180s observer and joined their original groups. No disk expansion, unknown cleanup, protected-file reuse, isolation/UID-map retry or downloaded input was needed.

At `2026-11-01T21:47:38.397039Z`, all5894 protected files /918676127B and exact file sets matched the original14 inventories. Free disk was1203150848B. The original7GiB self-contained packaging gate remains unmet and unchanged.

## Remaining real implementation and execution requirements

- Shell/identity maintenance still lacks original install-time registration/package ownership and the pinned signed package/issuer. A name/path/manifest match cannot authorize removal; production removal actuators are not complete. Actual registry/package changes would need a separately bounded Windows execution scope with those authentic inputs.
- Existing-installation shutdown lacks the original live controller in a fresh maintenance CLI. Existing coordinator/owned children already settle their own session; PID discovery/adoption is not a replacement. Any future stop must be limited to a proven original owned session, never unknown processes.
- Permanent purge of recovery/retired/old versions and caches remains unimplemented. This slice only moves its own recoverable sample. A future deletion test must name an exclusively created disposable sample and independent ownership proof; it cannot authorize real installation/user files.
- Optional user settings/key cleanup remains unimplemented in maintenance orchestration. Existing SecretStore revocation is not whole-profile deletion authority. A future synthetic test may use only public dummy settings/keys; no user data/key deletion ran here.
- Real Windows G/B qualification, native source/issuer completeness, actual registry/Shell/identity/clipboard/UI lifecycle and the blocked23 Core tests remain unverified. Ordinary production pins stay Unavailable. No PC, model, capture, Windows window, CI, main or Release action occurred.

The current priority was the real filesystem chain and its failure recovery. No unused wrappers or guessed ownership scheme were added for the remaining authority-dependent gaps.
