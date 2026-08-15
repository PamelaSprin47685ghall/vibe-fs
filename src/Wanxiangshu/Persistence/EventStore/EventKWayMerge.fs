namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity

/// DURABLE-CONVERGENCE-001..003 / DURABLE-EVENTS-014.
/// Pure structural k-way merge over ordered writer streams. It owns no business
/// projection and reads no files; callers provide already-decoded streams.
[<RequireQualifiedAccess>]
module EventKWayMerge =

    let private eventKey (eventId: EventId) = EventId.value eventId

    let private allPendingIds (queues: Map<string, EventEnvelope list>) =
        queues
        |> Map.toSeq
        |> Seq.collect (snd >> Seq.map (fun event -> eventKey event.EventId))
        |> Set.ofSeq

    let private collisionOrDuplicate
        (seen: Map<string, EventEnvelope>)
        (candidate: EventEnvelope)
        : Result<bool, StorageInvalid> =
        let key = eventKey candidate.EventId

        match Map.tryFind key seen with
        | None -> Ok false
        | Some existing ->
            CanonicalEventCodec.checkIdentity existing candidate
            |> Result.map (fun () -> true)

    let private readyHead (seen: Map<string, EventEnvelope>) (writerId: string) (events: EventEnvelope list) =
        match events with
        | head :: _ when
            Map.containsKey (eventKey head.EventId) seen
            || List.forall (fun parent -> Map.containsKey (eventKey parent) seen) head.Parents
            ->
            Some(writerId, head)
        | _ -> None

    let private frontierError
        (seen: Map<string, EventEnvelope>)
        (remaining: Map<string, EventEnvelope list>)
        (nonEmpty: (string * EventEnvelope list) list)
        =
        let pendingIds = allPendingIds remaining

        let missing =
            nonEmpty
            |> List.collect (fun (_, events) ->
                match events with
                | [] -> []
                | head :: _ -> head.Parents)
            |> List.filter (fun parent ->
                let key = eventKey parent
                not (Map.containsKey key seen) && not (Set.contains key pendingIds))
            |> List.sortBy eventKey
            |> List.tryHead

        match missing with
        | Some parent -> Error(StorageInvalid.MissingParent parent)
        | None -> Error(StorageInvalid.NonCanonical "writer-stream order has a cyclic or backward causal frontier")

    let private advance
        (remaining: Map<string, EventEnvelope list>)
        (seen: Map<string, EventEnvelope>)
        (acc: EventEnvelope list)
        (writerId: string)
        (head: EventEnvelope)
        =
        result {
            let! duplicate = collisionOrDuplicate seen head
            let nextQueues = Map.add writerId (List.tail remaining.[writerId]) remaining

            if duplicate then
                return nextQueues, seen, acc
            else
                return nextQueues, Map.add (eventKey head.EventId) head seen, head :: acc
        }

    let private nonEmptyQueues (remaining: Map<string, EventEnvelope list>) =
        remaining
        |> Map.toList
        |> List.filter (fun (_, events) -> not (List.isEmpty events))

    /// Preserve each writer's append order. Among currently causally-ready heads,
    /// EventId text is the deterministic tie-break; writer name only breaks an
    /// impossible same-id/same-bytes duplicate tie.
    let merge (streams: (string * EventEnvelope list) list) : Result<EventEnvelope list, StorageInvalid> =
        let queues = streams |> Map.ofList

        let rec loop
            (remaining: Map<string, EventEnvelope list>)
            (seen: Map<string, EventEnvelope>)
            (acc: EventEnvelope list)
            =
            match nonEmptyQueues remaining with
            | [] -> Ok(List.rev acc)
            | nonEmpty -> stepReady remaining seen acc nonEmpty

        and stepReady
            (remaining: Map<string, EventEnvelope list>)
            (seen: Map<string, EventEnvelope>)
            (acc: EventEnvelope list)
            (nonEmpty: (string * EventEnvelope list) list)
            =
            result {
                let ready =
                    nonEmpty
                    |> List.choose (fun (writerId, events) -> readyHead seen writerId events)
                    |> List.sortBy (fun (writerId, event) -> eventKey event.EventId, writerId)

                match ready with
                | (writerId, head) :: _ ->
                    let! nextQueues, nextSeen, nextAcc = advance remaining seen acc writerId head
                    return! loop nextQueues nextSeen nextAcc
                | [] -> return! frontierError seen remaining nonEmpty
            }

        loop queues Map.empty []
