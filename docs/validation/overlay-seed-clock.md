# SeedOpen timing and independent failed-run state observation

Base: `635898c0fe3ec27b5d28cfa6ae07be1b8777a1e0`, `dev/p7-fixed-signoff`. This is a local correction and evidence handoff; no new CI was dispatched.

## Original Windows result, preserved

Run `39960404039`, job `120436667676`, delivery source `1d28dde842f7fcad1e5410476eafe47bdcc383a9`, fixed parent `d52199a7c3cce69a8c550166f2c40cc170e5382d`:

- Consumer-materialized Library `libfile_be3f096ed3f88191b6cbb2ba1db2c132` v0: 240217 bytes, SHA256 `23a3d1c7122f71b9ce41e7104f304db94aade9666919a34fab9d61f1a64b3739`. All 27 originals / 42 unique chunks match raw and gzip lengths and SHA256, including the summary inventory. The 430 checkout hashes match the source Git objects as 4 exact blobs and 426 Windows CRLF checkouts; they are not all byte-identical LF blobs.
- DesktopOnly preflight completed in 5.313 seconds, exit 0. ProductionOverlayStages then failed: original process exit 3, original wait and both EOF/pipe/log closures confirmed, no rescue and no outer deadline expiry.
- Inner driver timed out after 45286.1814 ms in SeedOpen; issuance completed, but no changed intermediate Opening sample was observed. Raw callbacks: 2790, no PNG or raster reservation. Frames 1–4: Closing, 48×48. Frame 5: Opening/NeedsFrame, still 48×48, at 445.9412 ms. Frame 6: Expanded 420×113 at 465.5328 ms, Applying=true. Frame 7: stable Expanded at 469.3459 ms. The approximately 409 ms sampling gap is real; it is not a measured Tick delta or evidence of DWM fluidity.
- Inner CloseConfirmed/CleanupConfirmed=true records its original close and owned-state deletion. The old outer `StateDirectoryAbsenceConfirmed=false` was **not observed**: exit 3 threw before `_verify_result` could inspect the directory. It does not prove residue. Overall result remains failed. The first run `39957744943` had a different, still-historical unknown process-tree cleanup outcome; it is not rewritten using this run's confirmed exit.

## Demonstrated defect and narrow correction

Begin starts the existing Rendering subscription while waiting for text. The first text flush can measure content for a long time before `OverlayFrames.OpenMeasured` actually begins Opening. Previously StartFrames returned immediately if Rendering was already active, leaving lastRender before that measurement. The next Tick could consume preparation time and immediately reach Expanded. This mechanism is demonstrated locally, not asserted as the unique historical Windows cause: the old run has no direct Tick delta.

`OverlayFrames.Begin` now notifies one internal init-only clock callback after measurement, current-source recheck and geometry validation. The sole production ShellWindow construction binds it to `OverlayFrameClock.Begin`. RenderOverlay uses that same monotonic clock for actual Tick deltas. A newly begun phase keeps its start time even if subscription resumes later. Active Grow does not restart it; resuming idle growth excludes idle time. Reverse operations retain the current displayed edges. Deltas are not clamped, real post-Begin stalls still consume time, and 220/180/70 ms contracts and reduced motion are unchanged.

The existing native sampler records the current animation epoch/start plus the last actual applied Tick's sequence, epoch, from/now/delta and before/after states. A pre-Tick sample can distinguish the previous epoch's Tick. These observations do not interpolate frames or change eligibility, intermediate-state requirements, 45/60-second limits or evidence budgets.

The wrapper now independently checks the original output-directory identity and bounded state-prefix absence after the exact original exit and both pipe/log settlements, including exit 3. It distinguishes NotObserved / Absent / Present / Error; exit failure and observation error are retained separately. An Absent observation never promotes exit 3 to success. No state deletion, PID adoption, cleanup retry or historical receipt change was added.

## Actual local validation

- Red clock run: 6 cases, 3 passed / 3 failed. MeasuredOpen expected 20 ms but received 429 ms; reversal expected 20 ms but received 320 ms; the delayed-subscription case saw an unchanged epoch. The red tree included a compilable pure-clock and callback-property scaffold, with the old Begin behavior (no callback invocation); it is not an unmodified historical Windows binary. The identical six-case source passed after connecting Begin. Green related Provider filter: 18 passed / 0 failed / 0 skipped = those 6 clock cases plus 12 existing probe-evidence cases.
- Red wrapper run: 2 cases, one assertion failure for unobserved absent state and one missing-field error for the newly required observation status. The same test file then passed in the 17-case wrapper suite (15 existing + 2 new real Linux children). Linux subprocess tests are not Windows/WPF results.
- Existing Core geometry filter: 4 passed / 0 failed / 0 skipped, covering interruption/reversal, opacity continuity, growth target stability and reduced motion.
- GrowthHarness and its production App dependency: Debug Windows cross-build, 0 warnings / 0 errors. No package, license or SDK changes. Independent readonly review found no new definite P1/P2 in the correction.

Commands, full logs/TRX, red and green source snapshots and test/production DLL hashes are retained with the original failed Windows ZIP. Counts from different runs are not added together. No full Core/Provider suite or installation suite was repeated.

## Remaining boundary

No new Windows sampling, PNG, animation movie, DWM/FPS or physical-input evidence exists for this fix. Mixed system DPI, corners, 50 cycles and complete installation remain unaccepted. Source/password/integrity/cancellation/cleanup/isolation, 250/600 ms, default Off and sending gates are untouched; Installer pins stay Unavailable. The existing workflow and source gate remain frozen at the previous delivery, intentionally not dispatched or relaxed. A later authorized single retry needs a separately reviewed exact source-binding update before it can sample this fix.
