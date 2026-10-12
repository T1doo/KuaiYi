# E06 native marshal contract verification

P0/E06 remains Blocked. The previous Windows startup diagnostic reached NativeExportCreate and rejected an export with InvalidData. That result did not distinguish the length guard from the packet-header guard.

This slice adds one bounded, CI-only path from the real native receiver target through production CoMarshalInterface and its validator to production CoUnmarshalInterface on an independent parent STA. The actual receiver dispatcher and parent identity checks remain active; the imported pointer stays owned by that STA until release. Original receiver, diagnostic read and importer tasks must settle before finalization. No RegisterDragDrop, Drop, GetData, capture, providers or settings initialization occurs. Production builds exclude the CI entry unless KuaiYiIncludeWindowsCiProbe is explicitly enabled.

The production lower bound of 72 bytes, 65536-byte upper bound, Seek slot 5/CUR 1, LOCAL 0/TABLESTRONG 1, standard packet rule, MEOW signature and IDropTarget IID are unchanged at this gate. Diagnostic output contains only fixed enum classifications and reviewed bounded lengths/capacities. It excludes packet bodies, OID/IPID, identities, raw stderr and exception messages. A length rejection emits its classification before any optional owned 24-byte header observation. Mixed startup/export metadata retains at most 32 entries and continues discarding all other stderr. Successful evidence requires matching native and parent packet lengths and actual Import completion, independent STA, pointer release and owned task settlement.

Local verification before freezing this slice:

- 67 original xUnit assertion cases passed through direct invocation: 8 export contracts, 11 startup diagnostic cases and 48 related receiver/ownership/control cases. This is not VSTest or Windows COM proof. Earlier failing mixed-record retention output is preserved locally alongside its corrected result.
- 29 PowerShell schema/discard contracts and the existing 45 Windows runner contracts passed. They use synthetic receipts; none represents real COM import. Source swaps, inconsistent lengths, malformed enums, raw fields and unconfirmed ownership cannot certify Pass.
- Package-free production transfer/fixture builds passed with zero warnings/errors. Compiler checks of actual native-marshal C# and App sources against existing references passed with zero warnings/errors. These are type checks, not normal WPF project or Windows runtime validation.
- The actual Linux MarshalOnly entry reports Blocked and exits 2 without any native/COM/desktop claim. Normal App/test restore is still blocked by NU1900 at the official NuGet audit service. Audit, warnings, dependency pins, lock files and network policy remain unchanged.

One standard Windows CI verification is pending the frozen-source independent read-only review. It will build only the native OLE DLL, Receiver and App and run the minimal marshal contract. It will retain no uploaded artifact/cache and publish only the fixed marshal receipt. Complete logs and failed local attempts remain local-only; this document contains no full progress or authentication history.

Actual Drop, GetData, close/recovery, physical E06, Windows 11 and P0 acceptance remain NotRun/unpassed. The synthetic 72-byte header fixtures do not establish COM compatibility. No speculative bound or Seek repair has been made.
