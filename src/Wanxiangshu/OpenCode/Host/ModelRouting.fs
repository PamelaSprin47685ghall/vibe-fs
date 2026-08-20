namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal

type ModelRoutingTarget = { Model: string; Reasoning: string }

[<RequireQualifiedAccess>]
type ModelRoutingAcquisition =
    | Acquired of ModelRoutingTarget
    | Superseded

type private ExecutionLease =
    { PhysicalUserMessageId: string option
      Agent: string
      Target: ModelRoutingTarget }

type private PendingDemand =
    { SessionId: string
      PhysicalUserMessageId: string
      Agent: string
      PreviousTarget: ModelRoutingTarget option
      Completion: TaskCompletionSource<ModelRoutingAcquisition> }

module ModelRouting =

    [<Import("homedir", "node:os")>]
    let private homeDir () : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirName (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (left: string, right: string) : string = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string, options: obj) : unit = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string, content: string, options: obj) : unit = jsNative

    [<Import("linkSync", "node:fs")>]
    let private linkSync (existingPath: string, newPath: string) : unit = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("randomUUID", "node:crypto")>]
    let private randomUuid () : string = jsNative

    [<Import("pathToFileURL", "node:url")>]
    let private pathToFileUrl (path: string) : obj = jsNative

    [<Emit("import($0)")>]
    let private importModule (url: string) : Task<obj> = jsNative

    [<Emit("typeof $0 === 'function'")>]
    let private isFunction (value: obj) : bool = jsNative

    [<Emit("$0 != null && typeof $0.then === 'function'")>]
    let private isThenable (value: obj) : bool = jsNative

    [<Emit("$0($1, $2, $3)")>]
    let private callScheduler (scheduler: obj) (role: string) (running: obj array) (previous: obj) : obj = jsNative

    let private nonEmpty name (value: obj) =
        match value with
        | :? string as text when not (String.IsNullOrWhiteSpace text) -> text.Trim()
        | _ -> invalidOp (sprintf "execution-model-routing: scheduler target requires non-empty %s" name)

    let private requireFullModelSelector (model: string) =
        let slash = model.IndexOf '/'

        if slash <= 0 || slash >= model.Length - 1 then
            invalidOp "execution-model-routing: scheduler target model must be full provider/model"

        model

    let private parseTarget (value: obj) : ModelRoutingTarget =
        if isNull value then
            invalidOp "execution-model-routing: null is not a target"

        let model = nonEmpty "model" value?model |> requireFullModelSelector
        let reasoning = nonEmpty "reasoning" value?reasoning
        { Model = model; Reasoning = reasoning }

    let private targetObject (target: ModelRoutingTarget) =
        createObj [ "model" ==> target.Model; "reasoning" ==> target.Reasoning ]

    let invokeScheduler
        (scheduler: obj)
        (role: string)
        (running: ModelRoutingTarget array)
        (previous: ModelRoutingTarget option)
        : ModelRoutingTarget option =
        if not (isFunction scheduler) then
            invalidOp "execution-model-routing: scheduler default export must be a function"

        if String.IsNullOrWhiteSpace role then
            invalidOp "execution-model-routing: scheduler role must be non-empty"

        let result =
            callScheduler
                scheduler
                (role.Trim())
                (running |> Array.map targetObject)
                (previous |> Option.map targetObject |> Option.defaultValue null)

        if isThenable result then
            invalidOp "execution-model-routing: scheduler must be synchronous and must not return a Promise"

        if isNull result then None else Some(parseTarget result)

    let configPath () =
        pathJoin (pathJoin (homeDir (), ".config"), "opencode")
        |> fun root -> pathJoin (root, "wanxiangshu.mjs")

    let private errorCode (error: exn) =
        let value = box error

        if isNull value?code then None else Some(string value?code)

    let private acceptBootstrapLinkError error =
        match errorCode error with
        | Some "EEXIST" -> ()
        | _ -> raise error

    let private publishBootstrap tempPath path =
        try
            linkSync (tempPath, path)
        with ex ->
            acceptBootstrapLinkError ex

    let private removeBootstrapTemp tempPath =
        try
            unlinkSync tempPath
        with _ ->
            ()

    let private publishBootstrapTemplate tempPath path template =
        try
            writeFileSync (tempPath, template, createObj [ "encoding" ==> "utf8"; "flag" ==> "wx" ])
            publishBootstrap tempPath path
        finally
            removeBootstrapTemp tempPath

    let bootstrapAndLoadAt (path: string) (template: string) : Task<obj> =
        task {
            if String.IsNullOrWhiteSpace path then
                invalidArg "path" "execution-model-routing: scheduler path must be non-empty"

            mkdirSync (dirName path, createObj [ "recursive" ==> true ])

            // A direct O_EXCL write makes the destination name visible before all
            // bytes are written, so another OpenCode process could import a partial
            // module. Publish a fully-written same-directory inode with an atomic
            // hard-link instead; EEXIST means another bootstrap already won.
            let tempPath = path + ".tmp-" + randomUuid ()
            publishBootstrapTemplate tempPath path template

            let fileUrl = string (pathToFileUrl path)?href
            let! moduleObj = importModule fileUrl
            let scheduler = if isNull moduleObj then null else moduleObj?``default``

            if not (isFunction scheduler) then
                invalidOp "execution-model-routing: scheduler default export must be a function"

            return scheduler
        }

    let private recommendedTemplate () =
        ModelRoutingResource.recommendedTemplate ()

    let bootstrapDefault () =
        bootstrapAndLoadAt (configPath ()) (recommendedTemplate ())

    let toOpenCodeModel (target: ModelRoutingTarget) : OpencodeModel =
        let slash = target.Model.IndexOf '/'

        { providerID = target.Model.Substring(0, slash)
          modelID = target.Model.Substring(slash + 1)
          variant = Some target.Reasoning }

    let ofOpenCodeModel (model: OpencodeModel) : ModelRoutingTarget option =
        if
            String.IsNullOrWhiteSpace model.providerID
            || String.IsNullOrWhiteSpace model.modelID
        then
            None
        else
            model.variant
            |> Option.bind (fun reasoning ->
                if String.IsNullOrWhiteSpace reasoning then
                    None
                else
                    Some
                        { Model = model.providerID.Trim() + "/" + model.modelID.Trim()
                          Reasoning = reasoning.Trim() })

    let sameTarget (expected: ModelRoutingTarget) (observed: OpencodeModel) =
        match ofOpenCodeModel observed with
        | Some actual -> actual = expected
        | None -> false

    /// A SessionId is a reusable container. Model occupancy belongs to the exact
    /// physical user material that caused the provider execution, never to the
    /// session lifecycle or to an EffectiveAgent pair. Strength may reserve one
    /// target before its physical prompt exists; chat.message later adopts that
    /// reservation into the exact PhysicalUserMessageId without double-counting.
    let private failedTask<'T> (error: exn) : Task<'T> =
        let completion =
            TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

        completion.SetException(error)
        completion.Task

    let private normalizeReservationInput sessionId agent =
        if String.IsNullOrWhiteSpace sessionId then
            Error(ArgumentException("sessionId must be non-empty") :> exn)
        elif String.IsNullOrWhiteSpace agent then
            Error(ArgumentException("agent must be non-empty") :> exn)
        else
            Ok(sessionId.Trim(), agent.Trim())

    let private normalizeExecutionInput sessionId physicalUserMessageId agent =
        match normalizeReservationInput sessionId agent with
        | Error error -> Error error
        | Ok(normSessionId, normAgent) when String.IsNullOrWhiteSpace physicalUserMessageId ->
            Error(ArgumentException("physicalUserMessageId must be non-empty") :> exn)
        | Ok(normSessionId, normAgent) -> Ok(normSessionId, physicalUserMessageId.Trim(), normAgent)

    let private normalizeSessionId sessionId =
        if String.IsNullOrWhiteSpace sessionId then
            None
        else
            Some(sessionId.Trim())

    let private normalizePhysicalExecutionKey sessionId physicalUserMessageId =
        match normalizeSessionId sessionId with
        | None -> None
        | Some _ when String.IsNullOrWhiteSpace physicalUserMessageId -> None
        | Some normSessionId -> Some(normSessionId, physicalUserMessageId.Trim())

    let private targetProvider (target: ModelRoutingTarget) =
        target.Model.Substring(0, target.Model.IndexOf '/')

    type ModelRoutingRuntime(scheduler: obj) =
        let gate = obj ()

        let capacity =
            BorrowingCapacity<ModelRoutingTarget>(CapacityLedger<ModelRoutingTarget>(), targetProvider, (=))
        // DSL-MUTABLE: resource — active execution lease map per session
        let activeBySession = Dictionary<string, ExecutionLease>()
        // DSL-MUTABLE: resource — last physical target map per session
        let lastPhysicalTargetBySession = Dictionary<string, ModelRoutingTarget>()
        let pending = ResizeArray<PendingDemand>()
        // DSL-MUTABLE: resource — pending demand map per session
        let pendingBySession = Dictionary<string, PendingDemand>()
        // DSL-MUTABLE: resource — process-local scheduler poison
        let mutable fatalError: exn option = None

        let running () = capacity.Snapshot()

        let previousTarget sessionId =
            match lastPhysicalTargetBySession.TryGetValue sessionId with
            | true, target -> Some target
            | false, _ -> None

        let ensureHealthy () = fatalError |> Option.iter raise

        let removeDemand demand =
            pending.Remove demand |> ignore

            match pendingBySession.TryGetValue demand.SessionId with
            | true, current when obj.ReferenceEquals(current, demand) ->
                pendingBySession.Remove demand.SessionId |> ignore
            | _ -> ()

        let cancelDemand demand =
            removeDemand demand

            AsyncSupport.trySetResult demand.Completion ModelRoutingAcquisition.Superseded
            |> ignore

        let failDemand (error: exn) (demand: PendingDemand) =
            try
                demand.Completion.SetException(error)
            with _ ->
                ()

        let poison (error: exn) =
            fatalError <- Some error
            capacity.Fail error
            let waiting = pending |> Seq.toArray
            pending.Clear()
            pendingBySession.Clear()
            waiting |> Array.iter (failDemand error)

        let scheduleOrPoison running agent previous =
            try
                invokeScheduler scheduler agent running previous
            with ex ->
                poison ex
                raise ex

        let routeFreshOrPoison sessionId oldPhysicalUserMessageId physicalUserMessageId agent previous =
            try
                capacity.RouteFresh(
                    sessionId,
                    oldPhysicalUserMessageId,
                    physicalUserMessageId,
                    fun running -> scheduleOrPoison running agent previous
                )
            with ex ->
                poison ex
                raise ex

        let reserveFreshOrPoison sessionId agent previous =
            try
                capacity.ReserveFresh(sessionId, (fun running -> scheduleOrPoison running agent previous))
            with ex ->
                poison ex
                raise ex

        let rememberExecution demand target =
            activeBySession.[demand.SessionId] <-
                { PhysicalUserMessageId = Some demand.PhysicalUserMessageId
                  Agent = demand.Agent
                  Target = target }

            lastPhysicalTargetBySession.[demand.SessionId] <- target

        let commit demand target =
            rememberExecution demand target
            removeDemand demand

            AsyncSupport.trySetResult demand.Completion (ModelRoutingAcquisition.Acquired target)
            |> ignore

        let commitScheduled demand scheduled =
            match scheduled with
            | None -> false
            | Some target ->
                commit demand target
                true

        let schedulePendingDemand demand =
            if pending.Contains demand then
                routeFreshOrPoison demand.SessionId None demand.PhysicalUserMessageId demand.Agent demand.PreviousTarget
                |> commitScheduled demand
            else
                false

        let rec drainDemands () =
            ensureHealthy ()

            pending
            |> Seq.toArray
            |> Array.map schedulePendingDemand
            |> Array.exists id
            |> continueDrain

        and continueDrain progressed =
            if progressed && pending.Count > 0 then
                drainDemands ()

        let retireCurrentExecution sessionId =
            let changed = activeBySession.Remove sessionId
            capacity.ReleaseSession sessionId

            match pendingBySession.TryGetValue sessionId with
            | true, demand -> cancelDemand demand
            | false, _ -> ()

            if changed && fatalError.IsNone then
                drainDemands ()

        let retirePhysicalExecution sessionId physicalUserMessageId =
            let changed =
                match activeBySession.TryGetValue sessionId with
                | true, lease when lease.PhysicalUserMessageId = Some physicalUserMessageId ->
                    let removed = activeBySession.Remove sessionId
                    capacity.ReleasePhysical(sessionId, physicalUserMessageId)
                    removed
                | _ -> false

            // Exact terminal evidence for an older physical execution must never
            // cancel a newer pending demand for the same reusable SessionId.
            if changed && fatalError.IsNone then
                drainDemands ()

        let requireSameAgent sessionId physicalUserMessageId expected observed =
            if expected <> observed then
                invalidOp (
                    sprintf
                        "execution-model-routing: physical execution %s/%s changed agent (%s -> %s)"
                        sessionId
                        physicalUserMessageId
                        expected
                        observed
                )

        let reuseOrAdoptActiveExecution sessionId physicalUserMessageId agent (lease: ExecutionLease) =
            match lease.PhysicalUserMessageId with
            | Some current when current = physicalUserMessageId ->
                requireSameAgent sessionId physicalUserMessageId lease.Agent agent
                Some(Task.FromResult(ModelRoutingAcquisition.Acquired lease.Target))
            | None ->
                requireSameAgent sessionId physicalUserMessageId lease.Agent agent

                capacity.AdoptReservation(sessionId, physicalUserMessageId, lease.Target)

                activeBySession.[sessionId] <-
                    { lease with
                        PhysicalUserMessageId = Some physicalUserMessageId }

                lastPhysicalTargetBySession.[sessionId] <- lease.Target

                Some(Task.FromResult(ModelRoutingAcquisition.Acquired lease.Target))
            | Some _ -> None

        let reusePendingExecution sessionId physicalUserMessageId agent =
            match pendingBySession.TryGetValue sessionId with
            | true, demand when demand.PhysicalUserMessageId = physicalUserMessageId ->
                requireSameAgent sessionId physicalUserMessageId demand.Agent agent
                Some demand.Completion.Task
            | _ -> None

        let currentExecutionTask sessionId physicalUserMessageId agent =
            match activeBySession.TryGetValue sessionId with
            | true, lease -> reuseOrAdoptActiveExecution sessionId physicalUserMessageId agent lease
            | false, _ -> reusePendingExecution sessionId physicalUserMessageId agent

        let currentPhysicalUserMessageId sessionId =
            match activeBySession.TryGetValue sessionId with
            | true, lease -> lease.PhysicalUserMessageId
            | false, _ -> None

        let cancelCurrentDemand sessionId =
            match pendingBySession.TryGetValue sessionId with
            | true, demand -> cancelDemand demand
            | false, _ -> ()

        let acquireFreshDemand sessionId oldPhysicalUserMessageId physicalUserMessageId agent =
            let previous = previousTarget sessionId

            match routeFreshOrPoison sessionId oldPhysicalUserMessageId physicalUserMessageId agent previous with
            | Some target ->
                activeBySession.[sessionId] <-
                    { PhysicalUserMessageId = Some physicalUserMessageId
                      Agent = agent
                      Target = target }

                lastPhysicalTargetBySession.[sessionId] <- target

                drainDemands ()
                Task.FromResult(ModelRoutingAcquisition.Acquired target)
            | None ->
                let completion =
                    TaskCompletionSource<ModelRoutingAcquisition>(TaskCreationOptions.RunContinuationsAsynchronously)

                let demand =
                    { SessionId = sessionId
                      PhysicalUserMessageId = physicalUserMessageId
                      Agent = agent
                      PreviousTarget = previous
                      Completion = completion }

                pending.Add demand
                pendingBySession.[sessionId] <- demand
                completion.Task

        let acquireManagedTask sessionId physicalUserMessageId agent =
            lock gate (fun () ->
                ensureHealthy ()

                match currentExecutionTask sessionId physicalUserMessageId agent with
                | Some current -> current
                | None ->
                    let oldPhysicalUserMessageId = currentPhysicalUserMessageId sessionId
                    activeBySession.Remove sessionId |> ignore
                    cancelCurrentDemand sessionId
                    acquireFreshDemand sessionId oldPhysicalUserMessageId physicalUserMessageId agent)

        let acquireManagedSafe sessionId physicalUserMessageId agent =
            try
                acquireManagedTask sessionId physicalUserMessageId agent
            with ex ->
                failedTask<ModelRoutingAcquisition> ex

        let tryReserveFresh sessionId agent =
            match reserveFreshOrPoison sessionId agent (previousTarget sessionId) with
            | None -> None
            | Some target ->
                activeBySession.[sessionId] <-
                    { PhysicalUserMessageId = None
                      Agent = agent
                      Target = target }

                drainDemands ()
                Some target

        let tryReserveLocked sessionId agent =
            ensureHealthy ()

            match activeBySession.TryGetValue sessionId, pendingBySession.ContainsKey sessionId with
            | (true, lease), _ when lease.PhysicalUserMessageId.IsNone && lease.Agent = agent -> Some lease.Target
            | (true, _), _ -> None
            | (false, _), true -> None
            | (false, _), false -> tryReserveFresh sessionId agent

        let tryLeaseLocked sessionId physicalUserMessageId agent =
            match activeBySession.TryGetValue sessionId with
            | true, lease when lease.PhysicalUserMessageId = Some physicalUserMessageId && lease.Agent = agent ->
                Some lease.Target
            | _ -> None

        let cancelPendingLocked sessionId =
            match pendingBySession.TryGetValue sessionId with
            | true, demand -> cancelDemand demand
            | false, _ -> ()

        let exactTargetAvailable agent target running =
            match scheduleOrPoison running agent (Some target) with
            | Some candidate -> candidate = target
            | None -> false

        let enterProviderStepLocked sessionId physicalUserMessageId fence =
            ensureHealthy ()

            match activeBySession.TryGetValue sessionId with
            | true, lease when lease.PhysicalUserMessageId = Some physicalUserMessageId ->
                capacity.EnterStep(
                    sessionId,
                    physicalUserMessageId,
                    lease.Target,
                    fence,
                    fun running -> exactTargetAvailable lease.Agent lease.Target running
                )
            | _ ->
                failedTask<unit> (
                    InvalidOperationException(
                        sprintf
                            "execution-model-routing: provider step %s/%s has no active execution binding"
                            sessionId
                            physicalUserMessageId
                    )
                )
                :> Task

        let drainIfHealthy () =
            if fatalError.IsNone then
                drainDemands ()

        member _.AcquireManagedExecution
            (sessionId: string, physicalUserMessageId: string, agent: string)
            : Task<ModelRoutingAcquisition> =
            match normalizeExecutionInput sessionId physicalUserMessageId agent with
            | Error ex -> failedTask<ModelRoutingAcquisition> ex
            | Ok(normSessionId, normPhysicalUserMessageId, normAgent) ->
                acquireManagedSafe normSessionId normPhysicalUserMessageId normAgent

        /// Strength-only nonwaiting reservation. It is capacity-bearing, but not
        /// yet a provider execution identity. The exact chat.message later adopts
        /// it without another scheduler decision or another running occurrence.
        member _.TryReserveManaged(sessionId: string, agent: string) : ModelRoutingTarget option =
            match normalizeReservationInput sessionId agent with
            | Error _ -> None
            | Ok(normSessionId, normAgent) -> lock gate (fun () -> tryReserveLocked normSessionId normAgent)

        member _.TryLease(sessionId: string, physicalUserMessageId: string, agent: string) : ModelRoutingTarget option =
            match normalizeExecutionInput sessionId physicalUserMessageId agent with
            | Error _ -> None
            | Ok(normSessionId, normPhysicalUserMessageId, normAgent) ->
                lock gate (fun () -> tryLeaseLocked normSessionId normPhysicalUserMessageId normAgent)

        /// Physical end signals are cleanup evidence, not the sole correctness
        /// mechanism. A newer chat.message also supersedes this lease atomically.
        member _.ReleaseExecution(sessionId: string) =
            normalizeSessionId sessionId
            |> Option.iter (fun normSessionId ->
                lock gate (fun () ->
                    retireCurrentExecution normSessionId
                    lastPhysicalTargetBySession.Remove normSessionId |> ignore))

        /// Exact physical terminal evidence. Unlike force cleanup, this cannot
        /// retire a newer execution or pending demand that happens to reuse the
        /// same SessionId after the terminal event was produced.
        member _.ReleasePhysicalExecution(sessionId: string, physicalUserMessageId: string) =
            match normalizePhysicalExecutionKey sessionId physicalUserMessageId with
            | None -> ()
            | Some(normSessionId, normPhysicalUserMessageId) ->
                lock gate (fun () -> retirePhysicalExecution normSessionId normPhysicalUserMessageId)

        member _.CancelPendingExecution(sessionId: string) =
            normalizeSessionId sessionId
            |> Option.iter (fun normSessionId -> lock gate (fun () -> cancelPendingLocked normSessionId))

        member _.BindCapacityChild(parentSessionId: string, childSessionId: string) =
            lock gate (fun () -> capacity.BindChild(parentSessionId, childSessionId))

        member _.BindCapacityCompanion(ownerSessionId: string, bloggerSessionId: string) =
            lock gate (fun () -> capacity.BindCompanion(ownerSessionId, bloggerSessionId))

        member _.DropCapacityLineage(sessionId: string) =
            normalizeSessionId sessionId
            |> Option.iter (fun normSessionId -> lock gate (fun () -> capacity.DropLineage normSessionId))

        member _.EnterProviderStep
            (sessionId: string, physicalUserMessageId: string, visibleProviderRuns: Set<string>)
            : Task =
            match normalizePhysicalExecutionKey sessionId physicalUserMessageId with
            | None -> failedTask<unit> (ArgumentException("provider step identity must be non-empty")) :> Task
            | Some(normSessionId, normPhysicalUserMessageId) ->
                let admission =
                    lock gate (fun () ->
                        enterProviderStepLocked normSessionId normPhysicalUserMessageId visibleProviderRuns)

                task {
                    do! admission
                    lock gate drainIfHealthy
                }
                :> Task

        member _.EndProviderStep(sessionId: string, physicalUserMessageId: string, providerRun: string) =
            match normalizePhysicalExecutionKey sessionId physicalUserMessageId with
            | None -> ()
            | Some _ when String.IsNullOrWhiteSpace providerRun -> ()
            | Some(normSessionId, normPhysicalUserMessageId) ->
                lock gate (fun () ->
                    capacity.EndStep(normSessionId, normPhysicalUserMessageId, providerRun.Trim())
                    drainIfHealthy ())

        member _.SuppressProviderStep(sessionId: string, physicalUserMessageId: string) =
            match normalizePhysicalExecutionKey sessionId physicalUserMessageId with
            | None -> ()
            | Some(normSessionId, normPhysicalUserMessageId) ->
                lock gate (fun () ->
                    capacity.SuppressStep(normSessionId, normPhysicalUserMessageId)
                    drainIfHealthy ())

        member _.SnapshotOccupied() = lock gate (fun () -> running ())
        member _.PendingCount = lock gate (fun () -> pending.Count)

    let private sharedGate = obj ()
    // DSL-MUTABLE: resource — process-shared scheduler runtime singleton
    let mutable private sharedRuntime: ModelRoutingRuntime option = None
    // DSL-MUTABLE: single-flight — in-flight scheduler bootstrap
    let mutable private sharedLoad: Task<ModelRoutingRuntime> option = None

    let private ensureShared () : Task<ModelRoutingRuntime> =
        lock sharedGate (fun () ->
            match sharedRuntime, sharedLoad with
            | Some runtime, _ -> Task.FromResult runtime
            | None, Some loading -> loading
            | None, None ->
                let loading =
                    task {
                        let! scheduler = bootstrapDefault ()
                        let runtime = ModelRoutingRuntime(scheduler)
                        lock sharedGate (fun () -> sharedRuntime <- Some runtime)
                        return runtime
                    }

                sharedLoad <- Some loading
                loading)

    let initialize () : Task =
        task {
            let! _ = ensureShared ()
            return ()
        }
        :> Task

    let private current () =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime
        | None -> invalidOp "execution-model-routing: scheduler runtime was not initialized during plugin load"

    let acquireManagedExecution (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) (agent: string) =
        current()
            .AcquireManagedExecution(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId,
                agent
            )

    let hasRuntime () : bool =
        lock sharedGate (fun () -> sharedRuntime.IsSome)

    let tryReserveManaged (sessionId: SessionId) (agent: string) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.TryReserveManaged(SessionId.value sessionId, agent)
        | None -> None

    let tryLease (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) (agent: string) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.TryLease(SessionId.value sessionId, PhysicalUserMessageId.value physicalUserMessageId, agent)
        | None -> None

    let releaseExecution (sessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.ReleaseExecution(SessionId.value sessionId)
        | None -> ()

    let releasePhysicalExecution (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.ReleasePhysicalExecution(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId
            )
        | None -> ()

    let cancelUnacquiredExecution (sessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.CancelPendingExecution(SessionId.value sessionId)
        | None -> ()

    let bindCapacityChild (parentSessionId: SessionId) (childSessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.BindCapacityChild(SessionId.value parentSessionId, SessionId.value childSessionId)
        | None -> ()

    let bindCapacityCompanion (ownerSessionId: SessionId) (bloggerSessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.BindCapacityCompanion(SessionId.value ownerSessionId, SessionId.value bloggerSessionId)
        | None -> ()

    let dropCapacityLineage (sessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.DropCapacityLineage(SessionId.value sessionId)
        | None -> ()

    let enterProviderStep
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (visibleProviderRuns: Set<ProviderRunIdentity>)
        =
        current()
            .EnterProviderStep(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId,
                visibleProviderRuns |> Set.map ProviderRunIdentity.value
            )

    let endProviderStep
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.EndProviderStep(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId,
                ProviderRunIdentity.value providerRun
            )
        | None -> ()

    let suppressProviderStep (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.SuppressProviderStep(SessionId.value sessionId, PhysicalUserMessageId.value physicalUserMessageId)
        | None -> ()

    [<RequireQualifiedAccess>]
    type ChatExecutionAdmission =
        | NoRoute
        | ExternalManaged of SessionId * PhysicalUserMessageId * string
        | PluginManaged of PromptAuthority.PromptClaim * PhysicalUserMessageId * string
        | Rejected of string

    [<RequireQualifiedAccess>]
    type RoutedChatExecution =
        | NoRoute
        | Superseded
        | ExternalManaged of SessionId * PhysicalUserMessageId * string * OpencodeModel
        | PluginManaged of PromptAuthority.PromptClaim * PhysicalUserMessageId * string * OpencodeModel

    let managedAgentForAdmission (value: string option) =
        value
        |> Option.map (fun agent -> agent.Trim())
        |> Option.filter (fun agent ->
            not (String.IsNullOrWhiteSpace agent)
            && (ManagedAgent.requiredNames |> List.contains agent))

    let private externalAdmission sessionId physicalUserMessageId explicitAgent =
        match managedAgentForAdmission explicitAgent, physicalUserMessageId with
        | Some agent, Some physical -> ChatExecutionAdmission.ExternalManaged(sessionId, physical, agent)
        | Some _, None ->
            ChatExecutionAdmission.Rejected "EMR-009: managed chat.message has no physical user message id"
        | None, _ -> ChatExecutionAdmission.NoRoute

    let private agentManagedOfClaim
        (claim: PromptAuthority.PromptClaim)
        (physicalUserMessageId: PhysicalUserMessageId option)
        =
        match managedAgentForAdmission claim.EffectiveAgent, physicalUserMessageId with
        | Some agent, Some physical -> ChatExecutionAdmission.PluginManaged(claim, physical, agent)
        | Some _, None ->
            ChatExecutionAdmission.Rejected(
                sprintf
                    "EMR-009: managed PromptKey %s has no physical user message id"
                    (PromptKey.value claim.PromptKey)
            )
        | None, _ ->
            ChatExecutionAdmission.Rejected(
                sprintf "PROMPT-006: PromptKey %s has no managed EffectiveAgent" (PromptKey.value claim.PromptKey)
            )

    let private pendingAdmission tryPendingClaim durable sessionId physicalUserMessageId promptKey =
        match tryPendingClaim (Some durable) sessionId promptKey with
        | None -> ChatExecutionAdmission.NoRoute
        | Some claim -> agentManagedOfClaim claim physicalUserMessageId

    let private pluginAdmission tryPendingClaim journal sessionId physicalUserMessageId promptKey =
        match journal with
        | None -> ChatExecutionAdmission.NoRoute
        | Some durable -> pendingAdmission tryPendingClaim durable sessionId physicalUserMessageId promptKey

    let chatExecutionAdmission
        tryPendingClaim
        journal
        isHostCompaction
        (sessionId: SessionId option)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (promptKey: PromptKey option)
        (explicitAgent: string option)
        =
        match isHostCompaction, sessionId, promptKey with
        | true, _, _ -> ChatExecutionAdmission.NoRoute
        | false, Some sid, Some key -> pluginAdmission tryPendingClaim journal sid physicalUserMessageId key
        | false, Some sid, None -> externalAdmission sid physicalUserMessageId explicitAgent
        | _ -> ChatExecutionAdmission.NoRoute

    let private externalRoutedExecution sessionId physical agent =
        function
        | ModelRoutingAcquisition.Superseded -> RoutedChatExecution.Superseded
        | ModelRoutingAcquisition.Acquired target ->
            RoutedChatExecution.ExternalManaged(sessionId, physical, agent, toOpenCodeModel target)

    let private pluginRoutedExecution claim physical agent =
        function
        | ModelRoutingAcquisition.Superseded -> RoutedChatExecution.Superseded
        | ModelRoutingAcquisition.Acquired target ->
            RoutedChatExecution.PluginManaged(claim, physical, agent, toOpenCodeModel target)

    /// EMR-009 managed chat.message routing policy. The composition root supplies
    /// only the two process-local observations that cross into sibling owners;
    /// admission interpretation and acquisition outcomes stay owned here.
    let routeChatExecution
        (observeManagedSession: SessionId -> unit)
        (observeExternalAgent: SessionId -> string -> unit)
        (admission: ChatExecutionAdmission)
        : Task<RoutedChatExecution> =
        task {
            match admission with
            | ChatExecutionAdmission.NoRoute -> return RoutedChatExecution.NoRoute
            | ChatExecutionAdmission.Rejected error -> return invalidOp error
            | ChatExecutionAdmission.ExternalManaged(sessionId, physical, agent) ->
                observeManagedSession sessionId
                observeExternalAgent sessionId agent
                let! acquisition = acquireManagedExecution sessionId physical agent
                return externalRoutedExecution sessionId physical agent acquisition
            | ChatExecutionAdmission.PluginManaged(claim, physical, agent) ->
                observeManagedSession claim.SessionId
                let! acquisition = acquireManagedExecution claim.SessionId physical agent
                return pluginRoutedExecution claim physical agent acquisition
        }

    let routedModel =
        function
        | RoutedChatExecution.NoRoute
        | RoutedChatExecution.Superseded -> None
        | RoutedChatExecution.ExternalManaged(_, _, _, model)
        | RoutedChatExecution.PluginManaged(_, _, _, model) -> Some model
