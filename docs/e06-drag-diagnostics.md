# Bounded drag-entry diagnostics

The actual Windows probe waits for `QueryEnd` with COPY before releasing its test-owned mouse press to enter Drop. A deadline here does not prove DragEnter was never called. Registration readiness, a format query, GetData and final cleanup are separate observations.

The CI-only checkpoint retains a unique fixed-enum Trace list for source DoDragDrop begin/return, format queries and GetData; receiver ready/query begin/query end (COPY or non-COPY)/DropBegin; and owned-input/source cleanup outcomes. These are observations received by the parent, not a complete chronological event log. Missing events remain unknown; repetition counts cannot be inferred. No text, window titles, coordinates, arbitrary errors, paths or process identifiers are added to this list. The reader reprojects the whitelist and rejects unknown values, wrong types, duplicates and oversized arrays.

Receiver checkpoint rows also expose the original registrar completion Task state, current fixed native phase and first failing phase. Cleanup progresses through revoke, COM release and apartment uninitialization. A phase named Complete does not substitute for Task settlement, a completed parent control write does not establish receiver acknowledgement, and checkpoints cannot promote final acceptance.

The scenario finally now attempts release of only its own successfully injected mouse press before source cleanup. Failed release retains ownership for the outer final attempt and does not prevent source cleanup. When a scenario already failed, cleanup errors are recorded separately without replacing the original exception object/stack. Without a primary failure, cleanup failure still fails the test. The checkpoint retains the first primary and first cleanup code independently.

Controlled regression tests reproduce the previous disposal-before-release ordering and exception masking, then exercise the corrected helper used by the actual Windows scenario. They do not prove that held input caused a particular hosted Windows failure. Actual source/receiver phase evidence is still required; production admission gates and request deadlines are unchanged.

The hosted runner remains one standard Windows job bounded to 15 minutes with a separately bounded application process, no real model calls and no private log uploads. Physical E06 and Windows 11 acceptance require their own execution evidence.
