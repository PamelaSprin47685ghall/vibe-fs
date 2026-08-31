# semantic-trace — HOW

## Owner boundary

`src/Wanxiangshu/Context/Trace/SemanticTraceSurface.fs` is the registered JS proof surface. It accepts plain JS descriptors and opaque journal/projection capabilities, converts them to typed semantic-trace inputs, calls production owner operations, and returns copied plain evidence. It does not expose projection storage, F# union/record representation, a generic history fold, WorkRecord rendering policy, Todo locality policy, or Prefix reanchor mutation.

The published owner vocabulary is:

| Operation family | Laws | Published evidence |
|---|---|---|
| `XTraceProjection.openingEvidence/hasOpening`, `latestTerminalEvidence/terminalEvidenceForProviderRun` | SEMANTIC-TRACE-001/010 | copied opening and terminal evidence |
| `XTraceCapture.semanticPart`, `captureObservedMessagesWithReceipt` | SEMANTIC-TRACE-002/007/008 | copied semantic parts and typed capture receipts |
| `XTraceCursor.*`, `XTraceRange.*` | SEMANTIC-TRACE-003/006 | opaque monotonic cursors and half-open ranges |
| `orderedSemanticParts/currentGenerationSemanticParts/providerRunParts` | SEMANTIC-TRACE-003/004/009 | ordered, run-bound, generation-aware evidence |
| `toolResultParts/toolPartsForHostIdentity`, Host-message and range queries | SEMANTIC-TRACE-002/004/006 | exact provider, Host, and range evidence |
| `XTrace.render`, `XTraceMaterialization.renderRange` | SEMANTIC-TRACE-005/006/007 | canonical semantic rendering |
| `XTrace.flatten`, `currentProjection/currentProjectionBetween` | SEMANTIC-TRACE-007 | one semantic projection formula |
| Typed capture receipts and exact `SemanticTrace.Contract` symbols | SEMANTIC-TRACE-008 | owner-issued receipts and declared contracts |

`Cursor.fs` is registered as the exact `semantic-evidence` contract kind: its durable cursor operations cross the execution-position guard only under `WHAT[SEMANTIC-TRACE-003]`; symbol roots and representation fields are not authorized.

## Capture and provenance

The capture owner maps every semantic message part once. Activity parts are transport bookkeeping and are omitted. `captureObservedMessagesWithReceipt` owns the retry membrane: a typed `ProviderRetryAttempt` observation retains physical Host identity for stable eligibility but contributes no semantic parts. Receipts explicitly report identity mode, previous/current head, and captured counts.

Provenance carries stable provider, Host-message, and Host-part identity. Queries return copied `XTraceSemanticPartView` evidence. A new Host generation changes `currentGenerationSemanticParts` without deleting lifecycle-wide `orderedSemanticParts`, Opening evidence, terminal evidence, or the globally monotonic cursor.

## Law registration

| Law | Proof paths |
|---|---|
| SEMANTIC-TRACE-001 | `requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] opening evidence is copied verbatim and idempotent`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] semantic parts append in strict cursor order`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] terminal evidence is idempotent per provider run` |
| SEMANTIC-TRACE-002 | `requirements/semantic-trace/tests/x-trace-capture.test.mjs::WHAT[SEMANTIC-TRACE-002] capture mapper copies text and reasoning semantics`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-002] typed retry observation retains stable identity but appends no semantics`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs::WHAT[SEMANTIC-TRACE-002] provider-run query returns copied semantic evidence` |
| SEMANTIC-TRACE-003 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-003] cursor vocabulary is monotonic and opaque`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-003] duplicate and retreating cursors are rejected` |
| SEMANTIC-TRACE-004 | `requirements/semantic-trace/tests/x-trace-provider-run-provenance.test.mjs::WHAT[SEMANTIC-TRACE-004] provider runs segment the ordered semantic projection`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs::WHAT[SEMANTIC-TRACE-004] stable Host message identity resolves at a durable cursor` |
| SEMANTIC-TRACE-005 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-005] canonical render is deterministic and omits provenance`；`requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs::WHAT[SEMANTIC-TRACE-005] raw projection storage is rejected while copied semantic query is admitted` |
| SEMANTIC-TRACE-006 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-006] range vocabulary is half-open`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs::WHAT[SEMANTIC-TRACE-006] Host message set resolves only to its exact contiguous range`；`requirements/semantic-trace/tests/x-trace-locality.test.mjs::WHAT[SEMANTIC-TRACE-006] range and frontier queries preserve half-open boundaries` |
| SEMANTIC-TRACE-007 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-007] flatten is the single semantic source`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-007] projection capture is idempotent and reports owner receipts`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-007] materialization reads canonical durable semantics` |
| SEMANTIC-TRACE-008 | `requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs::WHAT[SEMANTIC-TRACE-008] semantic surface admits only the three append transitions`；`requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs::WHAT[SEMANTIC-TRACE-008] published trace contract contains exact implementation vocabulary` |
| SEMANTIC-TRACE-009 | `requirements/semantic-trace/tests/x-trace-compaction-survival.test.mjs::WHAT[SEMANTIC-TRACE-009] a new Host generation does not erase opening or semantic parts`；`requirements/semantic-trace/tests/x-trace-compaction-survival.test.mjs::WHAT[SEMANTIC-TRACE-009] cursor sequence remains global across Host generations` |
| SEMANTIC-TRACE-010 | `requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-010] conflicting opening is rejected`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-010] opening capture reports idempotent evidence` |
