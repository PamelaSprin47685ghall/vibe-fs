namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome

/// JS-native model-routing observation boundary. Scheduler policy remains in
/// the configured MJS provider; JS tests observe only the selected target.
module ModelRoutingSurface =

    type private RuntimeHandle(runtime: ModelRouting.ModelRoutingRuntime) =
        member _.Runtime = runtime

    type private PortHandle(port: IOpenCodePort) =
        member _.Port = port

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0[$1]"

    let private field (value: obj) (names: string list) : obj =
        if isNullish value then
            null
        else
            names
            |> List.tryPick (fun name ->
                let item = property value name
                if isNullish item then None else Some item)
            |> Option.defaultValue null

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private optionalObject (value: obj) : obj option =
        if isNullish value then None else Some value

    let private targetObject (target: ModelRoutingTarget) : obj =
        box
            {| model = target.Model
               reasoning = target.Reasoning |}

    let private acquisitionObject =
        function
        | ModelRoutingAcquisition.Acquired target ->
            box
                {| kind = "Acquired"
                   target = targetObject target |}
        | ModelRoutingAcquisition.Superseded -> box {| kind = "Superseded"; target = null |}

    let private targetOf (value: obj) : ModelRoutingTarget =
        if isNullish value then
            invalidArg "running" "execution-model-routing: running target must be non-null"

        { Model = text (field value [ "model"; "Model" ])
          Reasoning = text (field value [ "reasoning"; "Reasoning" ]) }

    let private targetsOf (value: obj) : ModelRoutingTarget array = arrayOf value |> Array.map targetOf

    let private runtimeOf (value: obj) : ModelRouting.ModelRoutingRuntime = (unbox<RuntimeHandle> value).Runtime

    let private portOf (value: obj) : IOpenCodePort = (unbox<PortHandle> value).Port

    let private modelOf (value: obj) : OpencodeModel option =
        if isNullish value then
            None
        else
            Some
                { providerID = text (field value [ "providerID"; "providerId" ])
                  modelID = text (field value [ "modelID"; "modelId" ])
                  variant = optionalText (field value [ "variant" ]) }

    let private toolsOf (value: obj) : Map<string, bool> option =
        let raw = field value [ "tools"; "Tools" ]

        if isNullish raw then
            None
        else
            let keys: string array = emitJsExpr raw "Object.keys($0)"

            keys
            |> Array.map (fun key -> key, unbox<bool> (property raw key))
            |> Map.ofArray
            |> Some

    let private bindingIntentOf (value: obj) : SessionBindingIntent =
        match text (field value [ "bindingIntent"; "BindingIntent" ]) with
        | "ExplicitExecutionOverride" -> SessionBindingIntent.ExplicitExecutionOverride
        | _ -> SessionBindingIntent.Preserve

    let private promptOptionsOf (value: obj) : OpenCodePromptOptions =
        { Model = modelOf (field value [ "model"; "Model" ])
          Agent = optionalText (field value [ "agent"; "Agent" ])
          Directory = optionalText (field value [ "directory"; "Directory" ])
          Metadata = optionalObject (field value [ "metadata"; "Metadata" ])
          Tools = toolsOf value
          BindingIntent = bindingIntentOf value }

    let private outcomeToJs (outcome: SendOutcome) : obj =
        match outcome with
        | AdmittedWithReceipt receipt ->
            box
                {| kind = "AdmittedWithReceipt"
                   receipt = TransportReceipt.value receipt
                   physical = null
                   error = null |}
        | AdmittedWithPhysicalMessage physical ->
            box
                {| kind = "AdmittedWithPhysicalMessage"
                   receipt = null
                   physical = PhysicalUserMessageId.value physical
                   error = null |}
        | Retryable reason ->
            box
                {| kind = "Retryable"
                   receipt = null
                   physical = null
                   error = reason |}
        | AcceptanceUnknown reason ->
            box
                {| kind = "AcceptanceUnknown"
                   receipt = null
                   physical = null
                   error = reason |}
        | Fatal reason ->
            box
                {| kind = "Fatal"
                   receipt = null
                   physical = null
                   error = reason |}

    /// Initialize the process-shared scheduler runtime used by Host admission.
    let initialize () : Task = ModelRouting.initialize ()

    /// Acquire from the process-shared Host runtime and expose the lifecycle
    /// outcome without translating expected supersession into Promise rejection.
    let acquire (sessionId: string) (physicalUserMessageId: string) (agent: string) : Task<obj> =
        task {
            let! acquisition =
                ModelRouting.acquireManagedExecution
                    (SessionId.create sessionId)
                    (PhysicalUserMessageId.create physicalUserMessageId)
                    agent

            return acquisitionObject acquisition
        }

    /// Release the current process-shared execution for a reusable session.
    let release (sessionId: string) : unit =
        ModelRouting.releaseExecution (SessionId.create sessionId)

    /// Release only the process-shared execution proven to belong to this exact
    /// physical user material. A stale terminal observation for an older turn is
    /// therefore harmless after the SessionId has been reused.
    let releasePhysical (sessionId: string) (physicalUserMessageId: string) : unit =
        ModelRouting.releasePhysicalExecution
            (SessionId.create sessionId)
            (PhysicalUserMessageId.create physicalUserMessageId)

    /// Load the user-visible scheduler module through the owner boundary. The
    /// returned function is an opaque JS capability and is never introspected by
    /// the semantic caller.
    let bootstrapAndLoadAt (path: string) (template: string) : Task<obj> =
        ModelRouting.bootstrapAndLoadAt path template

    /// Invoke a scheduler with plain JS target observations. `null` means the
    /// scheduler declined the demand; target validation remains owned by routing.
    let invokeScheduler (scheduler: obj) (role: string) (running: obj) (previous: obj) : obj =
        ModelRouting.invokeScheduler
            scheduler
            role
            (targetsOf running)
            (if isNullish previous then None else Some(targetOf previous))
        |> Option.map targetObject
        |> Option.defaultValue null

    /// Construct an isolated routing runtime around an injected scheduler. The
    /// handle is opaque; all observations and mutations remain on this surface.
    let createRuntime (scheduler: obj) : obj =
        RuntimeHandle(ModelRouting.ModelRoutingRuntime(scheduler)) :> obj

    let acquireManaged (runtime: obj) (sessionId: string) (physicalUserMessageId: string) (agent: string) : Task<obj> =
        task {
            let! acquisition =
                (runtimeOf runtime)
                    .AcquireManagedExecution(sessionId, physicalUserMessageId, agent)

            return acquisitionObject acquisition
        }

    let tryReserveManaged (runtime: obj) (sessionId: string) (agent: string) : obj =
        (runtimeOf runtime).TryReserveManaged(sessionId, agent)
        |> Option.map targetObject
        |> Option.defaultValue null

    let tryLease (runtime: obj) (sessionId: string) (physicalUserMessageId: string) (agent: string) : obj =
        (runtimeOf runtime).TryLease(sessionId, physicalUserMessageId, agent)
        |> Option.map targetObject
        |> Option.defaultValue null

    let releaseExecution (runtime: obj) (sessionId: string) : unit =
        (runtimeOf runtime).ReleaseExecution(sessionId)

    let releasePhysicalExecution (runtime: obj) (sessionId: string) (physicalUserMessageId: string) : unit =
        (runtimeOf runtime).ReleasePhysicalExecution(sessionId, physicalUserMessageId)

    let cancelPendingExecution (runtime: obj) (sessionId: string) : unit =
        (runtimeOf runtime).CancelPendingExecution(sessionId)

    let bindCapacityChild (runtime: obj) (parentSessionId: string) (childSessionId: string) : unit =
        (runtimeOf runtime).BindCapacityChild(parentSessionId, childSessionId)

    let enterProviderStep
        (runtime: obj)
        (sessionId: string)
        (physicalUserMessageId: string)
        (visibleProviderRuns: string array)
        : Task =
        (runtimeOf runtime).EnterProviderStep(sessionId, physicalUserMessageId, visibleProviderRuns |> Set.ofArray)

    let endProviderStep
        (runtime: obj)
        (sessionId: string)
        (physicalUserMessageId: string)
        (providerRun: string)
        : unit =
        (runtimeOf runtime).EndProviderStep(sessionId, physicalUserMessageId, providerRun)

    let suppressProviderStep (runtime: obj) (sessionId: string) (physicalUserMessageId: string) : unit =
        (runtimeOf runtime).SuppressProviderStep(sessionId, physicalUserMessageId)

    let snapshotOccupied (runtime: obj) : obj array =
        (runtimeOf runtime).SnapshotOccupied() |> Array.map targetObject

    let pendingCount (runtime: obj) : int = (runtimeOf runtime).PendingCount

    /// Create an SDK-backed prompt port without exposing the Fable class. The
    /// port keeps prompt_async enqueue semantics, including fire-and-forget
    /// observation of the Host run promise.
    let createSdkClientPort (client: obj) : obj =
        PortHandle(OpenCodePort.SdkClientPort(client, None) :> IOpenCodePort) :> obj

    let sendPrompt (port: obj) (sessionId: string) (text: string) (options: obj) : Task<obj> =
        task {
            let! outcome = (portOf port).SendPrompt (SessionId.create sessionId) text (promptOptionsOf options)
            return outcomeToJs outcome
        }
