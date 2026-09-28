namespace Wanxiangshu.Strength.Migration

open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength

/// JS-native view of one pre-delegation Strength envelope. The migration tool
/// reads envelopes through the EventStore surface and hands canonical JSON
/// text to `readLegacyEnvelope`, which is the single place the legacy wire
/// shape is understood.
type LegacyEnvelope =
    { EventId: string
      SourceStreamId: string
      EventType: string
      DecisionId: string
      BudgetEvidence: string option
      TargetProviderRun: string option
      FrameDigest: string option
      ByteLength: int option
      TracedStartInclusive: int64 option
      TracedEndExclusive: int64 option
      MaterialPayloads: string array }

/// JS-native planned import fact. `importEventJs` renders the typed fact from
/// this shape, so the tool never writes fact payload JSON itself.
type ImportFact =
    { DecisionId: string
      SourceStreamId: string
      SourceEventId: string
      ImportId: string
      OldBudgetEvidence: string option
      OutcomeKind: string
      TargetProviderRun: string option
      FrameDigest: string option
      ByteLength: int option
      TracedStartInclusive: int64 option
      TracedEndExclusive: int64 option
      MaterialPayloads: string array
      RelinquishReason: string option }

/// DELEGATE: offline planner that turns pre-delegation Strength envelopes into
/// DelegationHistoryImported facts. Every function here is pure; the caller
/// (node script) performs the IO and passes the effects in.
[<RequireQualifiedAccess>]
module DelegationHistoryMigration =

    /// Deterministic import identity: the same old event under the same
    /// contract revision always plans the same import, so re-running the tool
    /// appends an identical (idempotent) fact instead of a second truth.
    val deriveImportId: sha256: (string -> string) -> sourceEventId: string -> contractRevision: int -> string

    /// Extract one legacy Strength envelope from the canonical JSON text form
    /// of the EventStore surface's event object. Returns None for any event
    /// that is not one of the four legacy Strength fact types.
    val readLegacyEnvelope: envelopeJson: string -> LegacyEnvelope option

    /// Plan the import facts for one legacy decision. `envelopes` are that
    /// decision's envelopes in causal order and the caller already decided the
    /// decision is legacy. Raises when the retained window cannot support the
    /// plan (for example a Traced range without its Promoted material).
    val planDecision:
        sha256: (string -> string) -> contractRevision: int -> envelopes: LegacyEnvelope array -> ImportFact array

    /// Render one planned fact as the JS-native event the EventStore surface
    /// appends. Envelope, payload and event id come from the same encoder the
    /// runtime decodes.
    val importEventJs: sha256: (string -> string) -> fact: ImportFact -> obj
