# E06 receiver startup diagnostics

The tested baseline is `5c4f3e97befafc5664ae0d0069d087242eacaf6e`. Its [OLE run](https://github.com/T1doo/KuaiYi/actions/runs/38265638882) compiled successfully but never reached receiver Ready. Actual Drop, GetData, recovery and physical acceptance were NotRun. EOF alone does not identify the root cause.

The receiver now emits a fixed startup stage, a fixed exception category and a signed 32-bit HRESULT. The parent retains only this projection and its actual owned child's exit code. It does not retain stderr text, exception messages, paths, nonce or translation content. Unknown exception classes project to Other. Unknown, malformed, extra-field and oversized lines are discarded while the pipe continues draining. Retention is limited to 32 records, lines to 128 bytes and read buffers to 4096 bytes; temporary buffers are cleared.

The original diagnostic read remains owned after a wait times out or cancellation is requested. Finalization requires its actual settlement, in addition to the existing process, registration, delivery, control and startup checks. Parent identity validation and the existing OLE deadlines remain unchanged.

Eleven new diagnostic assertion cases and 48 related existing cases passed locally through direct invocation of the existing xUnit assertions, without VSTest or its socket host. This includes an actual owned child filling stderr before handshake, a failing owned child with stage/exit projection, cancellation-ignoring reads and delayed original diagnostic settlement. A fixture-only metadata file now supplies the existing Off/blocked observations; the original epoch, timeout, kill failure and exit assertions are preserved. Only complete newline records are consumed. Fixtures remain explicit test substitutes and do not prove Windows COM/OLE behavior.

Normal locked App and test-project restore remains blocked by NU1900: the official NuGet vulnerability service is unreachable. A finite normal retry failed. NuGetAudit, warning policy, dependency versions, lock files and network permissions were not changed. Package-free production transfer and test-fixture builds succeeded with zero warnings/errors. An independent compiler check uses the actual App/Receiver C# source and existing reference assemblies; it is a type check, not the normal WPF project build, installation or Windows runtime validation.

Run the standard tests on an environment with the required services available:

```powershell
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~OleStartupDiagnosticsTests|FullyQualifiedName~OleReceiverTests|FullyQualifiedName~OleReadOwnershipTests|FullyQualifiedName~OleReceiverBoundaryTests|FullyQualifiedName~OleControlProcessTests"
```

The single authorized [Windows diagnostic run](https://github.com/T1doo/KuaiYi/actions/runs/38271957014) completed with failure for source `1a65a1df3d6ad65a8146711623bc1efbd95ef425`. The native DLL and three normal Windows managed builds passed with zero managed warnings/errors. Receiver Hello validation, parent identity verification and shared activation initialization completed. The receiver then failed at NativeExportCreate with InvalidData / HRESULT 0x80131501, exit code 3; the original diagnostic read actually settled. ParentExportRead observed EOF / 0x80070026. Registration, Ready, actual Drop/GetData, recovery and close during GetData were not reached.

The exact root cause is not yet confirmed: the Export constructor has a length check and a standard-packet check that both throw InvalidData. This projection cannot distinguish them. [Microsoft's Seek contract](https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-istream-seek) says CUR,0 reports the cursor, which does not prove the value or packet shape in this run. The existing Seek, packet validation and identity guards were preserved; no speculative repair or extra CI run was performed. The next smallest diagnostic change would identify those two checks with fixed sub-stages.

Only the [reviewed whitelist result](test-evidence/2026-10-12-e06-startup-whitelist-result.json) is delivered. It excludes process IDs, paths, raw stderr, environment/authentication history and full job logs. Original materials remain local-only. Physical E06, Windows 11 acceptance and P0 remain unpassed.
