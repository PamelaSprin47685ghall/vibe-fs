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

type private PendingDemand =
    { SessionId: string
      LeaseKey: string
      Role: string
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

            try
                writeFileSync (tempPath, template, createObj [ "encoding" ==> "utf8"; "flag" ==> "wx" ])

                try
                    linkSync (tempPath, path)
                with ex ->
                    match errorCode ex with
                    | Some "EEXIST" -> ()
                    | _ -> raise ex
            finally
                try
                    unlinkSync tempPath
                with _ ->
                    ()

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

    let leaseKey (sessionId: string) (agent: string) = sessionId + "\u001f" + agent

    let private failedTask<'T> (error: exn) : Task<'T> =
        let completion =
            TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

        completion.SetException(error)
        completion.Task

    type ModelRoutingRuntime(scheduler: obj) =
        let gate = obj ()
        let managed = Dictionary<string, ModelRoutingTarget>()
        let managedBySession = Dictionary<string, HashSet<string>>()
        let pending = ResizeArray<PendingDemand>()
        let pendingManaged = Dictionary<string, PendingDemand>()
        // DSL-MUTABLE: resource — process-local scheduler poison
        let mutable fatalError: exn option = None

        let running () = managed.Values |> Seq.toArray

        let ensureHealthy () = fatalError |> Option.iter raise

        let rememberManaged sessionId key target =
            managed.[key] <- target

            let keys =
                match managedBySession.TryGetValue sessionId with
                | true, existing -> existing
                | false, _ ->
                    let created = HashSet<string>()
                    managedBySession.[sessionId] <- created
                    created

            keys.Add key |> ignore

        let removeDemand demand =
            pending.Remove demand |> ignore
            pendingManaged.Remove demand.LeaseKey |> ignore

        let poison (error: exn) =
            fatalError <- Some error
            let waiting = pending |> Seq.toArray
            pending.Clear()
            pendingManaged.Clear()

            for demand in waiting do
                try
                    demand.Completion.SetException(error)
                with _ ->
                    ()

        let trySchedule role =
            invokeScheduler scheduler role (running ())

        let scheduleOrPoison role =
            try
                trySchedule role
            with ex ->
                poison ex
                raise ex

        let commit demand target =
            rememberManaged demand.SessionId demand.LeaseKey target
            removeDemand demand
            AsyncSupport.trySetResult demand.Completion target |> ignore

        let rec drainDemands () =
            ensureHealthy ()
            // DSL-MUTABLE: algorithm-scratch — drain-loop progress flag
            let mutable progress = true

            while progress && pending.Count > 0 do
                progress <- false
                let wave = pending |> Seq.toArray

                for demand in wave do
                    if pending.Contains demand then
                        match scheduleOrPoison demand.Role with
                        | None -> ()
                        | Some target ->
                            commit demand target
                            progress <- true

        let acquireManagedTask sessionId agent =
            lock gate (fun () ->
                ensureHealthy ()
                let key = leaseKey sessionId agent

                match managed.TryGetValue key with
                | true, target -> Task.FromResult target
                | false, _ ->
                    match pendingManaged.TryGetValue key with
                    | true, demand -> demand.Completion.Task
                    | false, _ ->
                        match scheduleOrPoison agent with
                        | Some target ->
                            rememberManaged sessionId key target
                            drainDemands ()
                            Task.FromResult target
                        | None ->
                            let completion =
                                TaskCompletionSource<ModelRoutingTarget>(
                                    TaskCreationOptions.RunContinuationsAsynchronously
                                )

                            let demand =
                                { SessionId = sessionId
                                  LeaseKey = key
                                  Role = agent
                                  Completion = completion }

                            pending.Add demand
                            pendingManaged.[key] <- demand
                            completion.Task)

        member _.AcquireManaged(sessionId: string, agent: string) : Task<ModelRoutingTarget> =
            if String.IsNullOrWhiteSpace sessionId then
                failedTask<ModelRoutingTarget> (ArgumentException("sessionId must be non-empty"))
            elif String.IsNullOrWhiteSpace agent then
                failedTask<ModelRoutingTarget> (ArgumentException("agent must be non-empty"))
            else
                try
                    acquireManagedTask (sessionId.Trim()) (agent.Trim())
                with ex ->
                    failedTask<ModelRoutingTarget> ex

        member _.TryAcquireManaged(sessionId: string, agent: string) : ModelRoutingTarget option =
            if String.IsNullOrWhiteSpace sessionId || String.IsNullOrWhiteSpace agent then
                None
            else
                lock gate (fun () ->
                    ensureHealthy ()
                    let sessionId = sessionId.Trim()
                    let agent = agent.Trim()
                    let key = leaseKey sessionId agent

                    match managed.TryGetValue key with
                    | true, target -> Some target
                    | false, _ when pendingManaged.ContainsKey key -> None
                    | false, _ ->
                        match scheduleOrPoison agent with
                        | None -> None
                        | Some target ->
                            rememberManaged sessionId key target
                            drainDemands ()
                            Some target)

        member _.TryLease(sessionId: string, agent: string) : ModelRoutingTarget option =
            if String.IsNullOrWhiteSpace sessionId || String.IsNullOrWhiteSpace agent then
                None
            else
                lock gate (fun () ->
                    match managed.TryGetValue(leaseKey (sessionId.Trim()) (agent.Trim())) with
                    | true, target -> Some target
                    | false, _ -> None)

        member _.ReleaseSession(sessionId: string) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                lock gate (fun () ->
                    let sessionId = sessionId.Trim()
                    // DSL-MUTABLE: algorithm-scratch — occupancy changed by this release
                    let mutable changed = false

                    match managedBySession.TryGetValue sessionId with
                    | true, keys ->
                        for key in keys |> Seq.toArray do
                            changed <- managed.Remove key || changed

                        managedBySession.Remove sessionId |> ignore
                    | false, _ -> ()

                    let cancelled =
                        pending
                        |> Seq.filter (fun demand -> demand.SessionId = sessionId)
                        |> Seq.toArray

                    for demand in cancelled do
                        removeDemand demand
                        AsyncSupport.trySetCanceled demand.Completion |> ignore

                    if changed && fatalError.IsNone then
                        drainDemands ())

        member _.CancelPendingSession(sessionId: string) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                lock gate (fun () ->
                    let sessionId = sessionId.Trim()

                    pending
                    |> Seq.filter (fun demand -> demand.SessionId = sessionId)
                    |> Seq.toArray
                    |> Array.iter (fun demand ->
                        removeDemand demand
                        AsyncSupport.trySetCanceled demand.Completion |> ignore))

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

    let acquireManaged (sessionId: SessionId) (agent: string) =
        current().AcquireManaged(SessionId.value sessionId, agent)

    let tryAcquireManaged (sessionId: SessionId) (agent: string) =
        current().TryAcquireManaged(SessionId.value sessionId, agent)

    let tryLease (sessionId: SessionId) (agent: string) =
        current().TryLease(SessionId.value sessionId, agent)

    let releaseSession (sessionId: SessionId) =
        current().ReleaseSession(SessionId.value sessionId)

    let cancelUnacquiredSession (sessionId: SessionId) =
        current().CancelPendingSession(SessionId.value sessionId)
