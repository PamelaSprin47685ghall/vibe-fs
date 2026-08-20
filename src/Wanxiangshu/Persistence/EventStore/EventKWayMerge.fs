namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence
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
open System.Collections.Generic

/// DURABLE-CONVERGENCE-001..003 / DURABLE-EVENTS-014.
/// Pure structural k-way merge over ordered writer streams. It owns no business
/// projection and reads no files; callers provide already-decoded streams.
[<RequireQualifiedAccess>]
module EventKWayMerge =

    let private eventKey (eventId: EventId) = EventId.value eventId

    type private WriterCursor =
        { WriterId: string
          mutable Remaining: EventEnvelope list
          mutable Generation: int
          mutable MissingParents: int
          mutable Queued: bool }

    let private allPendingIds (cursors: WriterCursor array) =
        let pending = HashSet<string>()

        for cursor in cursors do
            for envelope in cursor.Remaining do
                pending.Add(eventKey envelope.EventId) |> ignore

        pending

    let private earlierParent (candidate: EventId) (current: EventId option) =
        match current with
        | None -> Some candidate
        | Some existing when compare (eventKey candidate) (eventKey existing) < 0 -> Some candidate
        | Some _ -> current

    let private frontierError (seen: Dictionary<string, EventEnvelope>) (cursors: WriterCursor array) =
        let pendingIds = allPendingIds cursors
        let mutable missing: EventId option = None

        for cursor in cursors do
            match cursor.Remaining with
            | head :: _ ->
                for parent in head.Parents do
                    let key = eventKey parent

                    if not (seen.ContainsKey key) && not (pendingIds.Contains key) then
                        missing <- earlierParent parent missing
            | [] -> ()

        match missing with
        | Some parent -> StorageInvalid.MissingParent parent
        | None -> StorageInvalid.NonCanonical "writer-stream order has a cyclic or backward causal frontier"

    let private compareQueuedCursor (cursors: WriterCursor array) left right =
        let leftCursor = cursors.[left]
        let rightCursor = cursors.[right]

        match leftCursor.Remaining, rightCursor.Remaining with
        | leftHead :: _, rightHead :: _ ->
            let byEvent = compare (eventKey leftHead.EventId) (eventKey rightHead.EventId)

            if byEvent <> 0 then
                byEvent
            else
                compare leftCursor.WriterId rightCursor.WriterId
        | _ -> 0

    let private swapHeap (heap: ResizeArray<int>) left right =
        let value = heap.[left]
        heap.[left] <- heap.[right]
        heap.[right] <- value

    let private heapPush (cursors: WriterCursor array) (heap: ResizeArray<int>) index =
        heap.Add index

        let rec bubble child =
            if child > 0 then
                let parent = (child - 1) / 2

                if compareQueuedCursor cursors heap.[child] heap.[parent] < 0 then
                    swapHeap heap child parent
                    bubble parent

        bubble (heap.Count - 1)

    let private heapPop (cursors: WriterCursor array) (heap: ResizeArray<int>) =
        let first = heap.[0]
        let lastIndex = heap.Count - 1
        let last = heap.[lastIndex]
        heap.RemoveAt lastIndex

        if heap.Count > 0 then
            heap.[0] <- last

            let rec sink parent =
                let left = parent * 2 + 1

                if left < heap.Count then
                    let right = left + 1

                    let smaller =
                        if right < heap.Count && compareQueuedCursor cursors heap.[right] heap.[left] < 0 then
                            right
                        else
                            left

                    if compareQueuedCursor cursors heap.[smaller] heap.[parent] < 0 then
                        swapHeap heap smaller parent
                        sink smaller

            sink 0

        first

    let private addWaiter
        (waiters: Dictionary<string, ResizeArray<int * int>>)
        key
        (cursorIndex: int, generation: int)
        =
        match waiters.TryGetValue key with
        | true, entries -> entries.Add(cursorIndex, generation)
        | false, _ ->
            let entries = ResizeArray<int * int>()
            entries.Add(cursorIndex, generation)
            waiters.Add(key, entries)

    let private queueCursor (cursors: WriterCursor array) (heap: ResizeArray<int>) index =
        let cursor = cursors.[index]

        if not cursor.Queued then
            cursor.Queued <- true
            heapPush cursors heap index

    let private scheduleCursor
        (seen: Dictionary<string, EventEnvelope>)
        (parentWaiters: Dictionary<string, ResizeArray<int * int>>)
        (duplicateWaiters: Dictionary<string, ResizeArray<int * int>>)
        (cursors: WriterCursor array)
        (heap: ResizeArray<int>)
        index
        =
        let cursor = cursors.[index]

        match cursor.Remaining with
        | [] ->
            cursor.MissingParents <- 0
            cursor.Queued <- false
        | head :: _ ->
            let key = eventKey head.EventId

            if seen.ContainsKey key then
                cursor.MissingParents <- 0
                queueCursor cursors heap index
            else
                let missing = HashSet<string>()

                for parent in head.Parents do
                    let parentKey = eventKey parent

                    if not (seen.ContainsKey parentKey) then
                        missing.Add parentKey |> ignore

                cursor.MissingParents <- missing.Count

                if cursor.MissingParents = 0 then
                    queueCursor cursors heap index
                else
                    let token = index, cursor.Generation

                    for parentKey in missing do
                        addWaiter parentWaiters parentKey token

                    // A same-id head is ready immediately once another writer
                    // establishes that identity, even if this candidate would
                    // otherwise still be blocked on parents. This preserves the
                    // previous collision/duplicate ordering exactly.
                    addWaiter duplicateWaiters key token

    let private wakeParent
        (parentWaiters: Dictionary<string, ResizeArray<int * int>>)
        (cursors: WriterCursor array)
        (heap: ResizeArray<int>)
        key
        =
        match parentWaiters.TryGetValue key with
        | false, _ -> ()
        | true, entries ->
            parentWaiters.Remove key |> ignore

            for cursorIndex, generation in entries do
                let cursor = cursors.[cursorIndex]

                if cursor.Generation = generation && not cursor.Queued && cursor.MissingParents > 0 then
                    cursor.MissingParents <- cursor.MissingParents - 1

                    if cursor.MissingParents = 0 then
                        queueCursor cursors heap cursorIndex

    let private wakeDuplicate
        (duplicateWaiters: Dictionary<string, ResizeArray<int * int>>)
        (cursors: WriterCursor array)
        (heap: ResizeArray<int>)
        key
        =
        match duplicateWaiters.TryGetValue key with
        | false, _ -> ()
        | true, entries ->
            duplicateWaiters.Remove key |> ignore

            for cursorIndex, generation in entries do
                let cursor = cursors.[cursorIndex]

                if cursor.Generation = generation && not cursor.Queued then
                    cursor.MissingParents <- 0
                    queueCursor cursors heap cursorIndex

    let private advanceCursor
        (seen: Dictionary<string, EventEnvelope>)
        (parentWaiters: Dictionary<string, ResizeArray<int * int>>)
        (duplicateWaiters: Dictionary<string, ResizeArray<int * int>>)
        (cursors: WriterCursor array)
        (heap: ResizeArray<int>)
        (ordered: ResizeArray<EventEnvelope>)
        index
        : Result<unit, StorageInvalid> =
        let cursor = cursors.[index]
        cursor.Queued <- false

        match cursor.Remaining with
        | [] -> Ok()
        | head :: tail ->
            cursor.Remaining <- tail
            cursor.Generation <- cursor.Generation + 1
            cursor.MissingParents <- 0
            let key = eventKey head.EventId

            match seen.TryGetValue key with
            | true, existing ->
                CanonicalEventCodec.checkIdentity existing head
                |> Result.map (fun () -> scheduleCursor seen parentWaiters duplicateWaiters cursors heap index)
            | false, _ ->
                seen.Add(key, head)
                ordered.Add head
                wakeParent parentWaiters cursors heap key
                wakeDuplicate duplicateWaiters cursors heap key
                scheduleCursor seen parentWaiters duplicateWaiters cursors heap index
                Ok()

    /// Preserve each writer's append order. Among currently causally-ready heads,
    /// EventId text is the deterministic tie-break; writer name only breaks an
    /// impossible same-id/same-bytes duplicate tie.
    let merge (streams: (string * EventEnvelope list) list) : Result<EventEnvelope list, StorageInvalid> =
        // Collapse duplicate writer keys exactly as the previous Map-backed
        // implementation did, but keep the hot loop on mutable cursors. The old
        // loop rebuilt/filter/sorted every writer head for every emitted event;
        // with tens of thousands of events that dominated startup and hook sync.
        let cursors =
            streams
            |> Map.ofList
            |> Map.toList
            |> List.map (fun (writerId, events) ->
                { WriterId = writerId
                  Remaining = events
                  Generation = 0
                  MissingParents = 0
                  Queued = false })
            |> List.toArray

        // DSL-MUTABLE: algorithm-scratch — stack depth and allocation must not scale
        // with history * writer-count on the k-way hot path.
        let seen = Dictionary<string, EventEnvelope>()
        let parentWaiters = Dictionary<string, ResizeArray<int * int>>()
        let duplicateWaiters = Dictionary<string, ResizeArray<int * int>>()
        let ready = ResizeArray<int>()
        let ordered = ResizeArray<EventEnvelope>()
        let mutable outcome: Result<EventEnvelope list, StorageInvalid> option = None

        for index = 0 to cursors.Length - 1 do
            scheduleCursor seen parentWaiters duplicateWaiters cursors ready index

        let advance () =
            if ready.Count > 0 then
                let readyIndex = heapPop cursors ready

                match advanceCursor seen parentWaiters duplicateWaiters cursors ready ordered readyIndex with
                | Ok() -> ()
                | Error invalid -> outcome <- Some(Error invalid)
            elif cursors |> Array.exists (fun cursor -> not (List.isEmpty cursor.Remaining)) then
                outcome <- Some(Error(frontierError seen cursors))
            else
                outcome <- Some(Ok(ordered |> Seq.toList))

        while outcome.IsNone do
            advance ()

        outcome.Value
