namespace Wanxiangshu.Execution.Fission

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.OpenCode

/// JS-native semantic surface for intra-participant Fission laws.
///
/// Domain values cross as JSON (`{ ok, reason, lanes, ... }`); admission
/// runtimes, delivery maps, and work bundles are opaque handles (obtain →
/// pass back, never inspect). Translation happens here at the owner boundary
/// (JS-SEMANTIC-SURFACE-003/005); Model / Admission / Runtime stay internal.
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

    let private invoke2 (fn: obj) (a: obj) (b: obj) : Task<obj> =
        unbox (asPromise (apply2 fn a b))

    let private invoke3 (fn: obj) (a: obj) (b: obj) (c: obj) : Task<obj> =
        unbox (asPromise (apply3 fn a b c))

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
        else
            try
                let ok = value?ok

                if isNullish ok || not (isFalse ok) then
                    None
                else
                    let error = value?error
                    Some(if isNullish error then "failed" else string error)
            with _ ->
                None

    let private rejectToJs (reason: FissionRejectReason) : obj =
        match reason with
        | FissionRejectReason.AlreadyFissioned -> box {| ok = false; reason = "AlreadyFissioned" |}
        | FissionRejectReason.TooFewLanes -> box {| ok = false; reason = "TooFewLanes" |}
        | FissionRejectReason.EmptyLanePrompt index ->
            box
                {| ok = false
                   reason = "EmptyLanePrompt"
                   laneIndex = index |}
        | FissionRejectReason.CapacityExceeded -> box {| ok = false; reason = "CapacityExceeded" |}
        | FissionRejectReason.InvalidOrigin -> box {| ok = false; reason = "InvalidOrigin" |}
        | FissionRejectReason.RuntimeUnavailable message ->
            box
                {| ok = false
                   reason = "RuntimeUnavailable"
                   message = message |}

    let private laneToJs (lane: FissionLanePrompt) : obj =
        box {| index = lane.Index; prompt = lane.Prompt |}

    /// Canonical fission prompt parser. Newlines only; empty / singleton fail closed.
    let parsePrompt (text: string) : obj =
        match FissionPrompt.parse text with
        | Ok parsed ->
            box
                {| ok = true
                   count = parsed.Count
                   lanes = parsed.Lanes |> List.map laneToJs |> List.toArray |}
        | Error reason -> rejectToJs reason

    let private parsedOfJs (value: obj) : ParsedFissionPrompts =
        let ok = value?ok

        if not (isNullish ok) && isFalse ok then
            failwith "cannot admit a failed parse"
        else
            let lanes =
                unbox<obj array> (value?lanes)
                |> Array.toList
                |> List.map (fun lane ->
                    { Index = intOf (lane?index)
                      Prompt = string (lane?prompt) })

            let count =
                let raw = value?count
                if isNullish raw then List.length lanes else intOf raw

            { Count = count; Lanes = lanes }

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

    /// Lane record as JS data: index/session/prompt only — no AgentId, handle, or parent.
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

                        if isNullish value then
                            return Ok None
                        else
                            match jsFailure value with
                            | Some message -> return Error message
                            | None -> return Ok(Some(SessionId.create (string value)))
                    with ex ->
                        return Error ex.Message
                }
          OwnerWorkRecord =
            fun owner ->
                task {
                    try
                        let! value = invoke1 workRecordFn (box (SessionId.value owner))

                        match jsFailure value with
                        | Some message -> return Error message
                        | None -> return Ok(string value)
                    with ex ->
                        return Error ex.Message
                }
          CreateLane =
            fun owner parent lane ->
                task {
                    try
                        let parentJs =
                            match parent with
                            | None -> null
                            | Some p -> box (SessionId.value p)

                        let laneJs = box {| index = lane.Index; prompt = lane.Prompt |}
                        let! value = invoke3 createFn (box (SessionId.value owner)) parentJs laneJs

                        match jsFailure value with
                        | Some message -> return Error message
                        | None when isNullish value -> return Error "lane create returned empty"
                        | None -> return Ok(SessionId.create (string value))
                    with ex ->
                        return Error ex.Message
                }
          StartLane =
            fun laneSession startupText ->
                task {
                    try
                        let! value = invoke2 startFn (box (SessionId.value laneSession)) (box startupText)

                        match jsFailure value with
                        | Some message -> return Error message
                        | None -> return Ok()
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

                        match jsFailure value with
                        | Some message -> return Error message
                        | None -> return Ok()
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

    /// Opaque process-local admission runtime. `deps` are JS async functions.
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

    type private CallFlags() =
        member val ContinuationSent = false with get, set
        member val TerminalNotified = false with get, set

    type private DummyDisposable() =
        interface IDisposable with
            member _.Dispose() = ()

    type private DummyTimer() =
        interface ITimerPort with
            member _.Delay _ = Unchecked.defaultof<_>
            member _.Dispose() = ()

    type private DummySessionPort(flags: CallFlags) =
        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) = DummyDisposable() :> IDisposable

            member _.SendPrompt(_, _, _) =
                flags.ContinuationSent <- true
                Task.FromResult(SendOutcome.AdmittedWithReceipt(TransportReceipt.create "receipt"))

            member _.AbortSession _ = Task.FromResult(Ok())
            member _.InterruptSessionOnly _ = Task.FromResult(Ok())
            member _.AbortChildren _ = AsyncSupport.completedTask ()
            member _.CreateSiblingSession(_, _, _) = Task.FromResult(Error "unused")
            member _.TryGetParentSession _ = Task.FromResult(Ok None)
            member _.CreateChildSession(_, _) = Task.FromResult(Error "unused")
            member _.ListChildren _ = Task.FromResult(Ok [])
            member _.FamilyRootOf sessionId = sessionId

    type private DummyEventPort(flags: CallFlags) =
        interface IEventObservationPort with
            member _.SubscribeTerminalListener _ = DummyDisposable() :> IDisposable

            member _.NotifyTerminal _ _ =
                flags.TerminalNotified <- true
                true

    let private dummyTurn (owner: SessionId) : ReconciledTurn =
        { SessionId = owner
          PhysicalUserMessageId = PhysicalUserMessageId.create "msg-1"
          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "msg-0"
          ProviderRun = ProviderRunIdentity.create "run-1"
          Role = None
          Directory = None
          Parts = [||]
          Finish = None
          ErrorName = None
          Model = None
          Outcome = ReconcileProgram.TurnInProgress
          Observation = None }

    /// Absorb a Fission-replaced owner turn through Host + ordinary-turn observe.
    /// Caller must have already `markSilentInterrupt`'d the owner.
    let observeReplacedOwner (ownerSessionId: string) : Task<obj> =
        task {
            let flags = CallFlags()
            let sessionPort = DummySessionPort flags :> ISessionHostPort
            let eventPort = DummyEventPort flags :> IEventObservationPort
            let owner = SessionId.create ownerSessionId
            let turn = dummyTurn owner
            let! handled = FissionHost.observeLaneTurn sessionPort eventPort None (HashSet()) turn

            let context =
                { Turn = turn
                  Quiescence = None
                  Delivery = ReconciledTurnDelivery.Observation }

            do!
                OrdinaryTurnWorkflow.observe
                    (DummyTimer() :> ITimerPort)
                    (fun _ -> ())
                    sessionPort
                    eventPort
                    None
                    (HashSet())
                    (fun _ -> false)
                    (HashSet())
                    None
                    (SessionQuiescenceGate())
                    context

            let idleContext =
                { context with
                    Delivery = ReconciledTurnDelivery.IdleRevisit }

            do! OrdinaryTurnWorkflow.observeIdle (SessionQuiescenceGate()) sessionPort eventPort None idleContext

            return
                box
                    {| handled = handled
                       continuationSent = flags.ContinuationSent
                       terminalNotified = flags.TerminalNotified |}
        }
