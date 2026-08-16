namespace Wanxiangshu.Execution.Fission

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native semantic surface for intra-participant Fission laws.
module FissionSurface =

    [<Emit("$0==null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private asPromise (value: obj) : JS.Promise<obj> = jsNative

    [<Emit("$0($1)")>]
    let private apply1 (fn: obj) (a: obj) : obj = jsNative

    [<Emit("$0($1,$2)")>]
    let private apply2 (fn: obj) (a: obj) (b: obj) : obj = jsNative

    [<Emit("$0($1,$2,$3)")>]
    let private apply3 (fn: obj) (a: obj) (b: obj) (c: obj) : obj = jsNative

    let private invoke1 (fn: obj) (a: obj) : Task<obj> = unbox (asPromise (apply1 fn a))
    let private invoke2 (fn: obj) (a: obj) (b: obj) : Task<obj> = unbox (asPromise (apply2 fn a b))
    let private invoke3 (fn: obj) (a: obj) (b: obj) (c: obj) : Task<obj> = unbox (asPromise (apply3 fn a b c))

    let private intOf (value: obj) : int =
        match value with
        | :? int as v -> v
        | :? int64 as v -> int v
        | :? float as v -> int v
        | _ -> int (string value)

    let private isFalse (value: obj) : bool =
        match value with
        | :? bool as v -> not v
        | _ -> string value = "false"

    let private jsFailure (value: obj) : string option =
        if isNullish value then
            None
        elif isNullish (value?ok) || not (isFalse (value?ok)) then
            None
        elif isNullish (value?error) then
            Some "failed"
        else
            Some(string (value?error))

    let private rejectToJs (reason: FissionRejectReason) : obj =
        match reason with
        | FissionRejectReason.AlreadyFissioned ->
            box
                {| ok = false
                   reason = "AlreadyFissioned" |}
        | FissionRejectReason.TooFewLanes -> box {| ok = false; reason = "TooFewLanes" |}
        | FissionRejectReason.EmptyLanePrompt index ->
            box
                {| ok = false
                   reason = "EmptyLanePrompt"
                   laneIndex = index |}
        | FissionRejectReason.CapacityExceeded ->
            box
                {| ok = false
                   reason = "CapacityExceeded" |}
        | FissionRejectReason.InvalidOrigin ->
            box
                {| ok = false
                   reason = "InvalidOrigin" |}
        | FissionRejectReason.RuntimeUnavailable message ->
            box
                {| ok = false
                   reason = "RuntimeUnavailable"
                   message = message |}

    let private laneToJs (lane: FissionLanePrompt) : obj =
        box
            {| index = lane.Index
               prompt = lane.Prompt |}

    let parsePrompt (text: string) : obj =
        match FissionPrompt.parse text with
        | Ok parsed ->
            box
                {| ok = true
                   count = parsed.Count
                   lanes = parsed.Lanes |> List.map laneToJs |> List.toArray |}
        | Error reason -> rejectToJs reason

    let private countOf (value: obj) (lanes: FissionLanePrompt list) : int =
        let raw = value?count
        if isNullish raw then List.length lanes else intOf raw

    let private parsedOfJs (value: obj) : ParsedFissionPrompts =
        if isNullish value then
            failwith "cannot admit a null parse"
        else
            ()

        let ok = value?ok

        if not (isNullish ok) && isFalse ok then
            failwith "cannot admit a failed parse"
        else
            ()

        let lanes =
            unbox<obj array> (value?lanes)
            |> Array.toList
            |> List.map (fun lane ->
                { Index = intOf (lane?index)
                  Prompt = string (lane?prompt) })

        { Count = countOf value lanes
          Lanes = lanes }

    let completionTargets (laneCount: int) (affinity: obj) : int array =
        let parsed =
            match string (affinity?kind) with
            | "lane" -> FissionCompletionAffinity.Lane(intOf (affinity?index))
            | _ -> FissionCompletionAffinity.PreFissionBroadcast

        FissionCompletionRouting.targets laneCount parsed |> List.toArray

    let deliveryEmpty (laneCount: int) : FissionDelivery = FissionDelivery.empty laneCount

    let deliveryMark (completionId: string) (laneIndex: int) (delivery: FissionDelivery) : obj =
        match FissionDelivery.mark completionId laneIndex delivery with
        | Ok next -> box {| ok = true; delivery = next |}
        | Error(FissionDeliveryError.InvalidLane index) ->
            box
                {| ok = false
                   reason = "InvalidLane"
                   laneIndex = index |}

    let deliveryPendingTargets (completionId: string) (delivery: FissionDelivery) : int array =
        FissionDelivery.pendingTargets completionId delivery |> List.toArray

    let workBundleEmpty: FissionWorkBundle = FissionWorkBundle.empty

    let private bundleErrorToJs (error: FissionBundleError) : obj =
        match error with
        | FissionBundleError.ConflictingLaneRecord(index, existing, proposed) ->
            box
                {| ok = false
                   reason = "ConflictingLaneRecord"
                   laneIndex = index
                   existingRef = existing
                   proposedRef = proposed |}

    let workBundleAdd (laneIndex: int) (workRecordRef: string) (bundle: FissionWorkBundle) : obj =
        match FissionWorkBundle.add laneIndex workRecordRef bundle with
        | Ok next -> box {| ok = true; bundle = next |}
        | Error error -> bundleErrorToJs error

    let workBundleMerge (left: FissionWorkBundle) (right: FissionWorkBundle) : obj =
        match FissionWorkBundle.merge left right with
        | Ok next -> box {| ok = true; bundle = next |}
        | Error error -> bundleErrorToJs error

    let workBundleKeys (bundle: FissionWorkBundle) : int array =
        FissionWorkBundle.keys bundle |> List.toArray

    let workBundleEntries (bundle: FissionWorkBundle) : obj array =
        FissionWorkBundle.entries bundle
        |> List.map (fun (index, workRef) -> box [| box index; box workRef |])
        |> List.toArray

    let convergenceReady
        (laneCount: int)
        (completionIds: string array)
        (bundle: FissionWorkBundle)
        (delivery: FissionDelivery)
        : bool =
        FissionConvergence.ready laneCount (Array.toList completionIds) bundle delivery

    let startedLane (index: int) (sessionId: string) (prompt: string) : obj =
        box
            {| index = index
               sessionId = sessionId
               prompt = prompt
               hasAgentId = false
               hasHandle = false
               hasParent = false |}

    let startup (laneCount: int) (laneIndex: int) (prompt: string) (workRecord: string) : string =
        FissionStartup.render laneCount { Index = laneIndex; Prompt = prompt } workRecord

    let private jsResult (value: obj) : Result<unit, string> =
        jsFailure value
        |> Option.map (fun message -> Error message)
        |> Option.defaultValue (Ok())

    let private jsResultToString (value: obj) : Result<string, string> =
        jsFailure value
        |> Option.map Error
        |> Option.defaultWith (fun () -> Ok(string value))

    let private jsResultToOptionalSessionId (value: obj) : Result<SessionId option, string> =
        jsFailure value
        |> Option.map Error
        |> Option.defaultWith (fun () ->
            Ok(
                if isNullish value then
                    None
                else
                    Some(SessionId.create (string value))
            ))

    let private jsResultToRequiredSessionId (emptyMessage: string) (value: obj) : Result<SessionId, string> =
        jsFailure value
        |> Option.map Error
        |> Option.defaultWith (fun () ->
            if isNullish value then
                Error emptyMessage
            else
                Ok(SessionId.create (string value)))

    let private parentAsJs (parent: SessionId option) : obj =
        match parent with
        | None -> null
        | Some p -> box (SessionId.value p)

    let private dependenciesOfJs (deps: obj) : FissionAdmissionDependencies =
        let parentOfFn = deps?parentOf
        let workRecordFn = deps?ownerWorkRecord
        let createFn = deps?createLane
        let startFn = deps?startLane
        let abortFn = deps?abortLane
        let interruptFn = deps?silentInterruptOwner

        { ParentOf =
            fun owner ->
                task {
                    try
                        let! value = invoke1 parentOfFn (box (SessionId.value owner))
                        return jsResultToOptionalSessionId value
                    with ex ->
                        return Error ex.Message
                }
          OwnerWorkRecord =
            fun owner ->
                task {
                    try
                        let! value = invoke1 workRecordFn (box (SessionId.value owner))
                        return jsResultToString value
                    with ex ->
                        return Error ex.Message
                }
          CreateLane =
            fun owner parent lane ->
                task {
                    try
                        let laneJs =
                            box
                                {| index = lane.Index
                                   prompt = lane.Prompt |}

                        let! value = invoke3 createFn (box (SessionId.value owner)) (parentAsJs parent) laneJs
                        return jsResultToRequiredSessionId "lane create returned empty" value
                    with ex ->
                        return Error ex.Message
                }
          StartLane =
            fun laneSession startupText ->
                task {
                    try
                        let! value = invoke2 startFn (box (SessionId.value laneSession)) (box startupText)
                        return jsResult value
                    with ex ->
                        return Error ex.Message
                }
          AbortLane =
            fun laneSession ->
                task {
                    try
                        let! _ = invoke1 abortFn (box (SessionId.value laneSession))
                        return ()
                    with _ ->
                        return ()
                }
                :> Task
          SilentInterruptOwner =
            fun owner ->
                task {
                    try
                        let! value = invoke1 interruptFn (box (SessionId.value owner))
                        return jsResult value
                    with ex ->
                        return Error ex.Message
                } }

    let private admissionToJs (admission: FissionAdmission) : obj =
        let parent =
            match admission.ParentSessionId with
            | None -> null
            | Some parent -> box (SessionId.value parent)

        let lanes =
            admission.Lanes
            |> List.map (fun lane ->
                box
                    {| index = lane.Index
                       sessionId = SessionId.value lane.SessionId
                       prompt = lane.Prompt |})
            |> List.toArray

        box
            {| ok = true
               ownerSessionId = SessionId.value admission.OwnerSessionId
               parentSessionId = parent
               ownerWorkRecord = admission.OwnerWorkRecord
               lanes = lanes |}

    let createAdmission (deps: obj) : FissionAdmissionRuntime =
        FissionAdmission.create (dependenciesOfJs deps)

    let admit (runtime: FissionAdmissionRuntime) (ownerSessionId: string) (parsed: obj) : Task<obj> =
        task {
            match! FissionAdmission.admit runtime (SessionId.create ownerSessionId) (parsedOfJs parsed) with
            | Ok admission -> return admissionToJs admission
            | Error reason -> return rejectToJs reason
        }

    let isActive (runtime: FissionAdmissionRuntime) (ownerSessionId: string) : bool =
        FissionAdmission.isActive runtime (SessionId.create ownerSessionId)

    let release (runtime: FissionAdmissionRuntime) (ownerSessionId: string) : unit =
        FissionAdmission.release runtime (SessionId.create ownerSessionId)

    let markSilentInterrupt (ownerSessionId: string) : unit =
        FissionRuntime.markSilentInterrupt (SessionId.create ownerSessionId)

    let isSilentInterrupt (ownerSessionId: string) : bool =
        FissionRuntime.isSilentInterrupt (SessionId.create ownerSessionId)

    let tryConsumeSilentInterrupt (ownerSessionId: string) : bool =
        FissionRuntime.tryConsumeSilentInterrupt (SessionId.create ownerSessionId)

    let clearSilentInterrupt (ownerSessionId: string) : unit =
        FissionRuntime.clearSilentInterrupt (SessionId.create ownerSessionId)

    let clearOwner (ownerSessionId: string) : unit =
        FissionRuntime.clearOwner (SessionId.create ownerSessionId)
