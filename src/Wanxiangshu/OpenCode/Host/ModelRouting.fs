namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources

type ModelRoutingTarget = { Model: string; Reasoning: string }

type private ExecutionLease =
    { PhysicalUserMessageId: string option
      Agent: string
      Target: ModelRoutingTarget }

type private PendingDemand =
    { SessionId: string
      PhysicalUserMessageId: string
      Agent: string
      Completion: TaskCompletionSource<ModelRoutingTarget> }

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

    [<Emit("$0($1, $2)")>]
    let private callScheduler (scheduler: obj) (role: string) (running: obj array) : obj = jsNative

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
        : ModelRoutingTarget option =
        if not (isFunction scheduler) then
            invalidOp "execution-model-routing: scheduler default export must be a function"

        if String.IsNullOrWhiteSpace role then
            invalidOp "execution-model-routing: scheduler role must be non-empty"

        let result =
            callScheduler scheduler (role.Trim()) (running |> Array.map targetObject)

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

    type ModelRoutingRuntime(scheduler: obj) =
        let gate = obj ()
        let activeBySession = Dictionary<string, ExecutionLease>()
        let pending = ResizeArray<PendingDemand>()
        let pendingBySession = Dictionary<string, PendingDemand>()
        // DSL-MUTABLE: resource — process-local scheduler poison
        let mutable fatalError: exn option = None

        let running () =
            activeBySession.Values |> Seq.map _.Target |> Seq.toArray

        let ensureHealthy () = fatalError |> Option.iter raise

        let removeDemand demand =
            pending.Remove demand |> ignore

            match pendingBySession.TryGetValue demand.SessionId with
            | true, current when obj.ReferenceEquals(current, demand) ->
                pendingBySession.Remove demand.SessionId |> ignore
            | _ -> ()

        let cancelDemand demand =
            removeDemand demand
            AsyncSupport.trySetCanceled demand.Completion |> ignore

        let failDemand (error: exn) (demand: PendingDemand) =
            try
                demand.Completion.SetException(error)
            with _ ->
                ()

        let poison (error: exn) =
            fatalError <- Some error
            let waiting = pending |> Seq.toArray
            pending.Clear()
            pendingBySession.Clear()
            waiting |> Array.iter (failDemand error)

        let trySchedule agent =
            invokeScheduler scheduler agent (running ())

        let scheduleOrPoison agent =
            try
                trySchedule agent
            with ex ->
                poison ex
                raise ex

        let rememberExecution demand target =
            activeBySession.[demand.SessionId] <-
                { PhysicalUserMessageId = Some demand.PhysicalUserMessageId
                  Agent = demand.Agent
                  Target = target }

        let commit demand target =
            rememberExecution demand target
            removeDemand demand
            AsyncSupport.trySetResult demand.Completion target |> ignore

        let commitScheduled demand scheduled =
            match scheduled with
            | None -> false
            | Some target ->
                commit demand target
                true

        let schedulePendingDemand demand =
            if pending.Contains demand then
                scheduleOrPoison demand.Agent |> commitScheduled demand
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

            match pendingBySession.TryGetValue sessionId with
            | true, demand -> cancelDemand demand
            | false, _ -> ()

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
                Some(Task.FromResult lease.Target)
            | None ->
                requireSameAgent sessionId physicalUserMessageId lease.Agent agent

                activeBySession.[sessionId] <-
                    { lease with
                        PhysicalUserMessageId = Some physicalUserMessageId }

                Some(Task.FromResult lease.Target)
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

        let acquireFreshDemand sessionId physicalUserMessageId agent =
            match scheduleOrPoison agent with
            | Some target ->
                activeBySession.[sessionId] <-
                    { PhysicalUserMessageId = Some physicalUserMessageId
                      Agent = agent
                      Target = target }

                drainDemands ()
                Task.FromResult target
            | None ->
                let completion =
                    TaskCompletionSource<ModelRoutingTarget>(TaskCreationOptions.RunContinuationsAsynchronously)

                let demand =
                    { SessionId = sessionId
                      PhysicalUserMessageId = physicalUserMessageId
                      Agent = agent
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
                    // A newer physical user message is itself authoritative proof
                    // that an older execution in this reusable session no longer
                    // owns capacity. Do not wait for an idle/abort/delete signal
                    // that Host may legitimately omit or reorder.
                    retireCurrentExecution sessionId
                    acquireFreshDemand sessionId physicalUserMessageId agent)

        let acquireManagedSafe sessionId physicalUserMessageId agent =
            try
                acquireManagedTask sessionId physicalUserMessageId agent
            with ex ->
                failedTask<ModelRoutingTarget> ex

        let tryReserveFresh sessionId agent =
            match scheduleOrPoison agent with
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

        member _.AcquireManagedExecution
            (sessionId: string, physicalUserMessageId: string, agent: string)
            : Task<ModelRoutingTarget> =
            match normalizeExecutionInput sessionId physicalUserMessageId agent with
            | Error ex -> failedTask<ModelRoutingTarget> ex
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
            |> Option.iter (fun normSessionId -> lock gate (fun () -> retireCurrentExecution normSessionId))

        member _.CancelPendingExecution(sessionId: string) =
            normalizeSessionId sessionId
            |> Option.iter (fun normSessionId -> lock gate (fun () -> cancelPendingLocked normSessionId))

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

    let cancelUnacquiredExecution (sessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.CancelPendingExecution(SessionId.value sessionId)
        | None -> ()
