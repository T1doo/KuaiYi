# Qualified startup settings removal — local B1

B1 is the removal half of the original Installer's same-session settings participant, not completed sign-in startup. It does not enable LaunchAtSignIn, supply source pins, implement a Windows Run adapter, or grant an ordinary Launcher restart a trusted context. Production pins remain Unavailable/null; public SettingsJson remains false-only. The production settings UI is not enabled for this internal path.

The original accepted deployment session creates the owner only from its live Installer/Operation/Scope references, qualified G/B, original controller admission, original launch and original child. The controller borrows that admission without reentering its serial or upgrade.lock. Startup removal attaches before canonical READY publication; the original stdio reader dispatches requests without awaiting settings IO. A request carries the exact launch/fingerprint and a fresh request ID. This bounded half allows one removal request per qualified session, with no second-request queue, replay or automatic fallback.

Settings reserve the controller before Settings.serial and secret-transaction.lock. A schema-3 settings intent and a separate controller intent precede external effects. A v3 intent is not handled by v1/v2 secret recovery, and disk records cannot reconstruct its authority. The v3 writer retains the original IO ledger before create/write/flush/close; partial or unknown close is not deleted. Removal treats the named startup value and ownership file as two independent effects. The owner file moves to this request's exact retained backup; abort restores the owner then the value. Registry write-after-effect can be classified against exact Before/After only after original close succeeds. Foreign reads/compare observations and unknown close stay sticky after external restoration.

Configuration already published is never reported as rolled back. Every original safety subscriber receives publication even if participant settlement fails. Known unchanged configuration permits same-owner abort, then settled-intent verification, then cleanup of provably unpublished secret references. Original save and cleanup exceptions survive together. The local v3 intent is deleted only after confirmed original settlement. Lost responses are not resent; original writes and flushes remain held and tracked through settlement. Unknown results retain the pending intent and execution owner.

B1 reuses A's seven-effect deployment and failed-READY compensation paths; it does not replace them with a current-only rollback. A's read-only uninstall plan remains a plan. Enabling startup, subsequent on/off transactions, settings UI availability, the ordinary Launcher trust path, real Run registration/unregistration and full uninstall execution remain outside B1.

## Actual validation boundaries

All inputs are synthetic and local. Original-stdio cases run the existing portable Linux SettingsStoreFixture child with the production control/coordinator code, a synthetic named-value registry and test-owned file roots. G is a structural fixture. The B runtime fixture checks the existing locked 189-file cache; neither is an accepted runnable Windows product payload. No Windows native/UI/sign-in/registry, model, network or CI test is claimed.

Preserved preliminary rounds under artifacts/2026-10-24-startup-B:

- r01: Core cross-build, 0 warnings/errors.
- r02: fixture compilation failed (captured startupSave before initialization); no tests ran. Declaration corrected.
- r03: 0P/7F; all seven were refused by the existing mixed-build-revision gate before the new path. Old role outputs were not accepted as a qualified fixture.
- r04: eight role cross-builds, each exit 0 and 0 warnings/errors.
- r05: 12P/0F, preliminary new-chain cases, exit 0. Not added to later results.
- r06: the foreign-owner counterexample was blocked by mismatched bootstrap/generation Core outputs; not a red proof.
- r07: after exact role rebuild, the same foreign-owner test gave 0P/1F at Assert.Throws: no exception was thrown. Sticky qualification was then fixed; the test's assertion was retained.
- r08: eight role builds passed; test compilation failed because Assert.Single returned a Task whose value was discarded implicitly (CS4014). The held original is now explicitly retained; no tests ran.
- r09: the error-preservation test failed during fixture directory setup; not a red proof.
- r10: corrected fixture, 0P/1F at the intended assertion: expected AggregateException, actual cleanup IOException replaced the primary disk error. Original errors and settlement order were then fixed.

r11 completed with 232P/5F/0skip, 237 cases, exit 1 in 314.126 seconds. All 16 new B1 cases passed, including both final counterexamples; all 33 A startup cases passed. The five failures were existing upgrade/handoff control cases. The new fixture had unconditionally attached a participant whose original production guard correctly refuses UpgradeRecord-bearing sessions, preventing their intended READY path. The fixture now attaches only for ordinary control sessions; the production refusal remains unchanged.

r12 changed only that fixture source relative to r11 and ran exactly the five failed identities plus three regression controls (two normal startup-save theory cases and one normal controlled-start case): 8P/0F/0skip, exit 0. No old assertions were removed or weakened. The reconstructed pre-fix fixture bytes match r11's actual source-map hash e44e513b9dd72bfcf032fd0466c95baeee73c816d4fda38628404acc24eaf078. Core and test DLL bytes are unchanged between the rounds; only the fixture DLL changed. The two rounds are separate evidence, not a claim of a fresh 237P run on the final fixture or a 240-case total.

The final production Core tree has eight cross-builds and the test project build at 0 warnings/errors in r11. Core DLL SHA256 is 4d20dd405c595625c346a12032785c057ced85accd945634bfa28503da5360ca; test DLL SHA256 is fb5c8ebc85f06b585b0bc562799fd2cc9435194f550d166986f30a1e8099f3a1; final fixture DLL SHA256 is 8f67845522f588ad6d6b0040fcf73a472ccea49abe50555c1191cb0e0263192d. r11's before/after source map covers 713 C#/project/props inputs and is identical; r12's source maps are also identical. This is a bounded source projection, not a complete before-build inventory of every resource/runtime input. Build HEAD was 3b9240cbbad27820259087c70a01e0c97cab1d3e with the candidate uncommitted; candidate file bytes are subsequently bound to Git blobs. Assembly revision alone is not proof of those dirty source bytes.

Builds use the existing .NET 10.0.402 SDK, --no-restore, NuGetAudit=false and EnableWindowsTargeting=true. r11 records actual command UTC start/finish and elapsed time; r12 records its exact command/exit/elapsed plus original TRX runner Times, not independently recorded command wall timestamps. No full Core/Provider/Codec or platform acceptance is claimed. Preliminary passing counts are not added.

/tmp remains full. One normal-permission interactive command session is retained after intermittent pre-command ENOSPC failures; no old temporary roots, locks or processes were removed or adopted. Unknown test roots and the deliberate partial-v3/close-failure fixture are retained, not declared cleaned. The Windows diagnostic refusal and all previous actual UI/DPI/performance limitations remain.
