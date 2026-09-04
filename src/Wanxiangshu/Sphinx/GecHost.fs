namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Sphinx.Core

/// WHAT[EPI-027]: OpenCode dispatch planning over the existing managed-session
/// owner (delegation/fission, capacity, failure policy). Sphinx only describes
/// which blind child to fork: common root snapshot, no sibling payload, a new
/// child per retry without failure output, abort/drain intents, depth exactly
/// one. Sphinx keeps no private pool of its own.
///
/// WHAT[EPI-018]: host-equivalence fold over the canonical accepted-event list.
/// Host-private session ids, transport receipts and arrival timing ride in the
/// outer arguments and never enter the semantic hash.
module GecHost =

    [<RequireQualifiedAccess>]
    type HostFault =
        | MissingWorkId
        | MissingAttempt
        | MissingSnapshotHash
        | MissingParentSession
        | MissingChildSession
        | MissingEvents
        | DepthExceeded of depth: int
        | ParentChainBreak of detail: string

    let code (fault: HostFault) : string =
        match fault with
        | HostFault.MissingWorkId -> "missing-work-id"
        | HostFault.MissingAttempt -> "missing-attempt"
        | HostFault.MissingSnapshotHash -> "missing-snapshot-hash"
        | HostFault.MissingParentSession -> "missing-parent-session"
        | HostFault.MissingChildSession -> "missing-child-session"
        | HostFault.MissingEvents -> "missing-events"
        | HostFault.DepthExceeded _ -> "DEPTH_EXCEEDED"
        | HostFault.ParentChainBreak _ -> "parent-chain-break"

    let message (fault: HostFault) : string =
        match fault with
        | HostFault.MissingWorkId -> "dispatch requires a non-blank workId"
        | HostFault.MissingAttempt -> "dispatch requires an attempt number"
        | HostFault.MissingSnapshotHash -> "dispatch requires rootSnapshot.hash"
        | HostFault.MissingParentSession -> "dispatch requires parentSession.sessionId"
        | HostFault.MissingChildSession -> "abort requires the childSessionId to terminate"
        | HostFault.MissingEvents -> "fold requires the canonical event list"
        | HostFault.DepthExceeded depth ->
            sprintf "dispatch depth %d exceeds the single blind-child level; workers cannot recurse" depth
        | HostFault.ParentChainBreak detail -> sprintf "fold arrivals break the parent chain: %s" detail

    type private DispatchPlan =
        { ParentSessionId: string
          SnapshotHash: string
          Depth: int
          ChildSessionId: string }

    let private isUndefined (value: obj) : bool = emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) : bool = isNull value || isUndefined value

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private faultView (fault: HostFault) : obj =
        box
            {| ok = false
               error =
                box
                    {| code = code fault
                       message = message fault |} |}

    let private requireText (fault: HostFault) (value: string) : Result<string, HostFault> =
        if String.IsNullOrWhiteSpace value then
            Error fault
        else
            Ok value

    let private childSessionFor (workId: string) (attempt: int) (snapshotHash: string) : string =
        CoreHash.sha256Hex (workId + "|" + string attempt + "|" + snapshotHash)

    let private planChild
        (workId: string)
        (attempt: int)
        (snapshotHash: string)
        (parentSessionId: string)
        (depth: int)
        : Result<DispatchPlan, HostFault> =
        result {
            let! wid = requireText HostFault.MissingWorkId workId
            let! snap = requireText HostFault.MissingSnapshotHash snapshotHash
            let! parent = requireText HostFault.MissingParentSession parentSessionId

            if depth > 1 then
                return! Error(HostFault.DepthExceeded depth)
            else
                return
                    { ParentSessionId = parent
                      SnapshotHash = snap
                      Depth = 1
                      ChildSessionId = childSessionFor wid attempt snap }
        }

    let private childView (plan: DispatchPlan) : obj =
        box
            {| parentSessionId = plan.ParentSessionId
               snapshotHash = plan.SnapshotHash
               depth = plan.Depth
               carriesSiblingPayload = false
               carriesFailureOutput = false
               childSessionId = plan.ChildSessionId |}

    let private renderPlan (planned: Result<DispatchPlan, HostFault>) : obj =
        match planned with
        | Ok(plan: DispatchPlan) -> box {| ok = true; child = childView plan |}
        | Error(fault: HostFault) -> faultView fault

    let private snapshotHashOf (input: obj) : string =
        let snap: obj = input?rootSnapshot
        if isNullish snap then "" else text snap?hash

    let private parentSessionOf (input: obj) : string =
        let parent: obj = input?parentSession
        if isNullish parent then "" else text parent?sessionId

    let private intField (fallback: int) (value: obj) : int =
        if isNullish value then fallback else unbox<int> value

    let private depthOf (input: obj) : int =
        let depthRaw: obj = input?depth
        intField 1 depthRaw

    // Siblings are accepted but never read: the child carries no sibling payload
    // by construction, so nothing from a sibling branch can leak into the plan.
    let planOpenCodeDispatch (input: obj) : obj =
        let attemptRaw: obj = input?attempt

        let attempt = if isNullish attemptRaw then -1 else unbox<int> attemptRaw

        let planned: Result<DispatchPlan, HostFault> =
            if attempt < 0 then
                Error HostFault.MissingAttempt
            else
                planChild (text input?workId) attempt (snapshotHashOf input) (parentSessionOf input) (depthOf input)

        renderPlan planned

    // failedOutput is accepted but never read: the retry forks the ORIGINAL root
    // snapshot under the next attempt, so failure output never propagates.
    let planOpenCodeRetry (input: obj) : obj =
        let nextRaw: obj = input?nextAttempt

        let planned: Result<DispatchPlan, HostFault> =
            if isNullish nextRaw then
                Error HostFault.MissingAttempt
            else
                planChild
                    (text input?workId)
                    (unbox<int> nextRaw)
                    (snapshotHashOf input)
                    (parentSessionOf input)
                    (depthOf input)

        renderPlan planned

    let abortOpenCodeWork (input: obj) : obj =
        result {
            let! wid = requireText HostFault.MissingWorkId (text input?workId)
            let! cid = requireText HostFault.MissingChildSession (text input?childSessionId)

            return
                box
                    {| ok = true
                       aborted = true
                       workId = wid
                       childSessionId = cid |}
        }
        |> function
            | Ok view -> view
            | Error fault -> faultView fault

    let drainOpenCodeHost (input: obj) : obj =
        box
            {| ok = true
               drained = true
               pending = 0
               parentSessionId = text input?parentSessionId |}

    let private parentOf (event: obj) : string option =
        let raw: obj = event?parent

        if isNullish raw then
            None
        else
            let value = text raw
            if value = "" || value = "none" then None else Some value

    let private checkedEventId (index: int) (event: obj) : Result<string, HostFault> =
        let id = text event?eventId

        if String.IsNullOrWhiteSpace id then
            Error(HostFault.ParentChainBreak(sprintf "event at index %d carries no eventId" index))
        else
            Ok id

    let private checkedParent
        (index: int)
        (id: string)
        (prev: string option)
        (event: obj)
        : Result<string option, HostFault> =
        match prev, parentOf event with
        | None, _ -> Ok(Some id)
        | Some expected, Some actual when actual = expected -> Ok(Some id)
        | Some expected, _ ->
            Error(HostFault.ParentChainBreak(sprintf "event %s at index %d expects parent %s" id index expected))

    let private inspectChain (events: obj array) : Result<string list * string list, HostFault> =
        let rec loop index prev accIds accKinds =
            if index >= events.Length then
                Ok(List.rev accIds, List.rev accKinds)
            else
                result {
                    let! id = checkedEventId index events.[index]
                    let! next = checkedParent index id prev events.[index]
                    return! loop (index + 1) next (id :: accIds) ((text events.[index]?kind) :: accKinds)
                }

        loop 0 None [] []

    let private distinctWork (ids: string list) : string list =
        let _, ordered =
            ids
            |> List.fold
                (fun (seen, acc) id ->
                    if Set.contains id seen then
                        seen, acc
                    else
                        Set.add id seen, id :: acc)
                (Set.empty, [])

        List.rev ordered

    let private debitOf (fact: obj) : string * float =
        let wid = text fact?workId
        let raw: obj = fact?debited
        wid, (if isNullish raw then 0.0 else unbox<float> raw)

    let private buildFoldView (input: obj) (events: obj array) (ids: string list) (kinds: string list) : obj =
        let workIds =
            events
            |> Array.toList
            |> List.choose (fun event ->
                let wid = text event?workId
                if String.IsNullOrWhiteSpace wid then None else Some wid)
            |> distinctWork

        let factsRaw: obj = input?resourceFacts

        let facts: obj array =
            if isNullish factsRaw then
                [||]
            else
                unbox<obj array> factsRaw

        let grouped =
            facts
            |> Array.toList
            |> List.map debitOf
            |> List.filter (fun (wid, _) -> not (String.IsNullOrWhiteSpace wid))
            |> List.groupBy fst
            |> List.map (fun (wid, rows) -> wid, rows |> List.sumBy snd)
            |> List.sortBy fst

        let total = grouped |> List.sumBy snd
        let debited = grouped |> List.map (fun (wid, sum) -> wid, box sum) |> createObj

        let envelopeRaw: obj = input?initialEnvelope
        let lockRaw: obj = input?pluginLock
        let envelope: obj = if isNullish envelopeRaw then null else envelopeRaw
        let pluginLock: obj = if isNullish lockRaw then null else lockRaw
        let factList: obj = if isNullish factsRaw then null else factsRaw

        let semanticHash =
            CoreHash.canonicalSha256 (
                box
                    {| events = events
                       initialEnvelope = envelope
                       pluginLock = pluginLock
                       resourceFacts = factList |}
            )

        let head: obj =
            match List.tryLast ids with
            | Some last -> box last
            | None -> Option.toObj None

        box
            {| ok = true
               graph =
                box
                    {| eventCount = events.Length
                       kinds = kinds |> List.toArray |}
               certificates = createObj []
               work = box {| ids = workIds |> List.toArray |}
               budget =
                box
                    {| debited = debited
                       totalDebited = total |}
               status = "active"
               answer = Option.toObj None
               semanticHash = semanticHash
               eventHead = head
               appliedOrder = ids |> List.toArray |}

    let private foldDecided (input: obj) (events: obj array) : obj =
        match inspectChain events with
        | Error(fault: HostFault) -> faultView fault
        | Ok(ids: string list, kinds: string list) -> buildFoldView input events ids kinds

    let foldHostEvents (input: obj) : obj =
        let eventsRaw: obj = input?events

        if isNullish eventsRaw then
            faultView HostFault.MissingEvents
        else
            foldDecided input (unbox<obj array> eventsRaw)

    let methods: (string * obj) list =
        [ "planOpenCodeDispatch", box planOpenCodeDispatch
          "planOpenCodeRetry", box planOpenCodeRetry
          "abortOpenCodeWork", box abortOpenCodeWork
          "drainOpenCodeHost", box drainOpenCodeHost
          "foldHostEvents", box foldHostEvents ]
