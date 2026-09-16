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
