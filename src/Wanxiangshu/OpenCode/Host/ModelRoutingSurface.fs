namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Execution.Failure

/// JS-native model-routing observation boundary. Scheduler policy remains in
/// the configured MJS provider; JS tests observe only the selected target.
module ModelRoutingSurface =

    type private RuntimeHandle(runtime: ModelRouting.ModelRoutingRuntime) =
        member _.Runtime = runtime

    type private PortHandle(port: IOpenCodePort) =
        member _.Port = port

    [<Emit("new WeakMap()")>]
    let private createWeakMap () : obj = jsNative

    let private executionAdmissionLeases = createWeakMap ()
    let private executionAdmissionTokens = createWeakMap ()
    let private queuedAdmissionNodes = createWeakMap ()

    [<Emit("Object.freeze(Object.create(null))")>]
    let private opaqueLeaseToken () : obj = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private rememberOpaqueLease (leases: obj) (token: obj) (lease: ExecutionAdmissionLease) : unit = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private rememberQueuedNode (nodes: obj) (token: obj) (node: ExecutionAdmissionQueueNode) : unit = jsNative

    [<Emit("$0.get($1)")>]
    let private queuedNodeValue (nodes: obj) (token: obj) : ExecutionAdmissionQueueNode = jsNative

    [<Emit("$0.has($1)")>]
    let private hasOpaqueLease (leases: obj) (token: obj) : bool = jsNative

    [<Emit("$0.get($1)")>]
    let private opaqueLeaseValue (leases: obj) (token: obj) : ExecutionAdmissionLease = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private rememberOpaqueToken (tokens: obj) (lease: ExecutionAdmissionLease) (token: obj) : unit = jsNative

    [<Emit("$0.has($1)")>]
    let private hasOpaqueToken (tokens: obj) (lease: ExecutionAdmissionLease) : bool = jsNative

    [<Emit("$0.get($1)")>]
    let private opaqueTokenValue (tokens: obj) (lease: ExecutionAdmissionLease) : obj = jsNative

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

    [<Emit("(function freeze(value) { if (value !== null && typeof value === 'object' && !Object.isFrozen(value)) { Object.freeze(value); Object.keys(value).forEach(function (key) { freeze(value[key]); }); } return value; })($0)")>]
    let private deepFreeze (value: obj) : obj = jsNative


    let private targetOf (value: obj) : ModelRoutingTarget =
        if isNullish value then
            invalidArg "running" "execution-model-routing: running target must be non-null"

        { Model = text (field value [ "model"; "Model" ])
          Reasoning = text (field value [ "reasoning"; "Reasoning" ]) }

    let private admissionIdentityOf (value: obj) : ExecutionAdmissionExactIdentity =
        { SessionId = text (field value [ "sessionId"; "SessionId" ])
          PhysicalUserMessageId = text (field value [ "physicalUserMessageId"; "PhysicalUserMessageId" ])
          EffectiveAgent = text (field value [ "effectiveAgent"; "EffectiveAgent" ])
          Target = targetOf (field value [ "target"; "Target" ]) }

    let private int64Of (value: obj) : int64 = value |> unbox<float> |> int64

    let private intOf (value: obj) : int = unbox value

    [<Emit("Number($0)")>]
    let private numberOfInt64 (value: int64) : float = jsNative

    let private ownerObject (owner: CapacityExactOwnerSnapshot) : obj =
        box
            {| sessionId = owner.SessionId
               physicalUserMessageId = owner.PhysicalUserMessageId
               effectiveAgent = owner.EffectiveAgent |> Option.map box |> Option.defaultValue null |}

    let private ownerOf (value: obj) : CapacityExactOwnerSnapshot =
        { SessionId = text (field value [ "sessionId"; "SessionId" ])
          PhysicalUserMessageId = text (field value [ "physicalUserMessageId"; "PhysicalUserMessageId" ])
          EffectiveAgent = optionalText (field value [ "effectiveAgent"; "EffectiveAgent" ]) }

    let private invariantEvidenceOf (value: obj) : CapacityInvariantEvidence =
        let ledgerEntries =
            field value [ "ledgerEntries"; "LedgerEntries" ]
            |> arrayOf
            |> Array.map (fun entry ->
                { Credit = int64Of (field entry [ "credit"; "Credit" ])
                  Target = targetOf (field entry [ "target"; "Target" ]) })

        let tokens =
            field value [ "tokens"; "Tokens" ]
            |> arrayOf
            |> Array.map (fun token ->
                { Credit = int64Of (field token [ "credit"; "Credit" ])
                  State = text (field token [ "state"; "State" ])
                  Owner = ownerOf (field token [ "owner"; "Owner" ])
                  Target = targetOf (field token [ "target"; "Target" ]) })

        let custodies =
            field value [ "custodies"; "Custodies" ]
            |> arrayOf
            |> Array.map (fun custody ->
                { Credit = int64Of (field custody [ "credit"; "Credit" ])
                  Owner = ownerOf (field custody [ "owner"; "Owner" ]) })

        let waiters =
            field value [ "waiters"; "Waiters" ]
            |> arrayOf
            |> Array.map (fun waiter ->
                { Owner = ownerOf waiter
                  Sequence = int64Of (field waiter [ "sequence"; "Sequence" ])
                  Kind = text (field waiter [ "kind"; "Kind" ]) })

        let lineage =
            field value [ "lineage"; "Lineage" ]
            |> arrayOf
            |> Array.map (fun edge ->
                { ParentSessionId = text (field edge [ "parentSessionId"; "ParentSessionId" ])
                  ChildSessionId = text (field edge [ "childSessionId"; "ChildSessionId" ]) })

        let stateCounts = field value [ "tokenStateCounts"; "TokenStateCounts" ]
        let counters = field value [ "counters"; "Counters" ]

        { LedgerEntries = ledgerEntries
          Tokens = tokens
          Custodies = custodies
          Executions = field value [ "executions"; "Executions" ] |> arrayOf |> Array.map ownerOf
          Waiters = waiters
          Owners = field value [ "owners"; "Owners" ] |> arrayOf |> Array.map ownerOf
          Lineage = lineage
          IdleCount = intOf (field stateCounts [ "idle"; "Idle" ])
          InFlightCount = intOf (field stateCounts [ "inFlight"; "InFlight" ])
          RetiringCount = intOf (field stateCounts [ "retiring"; "Retiring" ])
          ActiveCount = intOf (field value [ "activeCount"; "ActiveCount" ])
          Counters =
            { Duplicate = int64Of (field counters [ "duplicate"; "Duplicate" ])
              Stale = int64Of (field counters [ "stale"; "Stale" ])
              Conflict = int64Of (field counters [ "conflict"; "Conflict" ]) } }

    let private reconciliationFailureName =
        function
        | CapacityReconciliationFailure.ActiveOutsideLedgerBounds -> "ActiveOutsideLedgerBounds"
        | CapacityReconciliationFailure.TokenStateCountMismatch -> "TokenStateCountMismatch"
        | CapacityReconciliationFailure.MapLedgerDivergence -> "MapLedgerDivergence"
        | CapacityReconciliationFailure.UntraceableTokenOwner -> "UntraceableTokenOwner"
        | CapacityReconciliationFailure.UntraceableWaiterOwner -> "UntraceableWaiterOwner"
        | CapacityReconciliationFailure.UntraceableExecutionCustody -> "UntraceableExecutionCustody"
        | CapacityReconciliationFailure.CounterRegression -> "CounterRegression"

    let private capacitySnapshotObject (snapshot: CapacityInvariantEvidence) : obj =
        let ledgerEntries =
            snapshot.LedgerEntries
            |> Array.map (fun entry ->
                box
                    {| credit = numberOfInt64 entry.Credit
                       target = targetObject entry.Target |})

        let tokens =
            snapshot.Tokens
            |> Array.map (fun token ->
                box
                    {| credit = numberOfInt64 token.Credit
                       state = token.State
                       owner = ownerObject token.Owner
                       target = targetObject token.Target |})

        let custodies =
            snapshot.Custodies
            |> Array.map (fun custody ->
                box
                    {| credit = numberOfInt64 custody.Credit
                       owner = ownerObject custody.Owner |})

        let waiters =
            snapshot.Waiters
            |> Array.map (fun waiter ->
                box
                    {| sessionId = waiter.Owner.SessionId
                       physicalUserMessageId = waiter.Owner.PhysicalUserMessageId
                       effectiveAgent = waiter.Owner.EffectiveAgent |> Option.map box |> Option.defaultValue null
                       sequence = numberOfInt64 waiter.Sequence
                       kind = waiter.Kind |})

        box
            {| ledgerEntries = ledgerEntries
               tokens = tokens
               custodies = custodies
               executions = snapshot.Executions |> Array.map ownerObject
               waiters = waiters
               owners = snapshot.Owners |> Array.map ownerObject
               lineage =
                snapshot.Lineage
                |> Array.map (fun edge ->
                    box
                        {| parentSessionId = edge.ParentSessionId
                           childSessionId = edge.ChildSessionId |})
               tokenStateCounts =
                {| idle = snapshot.IdleCount
                   inFlight = snapshot.InFlightCount
                   retiring = snapshot.RetiringCount |}
               activeCount = snapshot.ActiveCount
               counters =
                {| duplicate = numberOfInt64 snapshot.Counters.Duplicate
                   stale = numberOfInt64 snapshot.Counters.Stale
                   conflict = numberOfInt64 snapshot.Counters.Conflict |} |}
        |> deepFreeze

    let private failureName =
        function
        | ExecutionFailure.ProtocolRejection -> "ProtocolRejection"
        | ExecutionFailure.Superseded -> "Superseded"
        | ExecutionFailure.UserCancelled -> "UserCancelled"
        | ExecutionFailure.CapacityQueueFull -> "CapacityQueueFull"
        | ExecutionFailure.LocalInvariant
        | ExecutionFailure.AuthorizationDenied
        | ExecutionFailure.ProviderTransient
        | ExecutionFailure.ProviderPermanent
        | ExecutionFailure.AcceptanceUnknown
        | ExecutionFailure.StreamInterruptedAfterFirstToken
        | ExecutionFailure.PersistenceFailure _ -> invalidOp "unexpected capacity boundary failure"

    let private transitionOutcomeObject =
        function
        | CapacityTransitionOutcome.Applied -> box {| kind = "Applied" |}
        | CapacityTransitionOutcome.AlreadyApplied -> box {| kind = "AlreadyApplied" |}
        | CapacityTransitionOutcome.StaleFence -> box {| kind = "StaleFence" |}
        | CapacityTransitionOutcome.Conflict -> box {| kind = "Conflict" |}

    let private leaseOf token =
        if hasOpaqueLease executionAdmissionLeases token then
            Some(opaqueLeaseValue executionAdmissionLeases token)
        else
            None

    let private terminalAcquisitionObject kind acquisition =
        let failure =
            ModelRouting.failureOfExecutionAdmissionAcquisition acquisition
            |> Option.map failureName
            |> Option.defaultWith (fun () -> invalidOp "terminal acquisition requires typed failure")

        box
            {| kind = kind
               failure = failure
               lease = null
               queue = null |}

    let private acquireAdmissionObject =
        function
        | ExecutionAdmissionAcquisition.QueueFull as acquisition -> terminalAcquisitionObject "QueueFull" acquisition
        | ExecutionAdmissionAcquisition.Cancelled as acquisition -> terminalAcquisitionObject "Cancelled" acquisition
        | ExecutionAdmissionAcquisition.Superseded as acquisition -> terminalAcquisitionObject "Superseded" acquisition
        | ExecutionAdmissionAcquisition.Queued node ->
            let token = opaqueLeaseToken ()
            rememberQueuedNode queuedAdmissionNodes token node

            box
                {| kind = "Queued"
                   failure = null
                   lease = null
                   queue = token |}
        | ExecutionAdmissionAcquisition.Admitted lease ->
            match ModelRouting.capacityOwnership lease with
            | CapacityOwnership.NoCapacityFence -> invalidOp "acquired admission requires exact fence"
            | CapacityOwnership.OwnsExactFence _ -> ()

            let token =
                if hasOpaqueToken executionAdmissionTokens lease then
                    opaqueTokenValue executionAdmissionTokens lease
                else
                    let created = opaqueLeaseToken ()
                    rememberOpaqueToken executionAdmissionTokens lease created
                    rememberOpaqueLease executionAdmissionLeases created lease
                    created

            box
                {| kind = "Acquired"
                   failure = null
                   lease = token
                   queue = null |}

    let rec private awaitAdmission =
        function
        | ExecutionAdmissionAcquisition.Queued node ->
            task {
                let! completed = node.Completion.Task
                return! awaitAdmission completed
            }
        | acquisition -> Task.FromResult acquisition

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

    let acquireSharedExecutionAdmission
        (sessionId: string)
        (physicalUserMessageId: string)
        (effectiveAgent: string)
        : Task<obj> =
        task {
            let! acquisition =
                ModelRouting.acquireExecutionAdmission
                    (SessionId.create sessionId)
                    (PhysicalUserMessageId.create physicalUserMessageId)
                    effectiveAgent

            let! completed = awaitAdmission acquisition
            return acquireAdmissionObject completed
        }

    let sharedExecutionAdmissionTarget (token: obj) : obj =
        match leaseOf token with
        | None -> null
        | Some lease ->
            match ModelRouting.executionAdmissionTarget lease with
            | Ok target -> targetObject target
            | Error _ -> null

    let sharedCapacitySnapshot () : obj =
        ModelRouting.capacitySnapshot () |> capacitySnapshotObject

    let commitSharedExecutionAdmission (token: obj) (observed: obj) : obj =
        match leaseOf token with
        | None -> CapacityTransitionOutcome.StaleFence |> transitionOutcomeObject
        | Some lease ->
            ModelRouting.commitExecutionAdmission lease (admissionIdentityOf observed)
            |> transitionOutcomeObject

    let releaseSharedExecutionAdmissionBeforeProvider (token: obj) (observed: obj) : obj =
        match leaseOf token with
        | None -> CapacityTransitionOutcome.StaleFence |> transitionOutcomeObject
        | Some lease ->
            ModelRouting.releaseExecutionAdmissionBeforeProvider lease (admissionIdentityOf observed)
            |> transitionOutcomeObject

    /// Release only the process-shared execution proven to belong to this exact
    /// physical user material. A stale terminal observation for an older turn is
    /// therefore harmless after the SessionId has been reused.
    let releasePhysical (sessionId: string) (physicalUserMessageId: string) : obj =
        ModelRouting.releasePhysicalExecution
            (SessionId.create sessionId)
            (PhysicalUserMessageId.create physicalUserMessageId)
        |> transitionOutcomeObject

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

    let acquireExecutionAdmission
        (runtime: obj)
        (sessionId: string)
        (physicalUserMessageId: string)
        (effectiveAgent: string)
        : Task<obj> =
        task {
            let! acquisition =
                (runtimeOf runtime)
                    .AcquireExecutionAdmission(sessionId, physicalUserMessageId, effectiveAgent)

            let! completed = awaitAdmission acquisition
            return acquireAdmissionObject completed
        }

    let beginExecutionAdmission
        (runtime: obj)
        (sessionId: string)
        (physicalUserMessageId: string)
        (effectiveAgent: string)
        : Task<obj> =
        task {
            let! acquisition =
                (runtimeOf runtime)
                    .AcquireExecutionAdmission(sessionId, physicalUserMessageId, effectiveAgent)

            return acquireAdmissionObject acquisition
        }

    let awaitQueuedExecutionAdmission (queueToken: obj) : Task<obj> =
        if hasOpaqueLease queuedAdmissionNodes queueToken then
            task {
                let node = queuedNodeValue queuedAdmissionNodes queueToken
                let! completed = node.Completion.Task
                let! terminal = awaitAdmission completed
                return acquireAdmissionObject terminal
            }
        else
            let rejected =
                TaskCompletionSource<obj>(TaskCreationOptions.RunContinuationsAsynchronously)

            rejected.SetException(System.InvalidOperationException "execution-model-routing: unknown queue node")

            rejected.Task

    let executionAdmissionTarget (runtime: obj) (token: obj) : obj =
        match leaseOf token with
        | None -> null
        | Some lease ->
            match (runtimeOf runtime).ExecutionAdmissionTarget lease with
            | Ok target -> targetObject target
            | Error _ -> null

    let commitExecutionAdmission (runtime: obj) (token: obj) (observed: obj) : obj =
        match leaseOf token with
        | None -> CapacityTransitionOutcome.StaleFence |> transitionOutcomeObject
        | Some lease ->
            (runtimeOf runtime)
                .CommitExecutionAdmission(lease, admissionIdentityOf observed)
            |> transitionOutcomeObject

    let releaseExecutionAdmissionBeforeProvider (runtime: obj) (token: obj) (observed: obj) : obj =
        match leaseOf token with
        | None -> CapacityTransitionOutcome.StaleFence |> transitionOutcomeObject
        | Some lease ->
            (runtimeOf runtime)
                .ReleaseExecutionAdmissionBeforeProvider(lease, admissionIdentityOf observed)
            |> transitionOutcomeObject

    let executionAdmissionLifecycle (runtime: obj) (token: obj) : obj =
        match leaseOf token with
        | None -> null
        | Some lease ->
            match (runtimeOf runtime).ExecutionAdmissionLifecycle lease with
            | Ok name -> box name
            | Error _ -> null

    let tryReserveManaged (runtime: obj) (sessionId: string) (agent: string) : obj =
        (runtimeOf runtime).TryReserveManaged(sessionId, agent)
        |> Option.map targetObject
        |> Option.defaultValue null

    let tryLease (runtime: obj) (sessionId: string) (physicalUserMessageId: string) (agent: string) : obj =
        (runtimeOf runtime).TryLease(sessionId, physicalUserMessageId, agent)
        |> Option.map targetObject
        |> Option.defaultValue null

    let releasePhysicalExecution (runtime: obj) (sessionId: string) (physicalUserMessageId: string) : obj =
        (runtimeOf runtime).ReleasePhysicalExecution(sessionId, physicalUserMessageId)
        |> transitionOutcomeObject

    let cancelPendingExecution (runtime: obj) (sessionId: string) : obj =
        (runtimeOf runtime).CancelPendingExecution(sessionId) |> transitionOutcomeObject

    let bindCapacityChild (runtime: obj) (parentSessionId: string) (childSessionId: string) : unit =
        (runtimeOf runtime).BindCapacityChild(parentSessionId, childSessionId)

    let bindCapacityCompanion (runtime: obj) (ownerSessionId: string) (bloggerSessionId: string) : unit =
        (runtimeOf runtime).BindCapacityCompanion(ownerSessionId, bloggerSessionId)

    let dropCapacityLineage (runtime: obj) (sessionId: string) : unit =
        (runtimeOf runtime).DropCapacityLineage(sessionId)

    let enterProviderStep
        (runtime: obj)
        (sessionId: string)
        (physicalUserMessageId: string)
        (visibleProviderRuns: string array)
        : Task =
        (runtimeOf runtime)
            .EnterProviderStep(sessionId, physicalUserMessageId, visibleProviderRuns |> Set.ofArray)

    let endProviderStep
        (runtime: obj)
        (sessionId: string)
        (physicalUserMessageId: string)
        (providerRun: string)
        : unit =
        (runtimeOf runtime)
            .EndProviderStep(sessionId, physicalUserMessageId, providerRun)

    let suppressProviderStep (runtime: obj) (sessionId: string) (physicalUserMessageId: string) : unit =
        (runtimeOf runtime).SuppressProviderStep(sessionId, physicalUserMessageId)

    let snapshotOccupied (runtime: obj) : obj array =
        (runtimeOf runtime).SnapshotOccupied() |> Array.map targetObject

    let capacitySnapshot (runtime: obj) : obj =
        (runtimeOf runtime).CapacitySnapshot() |> capacitySnapshotObject

    let reconcileCapacityEvidence (evidence: obj) : obj =
        match evidence |> invariantEvidenceOf |> CapacityReconciliation.decide with
        | CapacityReconciliationDecision.NoOp -> box {| kind = "NoOp" |}
        | CapacityReconciliationDecision.FailClosed failures ->
            box
                {| kind = "FailClosed"
                   reasons = failures |> Array.map reconciliationFailureName |}
            |> deepFreeze

    let pendingCount (runtime: obj) : int = (runtimeOf runtime).PendingCount

    let admissionSnapshot (routingRuntime: obj) (sessionId: string) (physicalUserMessageId: string) : obj =
        box
            {| activeCapacity = snapshotOccupied routingRuntime |> Array.length
               pendingAdmissions = pendingCount routingRuntime
               providerBinding =
                SessionExecutionBinding.exactExecutionBindingCount
                    (SessionId.create sessionId)
                    (PhysicalUserMessageId.create physicalUserMessageId) |}

    let pendingBound (runtime: obj) : int = (runtimeOf runtime).PendingBound

    let pendingContractVersion (runtime: obj) : int =
        (runtimeOf runtime).PendingContractVersion

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
