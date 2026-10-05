namespace Wanxiangshu.Sphinx.V2.Persistence

open System
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core

/// The Sphinx v2 canonical integration rule.
///
/// WHAT[sphinx-v2-009]: this is the only v2 fold. It reads accepted envelopes, decodes
/// each transition batch, runs the single Core reducer, and publishes the resulting
/// state as `Current`. There is no second history: the old results-only registry, the
/// stage cache and the caller-held Current are all gone, and a handle is a durable
/// reference rather than a parallel copy.
///
/// WHAT[sphinx-v2-016]: the fold is deterministic. Replaying the same envelope sequence
/// always produces the same state, so the `Current` here is exactly what a cold restart
/// rebuilds — no network, no fresh randomness, no model call.

[<RequireQualifiedAccess>]
type CurrentError =
    | DomainConflict of DomainConflict
    | SemanticRejected of reason: string

[<RequireQualifiedAccess>]
module Integrator =

    /// The `Current` key v2 inquiries are published under.
    let currentKey = "SphinxV2"

    type private AcceptedTransition =
        { State: InquiryState
          Envelope: EventEnvelope }

    type private InquiryHistory =
        { Accepted: Map<Wanxiangshu.Sphinx.V2.Core.EventId, AcceptedTransition>
          Heads: Set<Wanxiangshu.Sphinx.V2.Core.EventId> }

    type private Current =
        { Histories: Map<InquiryId, InquiryHistory>
          Unavailable: Map<string, string> }

    let private empty =
        { Histories = Map.empty
          Unavailable = Map.empty }

    let private streamOf inquiryId =
        "sphinx-v2/" + InquiryId.value inquiryId

    let private acceptedParentState (states: Current) inquiryId parent : Result<InquiryState option, string> =
        match parent with
        | None -> Ok None
        | Some eventId ->
            states.Histories
            |> Map.tryFind inquiryId
            |> Option.bind (fun history -> Map.tryFind eventId history.Accepted)
            |> Option.map (fun accepted -> Ok(Some accepted.State))
            |> Option.defaultValue (Error "parent has no accepted state for this inquiry")

    /// A valid ancestor remains a base even after another child advances a head.
    /// These snapshots are derived from accepted facts, never independent history.
    let parentState (current: obj) inquiryId parent : Result<InquiryState option, string> =
        let states = if isNull current then empty else unbox<Current> current

        match Map.tryFind (streamOf inquiryId) states.Unavailable with
        | Some reason -> Error reason
        | None -> acceptedParentState states inquiryId parent

    /// One registered business oracle; only the shared engine owns enumeration,
    /// canonical ordering, storage validation and the durable cut protocol.
    let private integrateOne digest (current: Current) (envelope: EventEnvelope) : Result<Current, string> =
        Codec.decode digest envelope
        |> Result.mapError (fun fault -> fault.Code + ": " + fault.Message)
        |> Result.bind (fun batch ->
            parentState (box current) batch.InquiryId batch.PreviousHead
            |> Result.bind (fun prior ->
                let eventId =
                    Identity.EventId.value envelope.EventId
                    |> Wanxiangshu.Sphinx.V2.Core.EventId.create

                Reducer.applyTransition digest eventId prior batch
                |> Result.mapError (fun fault -> fault.Code + ": " + fault.Message)
                |> Result.map (fun next ->
                    let history =
                        current.Histories
                        |> Map.tryFind batch.InquiryId
                        |> Option.defaultValue
                            { Accepted = Map.empty
                              Heads = Set.empty }

                    let heads =
                        batch.PreviousHead
                        |> Option.map (fun parent -> Set.remove parent history.Heads)
                        |> Option.defaultValue history.Heads

                    { current with
                        Histories =
                            Map.add
                                batch.InquiryId
                                { Accepted = Map.add eventId { State = next; Envelope = envelope } history.Accepted
                                  Heads = Set.add eventId heads }
                                current.Histories })))

    let rule (digest: string -> string) : IntegrationRule =
        { Name = "SphinxV2"
          Initial = box empty
          FaultScope = fun envelope -> EventStreamId.value envelope.StreamId
          Accepts = fun envelope -> SphinxV2EventTypes.isKnown envelope.EventType
          Integrate = fun current envelope -> integrateOne digest (unbox<Current> current) envelope |> Result.map box
          PlanCut =
            fun _ envelope reason _ ->
                Ok
                    { ResetJson =
                        CanonicalJson.canonicalJson (
                            createObj [ "stream" ==> EventStreamId.value envelope.StreamId; "reason" ==> reason ]
                        ) }
          ApplyCut =
            fun current reset ->
                let decoder =
                    BodyDto.exact
                        [ "stream"; "reason" ]
                        (Decode.object (fun get ->
                            get.Required.Field "stream" Decode.string, get.Required.Field "reason" Decode.string))

                Decode.fromString decoder reset
                |> Result.map (fun (stream, reason) ->
                    let states = unbox<Current> current

                    box
                        { states with
                            Unavailable = Map.add stream reason states.Unavailable }) }

    let private publishedInquiryState (states: Current) inquiryId : Result<InquiryState option, CurrentError> =
        match Map.tryFind inquiryId states.Histories with
        | None -> Ok None
        | Some history when Set.count history.Heads = 1 ->
            history.Accepted
            |> Map.tryFind (Set.minElement history.Heads)
            |> Option.map (fun accepted -> accepted.State)
            |> Ok
        | Some history ->
            let heads =
                history.Heads
                |> Set.toList
                |> List.map (Wanxiangshu.Sphinx.V2.Core.EventId.value >> Identity.EventId.create)

            Error(
                CurrentError.DomainConflict(
                    DomainConflict.ConcurrentHeads(EventStreamId.create (streamOf inquiryId), heads)
                )
            )

    /// Missing is distinct from invalid/cut and from multiple legitimate heads.
    /// There is no resolution command: a fork remains observable, not a chosen winner.
    let tryState (current: obj) (inquiryId: InquiryId) : Result<InquiryState option, CurrentError> =
        let states = if isNull current then empty else unbox<Current> current

        match Map.tryFind (streamOf inquiryId) states.Unavailable with
        | Some reason -> Error(CurrentError.SemanticRejected reason)
        | None -> publishedInquiryState states inquiryId

    let private traceFailure reason =
        Error(CurrentError.SemanticRejected("accepted canonical trace is unavailable: " + reason))

    let private acceptedEnvelope (history: InquiryHistory) inquiryId expectedRevision eventId =
        let verify (accepted: AcceptedTransition) =
            let envelope = accepted.Envelope

            if
                accepted.State.Id <> inquiryId
                || accepted.State.EventHead <> Some eventId
                || accepted.State.Revision <> expectedRevision
                || Identity.EventId.value envelope.EventId
                   <> Wanxiangshu.Sphinx.V2.Core.EventId.value eventId
                || EventStreamId.value envelope.StreamId <> streamOf inquiryId
            then
                traceFailure "a cached envelope does not match its accepted state"
            else
                Ok envelope

        history.Accepted
        |> Map.tryFind eventId
        |> Option.map verify
        |> Option.defaultValue (traceFailure "an accepted parent is missing from Current")

    let private traceParent revision (envelope: EventEnvelope) =
        match envelope.Parents with
        | [] when revision = Revision.origin -> Ok None
        | [ parent ] when Revision.value revision > 0L ->
            Wanxiangshu.Sphinx.V2.Core.EventId.tryCreate (Identity.EventId.value parent)
            |> Result.mapError CurrentError.SemanticRejected
            |> Result.bind (fun parent ->
                Revision.tryCreate (Revision.value revision - 1L)
                |> Result.mapError CurrentError.SemanticRejected
                |> Result.map (fun previousRevision -> Some(parent, previousRevision)))
        | _ -> traceFailure "the accepted parent chain has no unique origin"

    let private traceStep history inquiryId visited revision eventId =
        if Set.contains eventId visited then
            traceFailure "the accepted parent chain contains a cycle"
        else
            acceptedEnvelope history inquiryId revision eventId
            |> Result.bind (fun envelope ->
                traceParent revision envelope |> Result.map (fun parent -> envelope, parent))

    let private acceptedTrace (history: InquiryHistory) (state: InquiryState) =
        let rec collect visited revision eventId envelopes =
            traceStep history state.Id visited revision eventId
            |> Result.bind (fun (envelope, parent) ->
                let envelopes = envelope :: envelopes

                match parent with
                | None -> Ok envelopes
                | Some(parentId, previousRevision) ->
                    collect (Set.add eventId visited) previousRevision parentId envelopes)

        match state.EventHead with
        | None -> traceFailure "the published inquiry has no accepted head"
        | Some head -> collect Set.empty state.Revision head []

    /// The single-envelope oracle captures these envelopes only after their transition
    /// is accepted. Queries use this same Current, including after canonical cold replay.
    let tryTrace (current: obj) (inquiryId: InquiryId) : Result<EventEnvelope list option, CurrentError> =
        tryState current inquiryId
        |> Result.bind (function
            | None -> Ok None
            | Some state ->
                let states = unbox<Current> current

                match Map.tryFind inquiryId states.Histories with
                | None -> traceFailure "the published inquiry has no accepted transitions"
                | Some history -> acceptedTrace history state |> Result.map Some)
