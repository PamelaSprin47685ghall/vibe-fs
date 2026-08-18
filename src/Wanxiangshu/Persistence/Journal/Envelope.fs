namespace Wanxiangshu.Persistence.Journal

open Wanxiangshu.Composition.Durable

open System
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

type StreamId =
    | Workspace
    | Session of SessionId
    | Child of ChildId
    | Process of ProcessId

/// One durable journal line (PERSIST-001).
/// DSL-state-combination: domain — stream and optional provider-run identity
/// route one durable fact envelope; absence means the fact has no run, not a
/// workflow stage.
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

    let private compareAcrossRuntimes (a: Envelope) (b: Envelope) : int =
        let byObservation = compare a.ObservedAt b.ObservedAt

        if byObservation <> 0 then
            byObservation
        else
            String.Compare(RuntimeId.value a.RuntimeId, RuntimeId.value b.RuntimeId, StringComparison.Ordinal)

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
            compareAcrossRuntimes a b

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
    let private streamEncoder (stream: StreamId) : JsonValue =
        match stream with
        | Workspace -> Encode.object [ "Workspace", Encode.nil ]
        | Session id -> Encode.object [ "Session", Encode.string (SessionId.value id) ]
        | Child id -> Encode.object [ "Child", Encode.string (ChildId.value id) ]
        | Process id -> Encode.object [ "Process", Encode.string (ProcessId.value id) ]

    let private providerRunEncoder (run: ProviderRunIdentity option) : JsonValue =
        match run with
        | None -> Encode.nil
        | Some id -> Encode.string (ProviderRunIdentity.value id)

    let serialize (envelope: Envelope) : string =
        match envelope.Fact with
        | MagicTodo magicTodo ->
            Encode.toString 0 (
                Encode.object
                    [ "RuntimeId", Encode.string (RuntimeId.value envelope.RuntimeId)
                      "LocalSeq", Encode.int64 (LocalSeq.value envelope.LocalSeq)
                      "ObservedAt", Encode.datetimeOffset (envelope.ObservedAt.ToOffset TimeSpan.Zero)
                      "EventId", Encode.string (EventId.value envelope.EventId)
                      "Stream", streamEncoder envelope.Stream
                      "ProviderRun", providerRunEncoder envelope.ProviderRun
                      "Fact", Encode.object [ "MagicTodo", Encode.string (MagicTodoFactCodec.encode magicTodo) ] ]
            )
        | _ ->
            Encode.Auto.toString (
                0,
                { envelope with
                    ObservedAt = envelope.ObservedAt.ToOffset TimeSpan.Zero },
                extra = extra
            )

    /// PERSIST-005: a pre-0.5.0 / pre-tip-v2 line is refused, never guessed into shape.
    /// Tip v2 must be checked here (Boot reads envelopes) not only in FactCodec.deserializeFact:
    /// Auto-decode of BlogObservationCommitted without TipRuleId yields an opaque Thoth error,
    /// Boot truncates the stream mid-file, later Abandon/Commit vanish, and fold then dies on
    /// "already has open request" — a lie about the real cause. Pre-cutover observation tags
    /// are not decoded.
    let private streamDecoder: Decoder<StreamId> =
        Decode.object (fun get ->
            if get.Optional.Field "Workspace" Decode.unit |> Option.isSome then
                StreamId.Workspace
            elif get.Optional.Field "Session" Decode.string |> Option.isSome then
                StreamId.Session(SessionId.create (get.Required.Field "Session" Decode.string))
            elif get.Optional.Field "Child" Decode.string |> Option.isSome then
                StreamId.Child(ChildId.create (get.Required.Field "Child" Decode.string))
            else
                StreamId.Process(ProcessId.create (get.Required.Field "Process" Decode.string)))

    let private providerRunDecoder: Decoder<ProviderRunIdentity option> =
        Decode.option (Decode.string |> Decode.map ProviderRunIdentity.create)

    let private tryDecodeMagicTodoEnvelope (json: string) : Envelope option =
        let decoder: Decoder<Envelope> =
            Decode.object (fun get ->
                let canonical =
                    get.Required.Field "Fact" (Decode.object (fun fget -> fget.Required.Field "MagicTodo" Decode.string))

                match MagicTodoFactCodec.tryDecode canonical with
                | Ok fact ->
                    { RuntimeId = RuntimeId.create (get.Required.Field "RuntimeId" Decode.string)
                      LocalSeq = LocalSeq.create (get.Required.Field "LocalSeq" Decode.int64)
                      ObservedAt = get.Required.Field "ObservedAt" Decode.datetimeOffset
                      EventId = EventId.create (get.Required.Field "EventId" Decode.string)
                      Stream = get.Required.Field "Stream" streamDecoder
                      ProviderRun = get.Required.Field "ProviderRun" providerRunDecoder
                      Fact = Fact.MagicTodo fact }
                | Error reason -> failwith ("invalid MagicTodo canonical payload: " + reason))

        try
            match Decode.fromString decoder json with
            | Ok envelope -> Some envelope
            | Error _ -> None
        with _ -> None

    let deserialize (json: string) : Result<Envelope, string> =
        if FactCodec.containsLegacyFallbackFields json then
            Error FactCodec.pre050MigrationMessage
        elif FactCodec.containsLegacyScoreVectorEntry json then
            Error FactCodec.tipV2CleanBreakMessage
        else
            match tryDecodeMagicTodoEnvelope json with
            | Some envelope -> Ok envelope
            | None -> Decode.Auto.fromString<Envelope> (json, extra = extra)
