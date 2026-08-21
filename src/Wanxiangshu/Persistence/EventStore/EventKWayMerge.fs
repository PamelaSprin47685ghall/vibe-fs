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

    type private WriterStream =
        { WriterId: string
          Events: EventEnvelope array }

    type private CursorReadiness =
        | Dormant
        | Waiting of missingParentCount: int
        | Queued
        | Exhausted

    type private Cursor =
        { Offset: int
          Readiness: CursorReadiness }

    type private WaiterToken = { CursorIndex: int; Offset: int }

    type private ReadyCursor =
        { CursorIndex: int
          EventKey: string
          WriterId: string }

    type private HeadDisposition =
        | AlreadySeen
        | ReadyNow
        | BlockedOn of parentKeys: string list

    type private MergeProgress =
        | Continue
        | Finished of Result<EventEnvelope list, StorageInvalid>

    let private currentHead (writers: WriterStream array) (cursors: Cursor array) index =
        let writer = writers.[index]
        let cursor = cursors.[index]

        if cursor.Offset < writer.Events.Length then
            Some writer.Events.[cursor.Offset]
        else
            None

    let private allPendingIds (writers: WriterStream array) (cursors: Cursor array) =
        let pending = HashSet<string>()

        writers
        |> Array.iteri (fun index writer ->
            let writer = writers.[index]
            let cursor = cursors.[index]

            writer.Events
            |> Array.skip cursor.Offset
            |> Array.iter (fun envelope -> pending.Add(eventKey envelope.EventId) |> ignore))

        pending

    let private allKnownIds (writers: WriterStream array) =
        let known = HashSet<string>()

        writers
        |> Array.collect _.Events
        |> Array.iter (fun envelope -> known.Add(eventKey envelope.EventId) |> ignore)

        known

    let private strictFrontierError
        (seen: Dictionary<string, EventEnvelope>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        =
        let pendingIds = allPendingIds writers cursors

        let missing =
            [ 0 .. writers.Length - 1 ]
            |> List.choose (currentHead writers cursors)
            |> List.collect (fun head -> head.Parents)
            |> List.filter (fun parent ->
                let key = eventKey parent
                not (seen.ContainsKey key) && not (pendingIds.Contains key))
            |> List.sortBy eventKey
            |> List.tryHead

        match missing with
        | Some parent -> StorageInvalid.MissingParent parent
        | None -> StorageInvalid.NonCanonical "writer-stream order has a cyclic or backward causal frontier"

    let private frontierError
        allowExternalParents
        (seen: Dictionary<string, EventEnvelope>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        =
        if allowExternalParents then
            StorageInvalid.NonCanonical "retained writer-stream order has a cyclic or backward causal frontier"
        else
            strictFrontierError seen writers cursors

    let private compareReadyCursor left right =
        let byEvent = compare left.EventKey right.EventKey

        if byEvent <> 0 then
            byEvent
        else
            compare left.WriterId right.WriterId

    let private swapHeap (heap: ResizeArray<ReadyCursor>) left right =
        let value = heap.[left]
        heap.[left] <- heap.[right]
        heap.[right] <- value

    let private heapPush (heap: ResizeArray<ReadyCursor>) readyCursor =
        heap.Add readyCursor

        let parentOf child =
            if child > 0 then Some((child - 1) / 2) else None

        let rec bubble child =
            match parentOf child with
            | Some parent when compareReadyCursor heap.[child] heap.[parent] < 0 ->
                swapHeap heap child parent
                bubble parent
            | _ -> ()

        bubble (heap.Count - 1)

    let private smallerChild (heap: ResizeArray<ReadyCursor>) parent =
        let left = parent * 2 + 1
        let right = left + 1

        match left < heap.Count, right < heap.Count with
        | false, _ -> None
        | true, true when compareReadyCursor heap.[right] heap.[left] < 0 -> Some right
        | true, _ -> Some left

    let rec private sinkHeapRoot (heap: ResizeArray<ReadyCursor>) parent =
        match smallerChild heap parent with
        | Some child when compareReadyCursor heap.[child] heap.[parent] < 0 ->
            swapHeap heap child parent
            sinkHeapRoot heap child
        | _ -> ()

    let private restoreHeapRoot (heap: ResizeArray<ReadyCursor>) last =
        if heap.Count > 0 then
            heap.[0] <- last
            sinkHeapRoot heap 0

    let private heapPop (heap: ResizeArray<ReadyCursor>) =
        let first = heap.[0]
        let lastIndex = heap.Count - 1
        let last = heap.[lastIndex]
        heap.RemoveAt lastIndex
        restoreHeapRoot heap last
        first

    let private addWaiter (waiters: Dictionary<string, ResizeArray<WaiterToken>>) key token =
        match waiters.TryGetValue key with
        | true, entries -> entries.Add token
        | false, _ ->
            let entries = ResizeArray<WaiterToken>()
            entries.Add token
            waiters.Add(key, entries)

    let private queueCursor
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        index
        =
        let cursor = cursors.[index]

        match cursor.Readiness, currentHead writers cursors index with
        | CursorReadiness.Queued, _ -> ()
        | _, None ->
            cursors.[index] <-
                { cursor with
                    Readiness = CursorReadiness.Exhausted }
        | _, Some head ->
            cursors.[index] <-
                { cursor with
                    Readiness = CursorReadiness.Queued }

            heapPush
                heap
                { CursorIndex = index
                  EventKey = eventKey head.EventId
                  WriterId = writers.[index].WriterId }

    let private missingParentKeys
        allowExternalParents
        (knownIds: HashSet<string>)
        (seen: Dictionary<string, EventEnvelope>)
        (head: EventEnvelope)
        =
        head.Parents
        |> List.map eventKey
        |> List.filter (fun parentKey ->
            not (seen.ContainsKey parentKey)
            && (not allowExternalParents || knownIds.Contains parentKey))
        |> Set.ofList
        |> Set.toList

    let private classifyUnseenHead
        allowExternalParents
        (knownIds: HashSet<string>)
        (seen: Dictionary<string, EventEnvelope>)
        head
        =
        match missingParentKeys allowExternalParents knownIds seen head with
        | [] -> HeadDisposition.ReadyNow
        | missing -> HeadDisposition.BlockedOn missing

    let private classifyHead
        allowExternalParents
        (knownIds: HashSet<string>)
        (seen: Dictionary<string, EventEnvelope>)
        head
        =
        if seen.ContainsKey(eventKey head.EventId) then
            HeadDisposition.AlreadySeen
        else
            classifyUnseenHead allowExternalParents knownIds seen head

    let private blockCursor
        (parentWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (duplicateWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (cursors: Cursor array)
        index
        eventId
        (parentKeys: string list)
        =
        let cursor = cursors.[index]

        let token =
            { CursorIndex = index
              Offset = cursor.Offset }

        cursors.[index] <-
            { cursor with
                Readiness = CursorReadiness.Waiting parentKeys.Length }

        parentKeys
        |> List.iter (fun parentKey -> addWaiter parentWaiters parentKey token)

        addWaiter duplicateWaiters (eventKey eventId) token

    let private scheduleHead
        allowExternalParents
        (knownIds: HashSet<string>)
        (seen: Dictionary<string, EventEnvelope>)
        (parentWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (duplicateWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        index
        (head: EventEnvelope)
        =
        match classifyHead allowExternalParents knownIds seen head with
        | HeadDisposition.AlreadySeen
        | HeadDisposition.ReadyNow -> queueCursor writers cursors heap index
        | HeadDisposition.BlockedOn parentKeys ->
            blockCursor parentWaiters duplicateWaiters cursors index head.EventId parentKeys

    let private scheduleCursor
        allowExternalParents
        (knownIds: HashSet<string>)
        (seen: Dictionary<string, EventEnvelope>)
        (parentWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (duplicateWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        index
        =
        let cursor = cursors.[index]

        match currentHead writers cursors index with
        | None ->
            cursors.[index] <-
                { cursor with
                    Readiness = CursorReadiness.Exhausted }
        | Some head ->
            scheduleHead
                allowExternalParents
                knownIds
                seen
                parentWaiters
                duplicateWaiters
                writers
                cursors
                heap
                index
                head

    let private wakeParentToken
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        (token: WaiterToken)
        =
        let cursor = cursors.[token.CursorIndex]

        match cursor.Offset = token.Offset, cursor.Readiness with
        | true, CursorReadiness.Waiting 1 -> queueCursor writers cursors heap token.CursorIndex
        | true, CursorReadiness.Waiting count when count > 1 ->
            cursors.[token.CursorIndex] <-
                { cursor with
                    Readiness = CursorReadiness.Waiting(count - 1) }
        | _ -> ()

    let private wakeParent
        (parentWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        key
        =
        match parentWaiters.TryGetValue key with
        | false, _ -> ()
        | true, entries ->
            parentWaiters.Remove key |> ignore
            entries |> Seq.iter (wakeParentToken writers cursors heap)

    let private wakeDuplicateToken
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        (token: WaiterToken)
        =
        let cursor = cursors.[token.CursorIndex]

        match cursor.Offset = token.Offset, cursor.Readiness with
        | true, CursorReadiness.Waiting _ -> queueCursor writers cursors heap token.CursorIndex
        | _ -> ()

    let private wakeDuplicate
        (duplicateWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        key
        =
        match duplicateWaiters.TryGetValue key with
        | false, _ -> ()
        | true, entries ->
            duplicateWaiters.Remove key |> ignore
            entries |> Seq.iter (wakeDuplicateToken writers cursors heap)

    let private validateReadyHead (writers: WriterStream array) (cursors: Cursor array) (readyCursor: ReadyCursor) =
        match currentHead writers cursors readyCursor.CursorIndex with
        | None -> Error(StorageInvalid.NonCanonical "k-way ready cursor has no current event")
        | Some head when readyCursor.EventKey <> eventKey head.EventId ->
            Error(StorageInvalid.NonCanonical "k-way ready cursor no longer names its current event")
        | Some head -> Ok head

    let private acceptHead
        allowExternalParents
        (knownIds: HashSet<string>)
        (seen: Dictionary<string, EventEnvelope>)
        (parentWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (duplicateWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        (ordered: ResizeArray<EventEnvelope>)
        index
        head
        =
        let key = eventKey head.EventId

        match seen.TryGetValue key with
        | true, existing ->
            CanonicalEventCodec.checkIdentity existing head
            |> Result.map (fun () ->
                scheduleCursor
                    allowExternalParents
                    knownIds
                    seen
                    parentWaiters
                    duplicateWaiters
                    writers
                    cursors
                    heap
                    index)
        | false, _ ->
            seen.Add(key, head)
            ordered.Add head
            wakeParent parentWaiters writers cursors heap key
            wakeDuplicate duplicateWaiters writers cursors heap key

            scheduleCursor allowExternalParents knownIds seen parentWaiters duplicateWaiters writers cursors heap index

            Ok()

    let private advanceCursor
        allowExternalParents
        (knownIds: HashSet<string>)
        (seen: Dictionary<string, EventEnvelope>)
        (parentWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (duplicateWaiters: Dictionary<string, ResizeArray<WaiterToken>>)
        (writers: WriterStream array)
        (cursors: Cursor array)
        (heap: ResizeArray<ReadyCursor>)
        (ordered: ResizeArray<EventEnvelope>)
        (readyCursor: ReadyCursor)
        : Result<unit, StorageInvalid> =
        let index = readyCursor.CursorIndex
        let cursor = cursors.[index]

        validateReadyHead writers cursors readyCursor
        |> Result.bind (fun head ->
            cursors.[index] <-
                { Offset = cursor.Offset + 1
                  Readiness = CursorReadiness.Dormant }

            acceptHead
                allowExternalParents
                knownIds
                seen
                parentWaiters
                duplicateWaiters
                writers
                cursors
                heap
                ordered
                index
                head)

    /// Preserve each writer's append order. Among currently causally-ready heads,
    /// EventId text is the deterministic tie-break; writer name only breaks an
    /// impossible same-id/same-bytes duplicate tie.
    let private mergeCore
        allowExternalParents
        (streams: (string * EventEnvelope list) list)
        : Result<EventEnvelope list, StorageInvalid> =
        // Collapse duplicate writer keys exactly as the previous Map-backed
        // implementation did. Writer bytes are immutable; only the current
        // cursor snapshot is replaced while the heap stays O(log writers).
        let writers =
            streams
            |> Map.ofList
            |> Map.toList
            |> List.map (fun (writerId, events) ->
                { WriterId = writerId
                  Events = List.toArray events })
            |> List.toArray

        let cursors =
            Array.init writers.Length (fun _ ->
                { Offset = 0
                  Readiness = CursorReadiness.Dormant })

        let knownIds = allKnownIds writers

        let seen = Dictionary<string, EventEnvelope>()
        let parentWaiters = Dictionary<string, ResizeArray<WaiterToken>>()
        let duplicateWaiters = Dictionary<string, ResizeArray<WaiterToken>>()
        let ready = ResizeArray<ReadyCursor>()
        let ordered = ResizeArray<EventEnvelope>()

        // DSL-MUTABLE: algorithm-scratch — one finite loop verdict; per-writer state is immutable data.
        let mutable progress = MergeProgress.Continue

        for index = 0 to cursors.Length - 1 do
            scheduleCursor allowExternalParents knownIds seen parentWaiters duplicateWaiters writers cursors ready index

        let advance () =
            if ready.Count > 0 then
                let readyCursor = heapPop ready

                match
                    advanceCursor
                        allowExternalParents
                        knownIds
                        seen
                        parentWaiters
                        duplicateWaiters
                        writers
                        cursors
                        ready
                        ordered
                        readyCursor
                with
                | Ok() -> ()
                | Error invalid -> progress <- MergeProgress.Finished(Error invalid)
            elif
                [ 0 .. writers.Length - 1 ]
                |> List.exists (fun index -> currentHead writers cursors index |> Option.isSome)
            then
                progress <- MergeProgress.Finished(Error(frontierError allowExternalParents seen writers cursors))
            else
                progress <- MergeProgress.Finished(Ok(ordered |> Seq.toList))

        let isContinuing () =
            match progress with
            | MergeProgress.Continue -> true
            | MergeProgress.Finished _ -> false

        while isContinuing () do
            advance ()

        match progress with
        | MergeProgress.Finished result -> result
        | MergeProgress.Continue -> Error(StorageInvalid.NonCanonical "k-way merge exited without a terminal result")

    /// Strict merge for complete histories. Any parent absent from the supplied
    /// writer set is storage corruption.
    let merge (streams: (string * EventEnvelope list) list) : Result<EventEnvelope list, StorageInvalid> =
        mergeCore false streams

    /// Retention-window merge. A parent absent from the entire retained set is a
    /// causal predecessor before the truncation boundary and is considered
    /// satisfied. Dependencies that are present inside the retained set still
    /// participate in ordering and cycle detection.
    let mergeRetained (streams: (string * EventEnvelope list) list) : Result<EventEnvelope list, StorageInvalid> =
        mergeCore true streams
