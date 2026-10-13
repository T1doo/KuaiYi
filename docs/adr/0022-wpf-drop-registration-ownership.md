# ADR 0022: WPF drop registration ownership

Status: Implemented; actual Windows verification required.

WPF .NET 10 HwndSource.Initialize registers an OLE drop target before Window.SourceInitialized, regardless of AllowDrop. Registering the isolated receiver on the same HWND without a handoff therefore returns DRAGDROP_E_ALREADYREGISTERED (0x80040101). HwndSource disposal later calls RevokeDragDrop again.

The two application-owned floating windows now derive from OleDropWindow. Its sealed OnSourceInitialized validates the creating UI thread/process, exact HwndSource, RootVisual and uninitialized lifetime, then releases the framework's initial registration before publishing SourceInitialized to subscribers. EnsureHandle-only creation is deliberately rejected. No registrar error path revokes an existing registration to retry.

A per-window-generation OleWindowOwnership object permits one registrar at a time. It stays reserved until native revoke, imported COM reference release and apartment cleanup succeed AND the original registrar completion task actually settles. Failed cleanup prevents reuse and normal close. A failed initial handoff permits WPF teardown because no custom registrar was admitted, while preventing handoff retries. Disposing the exact HwndSource invalidates that generation permanently.

Normal close invokes existing asynchronous receiver shutdown first, then the base window guard permits WPF teardown only after ownership settlement. This contract does not make forced process termination or application/dispatcher shutdown a graceful close. No internal WPF reflection or undocumented OLE window property is used.

Tests cover handoff failure/reentrancy, wrong HWND, duplicate and concurrent reservations, cleanup-before-task and task-before-cleanup ordering, cleanup failure, repeated registration and handle-generation invalidation. The explicit CI-only Windows probe reproduces the initial conflict with an ordinary WPF window, verifies EnsureHandle rejection leaves its registration intact, then uses a handed-off real HWND for two register/revoke cycles and checks that close cannot revoke an active registration. Existing production GetData/cancel/recovery/close scenarios execute afterward; the preliminary ownership probe is not a substitute for those scenarios or Windows 11 physical acceptance.

Sources:

- [WPF v10.0.0 HwndSource creation and disposal](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndSource.cs)
- [WPF v10.0.0 Window creation order](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs)
- [RegisterDragDrop contract](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-registerdragdrop)
- [RevokeDragDrop contract](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-revokedragdrop)
