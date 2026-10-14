# Next SourceHead-only diagnostic: local preparation, not authorized to run

Delivery status correction: approved eight-file delivery is now remote commit `4bc4ee3ea471ed1dc51f83c8d04827ad02f92ed2`, tree `ac8a07e77e1d74b314b3091028ec6fab61ba3b61`. The public progress blob is `a4af1d635645269039224c5daaed1dc96dcf4804` (93926 bytes). Earlier notes describing only-local evidence and blocked uploads are historical preparation states; they no longer describe the delivered eight files. The original failed receipt remains unchanged. New preparations described here are local only, and have not been uploaded or pushed.

## Exact future scope

No direct authorization exists for a new Windows diagnostic run. Prior direct approvals covered the delivered eight files and explicitly excluded Windows reruns. The original attempt38443925339 is not to be rerun. Any future approved execution must use a newly reviewed delivery commit, with exactly one parent `4bc4ee3ea471ed1dc51f83c8d04827ad02f92ed2`, its expected-SHA development push, and one new standard windows-2022 job capped at10 minutes. Success or failure ends the attempt; there is no retry. Changing the workflow path will trigger that run, so this preparation must not be pushed without execution authorization.

Checkout pins the exact event SHA and takes depth4 for the fixed chain: future delivery →4bc4ee3→ae965898→locked3ae694. Depth2 is insufficient at current delivery, and depth3 omits the locked ancestor after a new delivery. The expected sole-parent delivery topology must be rechecked before any future upload. If another remote commit arrives, stop and recompute/review topology rather than guessing a sufficient depth. The retained full lifecycle runner is not invoked in this proposal. The SourceHead-only summary explicitly marks ancestor/source-equivalence verification NotRun; checkout history availability is not misreported as runtime equivalence verification.

The only external diagnostic operation is owned Git `rev-parse HEAD`, through the existing suspended-create/Job-bind/resume envelope. The operation keeps its original absolute30-second successful-completion budget and unchanged rescue observation limits (up to2 seconds for root exit and2 seconds for Job emptiness); rescue or unknown cleanup fails. The wrapper operation's synchronous preparation is not claimed to have a new interruptible30-second watchdog. The script step has a separate2-minute outer CI limit, and job10 minutes. Any future authorization must retain these explicit limits; no code here extends a deadline or claims that an outer CI timeout proves cleanup.

PowerShell must load the existing in-memory C# interop definition via Add-Type to invoke the Win32 envelope. This is bootstrap compilation/loading, not a dotnet/CMake build, restore or compilation of product projects. It is explicitly included in the proposed bootstrap scope; if the future approval prohibits even Add-Type, this proposal cannot execute unchanged. There are no SDK/tool installs, native/MSVC or managed product builds, four-case calls, COM/UIA targets, model calls, credentials, privilege changes or artifacts uploaded by this workflow.

The public output is one closed summary: fixed bootstrap stage/preparation category, known reviewed/expected source identities, the existing finite native stage/error categories, Created/Resumed, exit/Job/cleanup/rescue observations and fixed NotRun scope. Command path, account identity, unexpected Git text, exception message and raw operation logs stay private. Only a whole single lowercase40-hex Git result with optional one line ending can match the approved event SHA. A mismatching SHA is not published. Pass means this one Git operation succeeded and settled; it does not pass the four cases or P0/E06.

## Local evidence and newly found lookup defect

SourceHead contracts59/59 passed locally. The tests exercise receipt/output rejection, private exception redaction, no-pass-on-rescue/missing creation/exit/cleanup, one-operation AST guard and actual Linux Blocked entry. A real local shallow clone experiment of the exact prospective four-commit ancestry confirms depth3 omits locked3ae while depth4 contains it and permits the unchanged lifecycle-source diff. No Windows P/Invoke or envelope Run method is called by these tests; the Linux entry creates no diagnostic child.

The initial test harness failed because unparenthesized PowerShell multiplication in an array was bound against an Object[]; its source was privately preserved and corrected without dropping malformed-output cases. The next real Git fixture failed because Get-Command git returned four application candidates in this environment. The old `(Get-Command git -CommandType Application).Source` expression returned multiple paths; a pure call to the actual C# string-parameter binder demonstrates that a string array becomes one space-joined quoted string. This is a reproducible local command-selection defect, not evidence of the original Windows candidate count or exact failure cause. The new dedicated entry and Git fixture select exactly the first application. The old full lifecycle runner is outside this proposal and still needs separate review of its executable-selection assumptions before future full-suite execution. No old Windows receipt is rewritten.

## Files proposed for later public delivery

1. .github/workflows/windows-contracts.yml — exact checkout, depth4, SourceHead-only entry and outer bounds.
2. tests/scripts/run-source-head-windows-ci.ps1 — one operation, private output, fixed summary.
3. tests/scripts/source-head-ci-evidence.ps1 — closed preparation/output classification.
4. tests/scripts/source-head-ci-evidence.test.ps1 — pure contracts, Linux guard and actual local Git ancestry experiment.
5. docs/validation/source-head-next-diagnostic.md — scope, findings, limits and authorization boundary.
6. docs/progress.md — current delivery correction and local preparation status.
7. docs/validation/lifecycle-operation-local-diagnostics.md — appended current-delivery correction.
8. docs/validation/lifecycle-single-windows-run.md — appended current-delivery correction.

No file in this new list has been published in this preparation task. Existing raw private log, failed receipt, reviewed branches and verified source bundles remain intact. Current state remains P0/E06Blocked, Windows startup root cause unknown, native/managed lifecycle compilation and all four cases NotRun, ProductionReuseOff, ModelCalls0.


## Actual SourceHead execution after approval

The prepared eight files were delivered as b39a9076e69bec3c277e5664428ff142eaaef9f5 with the reviewed tree2bcfa3dd1a23c0da085f9d5303d491354e668686. Run38524993308/job115723148036/attempt1 completed success: one SourceHead Exit0, creation/resume/root exit/Job empty/cleanup all confirmed, RescueUsedfalse, returned SHA matched. Fixed receipt SHA256cf71da04ecb8260f4c0d31cf69e37a384c5a5ee0e2c565c52d00b73bb4edcebb is privately preserved. Earlier authorization/local-only statements describe preparation time. This one-run authorization is consumed; no rerun is permitted. Locked ancestry, product compilation, four cases, UIA/matching/performance remained NotRun. Original Windows root cause is still unknown. The next full-runner candidate is only local; see lifecycle-full-next-validation.md.
