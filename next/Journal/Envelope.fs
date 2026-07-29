namespace Wanxiangshu.Next.Journal

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

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

    let serialize (envelope: Envelope) : string =
        Encode.Auto.toString (0, envelope, extra = extra)

    /// PERSIST-005: a pre-0.5.0 line is refused, never guessed into shape.
    let deserialize (json: string) : Result<Envelope, string> =
        if FactCodec.containsLegacyFallbackFields json then
            Error FactCodec.pre050MigrationMessage
        else
            Decode.Auto.fromString<Envelope> (json, extra = extra)
