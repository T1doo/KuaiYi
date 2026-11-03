# Escape scope correction — uncompiled source candidate

Baseline `e1d93d1cba05cd4fa10299c9dfcf5bccbc1ff651` was clean. Product source at that baseline is the previously compiled `525cf483e54a4486edcbbecc016e426035281076`; that compilation is not certification of the change described here.

## Actual finding and correction

Approved DOCX version 1, §22 and §29/B60 require Escape to exit a local menu and return to its entry. The window's `PreviewKeyDown` handler instead called `NativeDesktop.SetOverlayStyle(hwnd,false)` and `CollapseResult()` whenever the overflow menu was not open. In the concrete path “deliberately focused reader, no open menu, press Escape,” it therefore collapsed the entire result and cleared the expanded source presentation through `CollapseResult`.

The handler now accepts only unmodified Escape, selects an already-open overflow or anchor context menu, and closes that menu. With no open menu, or with a modified key, it returns without handling the key. It no longer invokes activation-style changes or result collapse. Existing menu Closed handlers, Button/MenuItem commands, source state, geometry and request gates are retained. No new focus activation or global focus command is introduced.

Only `src/KuaiYi.App/OverlayPresentation.cs` changes in product code. The [source review receipt](../test-evidence/frontend-day-2026-11-03/escape-scope-source-review.json) and [564-input snapshot](../test-evidence/frontend-day-2026-11-03/escape-scope-source-inputs.json) bind the candidate to its actual bytes.

## Independent review and verification boundary

The read-only reviewer inspected the final diff and found no definite static blocker. The proven no-menu-Escape-to-whole-result-collapse path has been removed from source; the handler has no direct activation, reading-state, geometry or request mutation. Closing a menu still follows the existing Closed flow.

**No new compilation, test, restore, download, CI, Windows execution, screenshot or permission change occurred.** The new candidate is uncompiled and untested. All three execution windows remain consumed; their 122.032457139 seconds, observed output accounts and the older excess/Core ledger remain unchanged. No denied path was retried, no protected file deleted and no screen substitute presented as product evidence. Main and release/package state are unchanged.

## Concrete stop point

Other inspected keyboard items did not establish another independent source defect:

- The Canvas child order and lack of explicit TabIndex prove there is no explicit application ordering guarantee; they do not prove the actual WPF Tab sequence is wrong. No speculative navigation policy was installed.
- The shared Closed handler schedules the existing peek leave timer. The overflow/style/version menus already identify their PlacementTarget, and other context menus have corresponding entries. This does not prove WPF fails to restore focus, so no guessed focus-restoration or activation behavior was added.
- No custom Enter/Space path duplicating Button/MenuItem Click was found in this scope. Existing command delivery was retained.

B60 stays **Partial**, with native acceptance **Blocked/NotRun**. Popup event routing, keyboard menu cancellation, submenu-level Escape, actual return focus, Tab/ShiftTab order through controls/body/source and Enter/Space delivery require a separately authorized interactive Windows/WPF session. Check no-menu Escape without changing visible result/source state; then open each actual menu by keyboard, close its local level and verify focus returns to the intended entry without changing activation or issuing requests. Large independent text scaling and clipped focus/hit areas also remain real Windows checks.

No such session, user-computer operation, CI run or new compile budget is authorized here. The current task stops after this concrete source correction and review; it does not repeat the prior visual audit or claim the whole frontend complete.
