namespace Wanxiangshu.Sphinx

// WHAT[EPI-030]: durable restart fold vocabulary. The canonical spine owns the
// EventEnvelope; this contract only judges decoded sphinx observations, so the
// fold stays free of storage, codec and host dependencies.
[<RequireQualifiedAccess>]
module LegacyIntegrator =
    /// One durable inquiry: highest folded revision, opening question, and the
    /// decoded raws (oldest first) that rebuild it through replayObservations.
    type LegacyInquiryCursor =
        { Revision: int
          Question: string
          Raws: obj list }

    /// WHAT[EPI-030]: durable Sphinx Current shared with the canonical spine.
    /// Keyed by durable handle; read back only through TryCurrent "Sphinx".
    type SphinxLegacyCurrent = Map<string, LegacyInquiryCursor>

    /// One decoded legacy observation offered to the fold. ArgsJson carries the
    /// canonical JSON of the accepted MCP args; boot parses it back to a live
    /// object before replay, so the fold never touches a JSON library.
    type LegacyObservationFields =
        { Handle: string
          Tool: string
          ArgsJson: string
          Revision: int
          Question: string }

    /// WHAT[EPI-030]: envelope carrier across the owner boundary. The spine
    /// maps every accepted sphinx event to this carrier; unknown sphinx kinds
    /// ride as OtherSphinxEvent and never fail the fold.
    type LegacyEnvelopeInput =
        | LegacyObservation of LegacyObservationFields
        | OtherSphinxEvent of eventType: string

    val empty: SphinxLegacyCurrent

    val applyOne: current: SphinxLegacyCurrent -> envelope: obj -> Result<SphinxLegacyCurrent, string>
