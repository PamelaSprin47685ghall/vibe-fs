namespace Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable

open System
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact

type StreamId =
    | Workspace
    | Session of SessionId
    | Child of ChildId
    | Process of ProcessId

/// One durable journal line (PERSIST-001).
type Envelope =
    {
        RuntimeId: RuntimeId
        LocalSeq: LocalSeq
        ObservedAt: ObservedAt
        EventId: EventId
        Stream: StreamId
        /// The provider run this fact was observed during, when there was one.
        ///
        /// Replaces the previous `TurnId`, which was a third name for the same
        /// thing: HOST-010 establishes that one assistant message is one provider
        /// request is one turn, so a separate turn identity could only ever be a
        /// copy of the run id — or disagree with it.
        ///
        /// `None` for facts that belong to no run: runtime start, worktree
        /// creation, a Manager job's lifecycle.
        ProviderRun: ProviderRunIdentity option
        Fact: Fact
    }

module Envelope =

    let private extra = Extra.empty |> Extra.withInt64

    /// PERSIST-001 ordering: within a runtime by LocalSeq, across runtimes by
    /// observation time with the runtime id as the tie-break.
    ///
    /// The tie-break is not cosmetic. Two runtimes can observe facts in the same
    /// millisecond, and a fold must be deterministic across restarts, so the
    /// order cannot depend on which line the reader happened to see first.
    let compareSortKey (a: Envelope) (b: Envelope) : int =
        if a.RuntimeId = b.RuntimeId then
            compare (LocalSeq.value a.LocalSeq) (LocalSeq.value b.LocalSeq)
        else
            let byObservation = compare a.ObservedAt b.ObservedAt

            if byObservation <> 0 then
                byObservation
            else
                String.Compare(RuntimeId.value a.RuntimeId, RuntimeId.value b.RuntimeId, StringComparison.Ordinal)

    /// PERSIST-001: the line is the durable artifact, so its bytes must not depend
    /// on the machine that wrote it.
    ///
    /// `ObservedAt` is pinned to offset zero before encoding. Writers always pass
    /// `DateTimeOffset.UtcNow`, so this changes nothing they produce — but the
    /// DECODER attaches the reader's local offset, so without this a line read on a
    /// `TZ=Asia/Shanghai` host and written back would render `+08:00` for the same
    /// instant. Two hosts would then disagree on the bytes of one history, and a
    /// byte comparison of two replicas would report a difference that is not one.
    ///
    /// `ToOffset TimeSpan.Zero` rather than `ToUniversalTime()`: Fable's
    /// `toUniversalTime` leaves the emitted value's `offset` field `undefined`, and
    /// the encoder then renders a bare `Z` by accident rather than by contract.
    let serialize (envelope: Envelope) : string =
        Encode.Auto.toString (
            0,
            { envelope with
                ObservedAt = envelope.ObservedAt.ToOffset TimeSpan.Zero },
            extra = extra
        )

    /// PERSIST-005: a pre-0.5.0 / pre-tip-v2 line is refused, never guessed into shape.
    /// Tip v2 must be checked here (Boot reads envelopes) not only in FactCodec.deserializeFact:
    /// Auto-decode of BlogObservationCommitted (or legacy BlogEntryCommitted) without
    /// TipRuleId yields an opaque Thoth error, Boot truncates the stream mid-file, later
    /// Abandon/Commit vanish, and fold then dies on "already has open request" — a lie
    /// about the real cause.
    let deserialize (json: string) : Result<Envelope, string> =
        if FactCodec.containsLegacyFallbackFields json then
            Error FactCodec.pre050MigrationMessage
        elif FactCodec.containsLegacyScoreVectorEntry json then
            Error FactCodec.tipV2CleanBreakMessage
        else
            Decode.Auto.fromString<Envelope> (FactCodec.rewriteLegacyObservationTags json, extra = extra)
