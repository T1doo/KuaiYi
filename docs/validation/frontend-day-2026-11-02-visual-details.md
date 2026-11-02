# Approved visual detail audit — source candidate only

Baseline: `680c53e6822258bbebaf618b3c24653ce0661258`, clean before this slice. The unchanged approved DOCX has SHA256 `cfe55dbc74e34b02da650e24e943f375048bfa5e96606d6ca5619288f5645a14`. All 47 extracted media hashes matched, and all eight existing contact sheets were re-viewed, including the original reference fragments and proposal figures. They are design evidence, not screenshots of KuaiYi running.

The [12-item audit](../test-evidence/frontend-day-2026-11-02/visual-detail-audit.csv) maps typography, spacing, controls, backgrounds, motion and adaptive layout to the actual implementation. Page numbers there are the DOCX's explicit page-break sequence, not a Word-rendered pagination claim; use the section and figure numbers as the exact locator. No document rendering or new image/asset download was performed.

## Two evidenced source omissions corrected

| Approved location | Baseline difference | Source change |
|---|---|---|
| §23 continuation, explicit page index 28: small buttons and inputs, 3 DIP radius; §18 figures 29–30 | Buttons and read-only choices already had 3 DIP; TextBox/PasswordBox only set brushes, border and padding, with no explicit radius template. This proves missing design-value wiring, not what Windows happened to render. | `DesktopTheme.TextInputTemplate` now applies the same 3 DIP surface to these inputs. It retains the original control and its empty `PART_ContentHost`, TextBox scrolling bindings and PasswordBox's hidden scrolling. Existing hover/focus/disabled brushes and auto height remain in use. No text/password binding or editing handler is added. |
| §6 figures 10–11, explicit page index 7: proposed 80–120 ms local feedback and immediate reduced-motion state | Hover/press feedback used only immediate setters. | `DesktopControlFeedback` interpolates only the small tint opacity for 100 ms, starting from its current value on reversal. Focus, border changes and command delivery remain immediate. User/system reduced motion cancels the clock and immediately applies the current state; settings draft changes preview only presentation. Shell feedback reads saved preferences. No geometry animation or anchor movement is added. |

The new helper is included in the [564-input source snapshot](../test-evidence/frontend-day-2026-11-02/visual-detail-source-inputs.json). Only four App source files change. The 48 DIP anchor, request identities, Tag-based commands, content/version state and permission/request boundaries are unchanged.

## Existing design decisions retained

- §3/11/16/23: a single opaque reading plane; 15 DIP body, 1.55 line height, short headings at 16/15 of body size, and 16 DIP content inset already have corresponding layout/measurement code. No new size reduction or card stack is introduced.
- §12/15: source images remain content. Opaque backgrounds are the default, while Mica/Acrylic are optional in the approved design. Wallpaper, brand images and marketing photos are not product backgrounds; no new image/font package is imported. Existing original vector shapes and system fonts remain the implementation path.
- §14/27: 220/180 ms geometry, 80 ms body phase, 350/250 ms auxiliary-strip waits and tau70 height following are retained. The new 100 ms tint does not alter those contracts or the separate UIA 250/600 ms budgets. Reduced geometry motion remains immediate.
- §22/29: the existing responsive toolbar rows, 44 DIP touch slots, fixed settings actions, stacked preference rows and independent text factor remain. This slice does not redo approved page layout. High contrast continues to use system resources, and theme changes need no new transition animation.

## Independent review and actual verification boundary

The read-only reviewer inspected the final input templates and tint implementation. No definite static blocker was found. The control instances/standard content-host parts, scrolling properties, binding paths and minimum-height behavior remain intact in this source scope. The tint takes the current effective opacity before replacing the clock, settles to its base target, and removes the clock immediately when reduced motion is inherited. No new timer, completion callback, static subscription, request or permission was introduced. The existing system-preference handler still unsubscribes on window close.

This is **static review only**. No build, test, restore, dependency/cache download, CI or Windows execution occurred. The previous App/Provider SDK401 builds and eight passing cases belong to `55a24b78b49767565c71a5efdd6646f1e6d072e4`; they do not validate this changed candidate. Both approved execution rounds remain consumed, and no third pass was started. Their prior time/output charges and original excess/Core account are not reset or credited by this source work. No protected-file deletion or denied-path retry occurred.

## Stop point and remaining gaps

Both newly proved source omissions in this visual audit are wired. The rest of these 12 reviewed visual items yielded retained implementation or a runtime acceptance gap, so this slice stops here rather than inventing further styling.

The current candidate's compilation and template loading remain **NotRun**. Actual 3 DIP input pixels, IME/caret/selection, password masking, scroll behavior, all state-color composites, quick feedback reversal, system preference notifications, reduced-motion timing, independent 200/225% text, popup/focus/mixed-DPI/real hit areas and same-viewport before/after Windows screenshots remain **NotRun**. Existing broader feature/acceptance gaps in the 77-boundary table remain separately open; this audit does not mark the entire DOCX or product complete. SDK402, native clipboard/OLE/UIA, installation/signature qualifications and the 23 blocked Core cases are unchanged.

See the [audit receipt and independent review scope](../test-evidence/frontend-day-2026-11-02/visual-detail-audit.json). No further research or execution is queued in this slice.
