# Command log budget and independent settlement

Status: Pass for this local supervisor slice; complete Core qualification remains Blocked.

Baseline: `422eba323df6d4180bea1b9ff0988958f4ac995f`, preparation branch. No C# product code, pins, production admission gates, Core assertions or 250/600 ms contracts changed.

## Defect and change

The original supervisor redirected stdout/stderr directly to a file and inspected the working directory only while its leader was running. A local synthetic command reproduced a real gap: with `LIMIT=1024`, the child wrote 65,536 bytes and exited 0 before the next poll. The oversized log was accepted with `LeaderJoined_GroupEmpty`.

The same supervisor now reads merged binary stdout/stderr through a selector. It writes at most the reserved byte count, including partial writes, and refuses on the first excess byte. The prefix and `logTruncated` receipt indicate failure, never a complete log. Callers can supply a smaller positive integer `log_limit_bytes`; the default remains the original aggregate LIMIT. This bounds only the captured stream. Arbitrary child-created files and TRX still need the separate whole-case resource proof.

The deadline includes draining. An inherited pipe is subject to the original five-second child-settlement allowance. Failure terminates only the newly created process group. Exit/group observation and pipe closure settle independently. Review found a pipe-close error could obscure the first failure and skip evidence; the correction records Unconfirmed/Unknown and preserves the original error first. Group-observation errors also remain Unknown.

## Actual validation

- New API tests initially failed on the missing reservation argument. The separate 65,536/1,024-byte reproducer establishes the old behavioral defect.
- First implementation: 19 tests, one failure because an older test expected its log to exceed the budget. That assertion was tightened to the exact retained 100-byte prefix; no Core assertion changed.
- Initial slice: 21 passed. Independent review then found the pipe-close issue.
- Final suite: **22 passed, 0 failed**, 6.031 seconds, Python 3.12.14. Covers fast overflow, exact-bound binary stdout/stderr with exit 7, invalid reservation before launch, empty output, timeout, inherited pipe, non-log output monitoring and closure errors with/without prior overflow.
- Command: `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest scripts.test_managed_publish`.
- Raw local logs: `artifacts/2026-10-29-supervisor-log-bound/unittest.log` and `unittest-r02.log`. Independent reviewer read the final log without rerunning.

Reviewer-checked SHA-256: publisher `2100f2c983e7b1a7c524aa009abd344decd3d59b7937b647e012884c6cfc4c7a`; tests `9eda793ab5c155f839e39d4694d5ebc30fd3f813cc55e83ccd06c40e66428c29`.

## Preservation and remaining gates

Initial current workspace free space was 1,672,581,120 bytes. The restarted `/tmp` mount was empty: presence of the old `/tmp` failure fixture is **not reverified**. All 5,894 files in the fourteen protected workspace fixtures matched their saved SHA-256 inventories, total 918,676,127 bytes. None were deleted, compressed or moved. The 449 product inputs match the prior publish snapshot. Core test DLL SHA-256 remains `174de8f6e8c23253f2004dd23034816f6fae97894b5d52148106428885bcb35e`.

No remaining Core case was rerun. Historical directed qualification remains **28/51 passed, 23 unverified**, 216.468 seconds consumed and at most 683.532 seconds remaining in the original 900-second budget. Protected content consumes 918,676,127 of the frozen 1,342,177,280-byte aggregate allowance. Remaining allowance 423,501,153 bytes is not proof a whole case fits. Control-record coexistence, VSTest/TRX and child temporary-file bounds remain unresolved; the default six-GiB limit cannot substitute for that smaller allowance. No budget reset or expansion occurred. No new CI, Windows, model, capture, installation, main or Release action was performed.
