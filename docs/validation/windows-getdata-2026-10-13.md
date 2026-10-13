# Bounded Windows GetData validation

Status: **Pass for the hosted public-source OLE subgate. P0 overall remains Blocked.**

Verified source: `220060f813a3640487d593920a1b1824aa779a00`.
Reviewed freeze `152cfb65a31441892006ec94c0d299c7c07b4639` has the same tree `6436c5d412551372ec5bf90222109c990a21dd6a` and sole parent `6b83b914a9b53fb23c6eddc7e1565ff6f650f776`.

[Windows run 38347142627](https://github.com/T1doo/KuaiYi/actions/runs/38347142627), [job 115131066822](https://github.com/T1doo/KuaiYi/actions/runs/38347142627/job/115131066822): attempt 1, completed/success, 2026-10-13 12:35:03–12:36:45 Asia/Shanghai (1m42). One standard windows-2022 job, 15-minute limit, contents read permission. ModelCalls: 0.

| Validated gate | Result |
| --- | --- |
| Native marshaling prerequisite | Pass |
| Actual matching QueryEnd with COPY and production Drop | Pass |
| Actual source GetData deliberately blocked | Pass |
| Presentation heartbeat during the original blocked call | Pass |
| Synthetic pointer drag of the actual presentation | Pass |
| Cancellation, confirmed exit and replacement | Pass |
| Close while original GetData is live | Pass |
| Every original receiver Run/control/read and owned finalization | Pass |
| Final receipt and process/output-drain settlement | Pass |

Both cancel-recover and close scenarios completed. All five receiver attempts remain in the receipt: three became Ready, and two were explicitly revoked during the owned handshake before registration. The latter require first-revoke-before-failure evidence, fresh cancellation, no deadline/control/register start, original Run cancellation, actual process exit and complete cleanup. Their UNKNOWN registrar Task state means no registrar task was started; it is not used as proof of completed registration. All Ready receivers have unique instance/PID/registrar bindings and completed register/revoke work. Both source processes exited 0 with their original metadata/error/close tasks complete. Application and outer runner exited 0; both outer output drains completed.

Local direct contracts: **120/120**. Script regressions: Linux **49/70/43**, Windows **48/70/43**; the entry script has one Linux-only Windows-required case. OleTransfer build and actual Windows/App source typechecks: zero warnings/errors. The Windows job also executed the real Windows build/native path. Portable fixtures and parser tests remain explicitly synthetic and are separate from native observations.

The public source identity was corrected only for the explicitly configured synthetic fault-source build; real WPF, IDataObject and DoDragDrop remain in use. Production own-process, KuaiYi-name, terminal, blacklist and focus checks are preserved. The earlier run 38344973214 remains Fail: it observed the core scenarios but its receipt was rejected. Its result has not been retroactively promoted.

Remaining acceptance: physical E06 **NotRun**, Windows 11 **NotRun**, browser/PDF/Markdown real selection compatibility **not established by this job**, multi-monitor/DPI and installation/uninstallation **not established by this job**. This result does not pass the complete P0 exit gate or the overall product acceptance.

Only the fixed whitelisted summary leaves the runner. Original logs remain ignored local evidence; no private output, credentials, real model calls or public release were introduced.
