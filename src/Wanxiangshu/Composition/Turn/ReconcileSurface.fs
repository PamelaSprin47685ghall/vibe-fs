namespace Wanxiangshu.Composition.Turn

open Wanxiangshu.Execution.Failure

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// JS boundary for reconciliation's classifiers, evidence, wakes and publish
/// seals. Maps and idle permits remain opaque handles; decisions and
/// observations cross as plain objects or stable strings.
module ReconcileSurface =

    type private PublishMapsHandle(maps: ReconcileProgram.PublishMaps) =
        member _.Maps = maps

    [<Emit("$0==null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0[$1]"

    let private stringOf (value: obj) =
        if isNullish value then "" else string value

    let private boolOf (value: obj) =
        if isNullish value then false else unbox<bool> value

    let private mapsOf (value: obj) = (value :?> PublishMapsHandle).Maps

    let private outcomeOf (value: obj) : ReconcileProgram.TurnOutcome =
        ReconcileProgram.outcomeOf (stringOf value)

    let private wakeOf (value: obj) : ReconcileProgram.ReconcileWake =
        match stringOf (property value "kind") with
        | "IdleWake" ->
            property value "permit"
            |> unbox<QuiescencePermit>
            |> ReconcileProgram.ReconcileWake.IdleWake
        | "RetryWake" -> ReconcileProgram.ReconcileWake.RetryWake
        | "FailureWake" ->
            let physical = stringOf (property value "physical")
            let reason = stringOf (property value "reason")

            ReconcileProgram.ReconcileWake.FailureWake(
                (if System.String.IsNullOrWhiteSpace physical then
                     None
                 else
                     Some(PhysicalUserMessageId.create physical)),
                ExecutionFailure.ProviderTransient,
                (if System.String.IsNullOrWhiteSpace reason then
                     "provider failure"
                 else
                     reason),
                (if stringOf (property value "source") = "ExactAssistantProjection" then
                     ReconcileProgram.FailureWakeSource.ExactAssistantProjection
                 else
                     ReconcileProgram.FailureWakeSource.CoarseHostSignal)
            )
        | "AbortWake" -> ReconcileProgram.ReconcileWake.AbortWake
        | other -> invalidArg "wake" (sprintf "unknown reconcile wake: %s" other)

    let private evidenceOf (value: obj) : ReconcileProgram.ReconcileEvidence =
        match stringOf (property value "kind") with
        | "SnapshotError" -> ReconcileProgram.evidenceSnapshotError (stringOf (property value "reason"))
        | "NoTurn" -> ReconcileProgram.evidenceNoTurn ()
        | "Provisional" -> ReconcileProgram.evidenceProvisional (outcomeOf (property value "outcome"))
        | "Unknown" -> ReconcileProgram.evidenceUnknown ()
        | "Terminal" ->
            let physical = stringOf (property value "physical")
            let outcome = outcomeOf (property value "outcome")

            if System.String.IsNullOrWhiteSpace physical then
                ReconcileProgram.evidenceTerminal outcome
            else
                ReconcileProgram.turnFixture "terminal-session" physical "terminal-provider-run" outcome
                |> ReconcileProgram.observedTurn
                |> ReconcileProgram.ReconcileEvidence.Terminal
        | "SessionCleared" -> ReconcileProgram.evidenceSessionCleared ()
        | other -> invalidArg "evidence" (sprintf "unknown reconcile evidence: %s" other)

    let private decisionToJs (decision: ReconcileProgram.ReconcileDecision) (rereadsRemaining: int) : obj =
        let name = ReconcileProgram.decisionName decision

        let rereads =
            match decision with
            | ReconcileProgram.ReconcileDecision.Reread(_, remaining) -> remaining
            | ReconcileProgram.ReconcileDecision.Publish
            | ReconcileProgram.ReconcileDecision.StopPass -> rereadsRemaining

        box
            {| name = name
               clearsContinuationCandidate = ReconcileProgram.clearsContinuationCandidate decision
               rereadsRemaining = rereads |}

    let private turnOf (value: obj) : ReconcileProgram.PublishTurn =
        ReconcileProgram.turnFixture
            (stringOf (property value "session"))
            (stringOf (property value "physical"))
            (stringOf (property value "providerRun"))
            (ReconcileProgram.outcomeOf (stringOf (property value "outcome")))

    let private turnObject (session: string) (physical: string) (providerRun: string) (outcome: string) : obj =
        box
            {| session = session
               physical = physical
               providerRun = providerRun
               outcome = outcome |}

    let empty () : obj =
        PublishMapsHandle(ReconcileProgram.publishMapsEmpty ()) :> obj

    let turnFixture (value: obj) : obj =
        turnObject
            (stringOf (property value "session"))
            (stringOf (property value "physical"))
            (stringOf (property value "providerRun"))
            (stringOf (property value "outcome"))

    /// Stable JS contract for the fields accepted by `turnFixture` and the
    /// publish seal operations. This is an owner-defined vocabulary view, not
    /// Fable reflection metadata.
    let acceptedTurnFields () : string array =
        [| "session"; "physical"; "providerRun"; "outcome" |]

    /// Classify a publishable turn from its JS outcome name. `TurnUnknown` is
    /// deliberately rejected by the domain classifier: it is a snapshot
    /// observation, never a business turn.
    let classifyTurn (outcomeName: string) : obj =
        let outcome = ReconcileProgram.outcomeOf outcomeName
        let terminal = ReconcileProgram.isTerminalOutcome outcome

        box
            {| outcome = outcomeName
               state = if terminal then "terminal" else "provisional"
               isTerminal = terminal |}

    let isTerminalOutcome (outcomeName: string) : bool =
        ReconcileProgram.outcomeOf outcomeName |> ReconcileProgram.isTerminalOutcome

    /// These predicates keep structural clean-break checks at the owner boundary
    /// without exporting the underlying union constructors or case metadata.
    let isPublishableOutcome (outcomeName: string) : bool =
        outcomeName <> "TurnUnknown" && outcomeName <> "AbortWake"

    let isSnapshotObservation (observationName: string) : bool = observationName = "TurnUnknown"

    let private resolveCanonicalName (outcomeName: string) (outcome: ReconcileProgram.TurnOutcome) =
        match outcomeName with
        | "TurnInProgress"
        | "TurnNeedsContinuation"
        | "TurnCompleted"
        | "TurnAborted"
        | "TurnFailed" -> outcomeName
        | _ when ReconcileProgram.isTerminalOutcome outcome -> "TurnFailed"
        | _ -> "TurnInProgress"

    let tryOutcome (outcomeName: string) : obj =
        try
            let outcome = ReconcileProgram.outcomeOf outcomeName
            let canonicalName = resolveCanonicalName outcomeName outcome

            box
                {| accepted = true
                   name = canonicalName |}
        with error ->
            box
                {| accepted = false
                   error = error.Message |}

    // ── wake and evidence observations ───────────────────────────────────────

    let idleWake (session: string) : obj =
        let sessionId = SessionId.create session
        let gate = SessionQuiescenceGate()
        gate.BeginProviderAttempt sessionId
        let permit = gate.ObserveIdle sessionId

        box
            {| kind = "IdleWake"
               hasQuiescence = true
               permit = permit |}

    let retryWake () : obj =
        box
            {| kind = "RetryWake"
               hasQuiescence = false |}

    let failureWake () : obj =
        box
            {| kind = "FailureWake"
               physical = ""
               reason = "provider failure"
               source = "CoarseHostSignal"
               hasQuiescence = false |}

    let failureWakeFor (physical: string) : obj =
        box
            {| kind = "FailureWake"
               physical = physical
               reason = "provider failure"
               source = "ExactAssistantProjection"
               hasQuiescence = false |}

    let abortWake () : obj =
        box
            {| kind = "AbortWake"
               hasQuiescence = false |}

    let mergeWakeKind (currentPhysical: string) (previous: obj) (incoming: obj) =
        ReconcileProgram.mergeWake
            (if System.String.IsNullOrWhiteSpace currentPhysical then
                 None
             else
                 Some(PhysicalUserMessageId.create currentPhysical))
            (wakeOf previous)
            (wakeOf incoming)
        |> function
            | ReconcileProgram.ReconcileWake.IdleWake _ -> "IdleWake"
            | ReconcileProgram.ReconcileWake.RetryWake -> "RetryWake"
            | ReconcileProgram.ReconcileWake.FailureWake _ -> "FailureWake"
            | ReconcileProgram.ReconcileWake.AbortWake -> "AbortWake"

    let evidenceSnapshotError (reason: string) : obj =
        box
            {| kind = "SnapshotError"
               reason = reason |}

    let evidenceNoTurn () : obj = box {| kind = "NoTurn" |}

    let evidenceProvisional (outcomeName: string) : obj =
        box
            {| kind = "Provisional"
               outcome = outcomeName |}

    let evidenceUnknown () : obj = box {| kind = "Unknown" |}

    let evidenceTerminal (outcomeName: string) : obj =
        box
            {| kind = "Terminal"
               physical = ""
               outcome = outcomeName |}

    let evidenceTerminalFor (physical: string) (outcomeName: string) : obj =
        box
            {| kind = "Terminal"
               physical = physical
               outcome = outcomeName |}

    let evidenceSessionCleared () : obj = box {| kind = "SessionCleared" |}

    let decideStep (wake: obj) (rereadsRemaining: int) (evidence: obj) : obj =
        ReconcileProgram.decideStep (wakeOf wake) rereadsRemaining (evidenceOf evidence)
        |> fun decision -> decisionToJs decision rereadsRemaining

    let decisionName (decision: obj) : string = stringOf (property decision "name")

    let clearsContinuationCandidate (decision: obj) : bool =
        boolOf (property decision "clearsContinuationCandidate")

    let consumeKey (turn: obj) : string =
        ReconcileProgram.consumeKey (turnOf turn)

    let provisionalHas (maps: obj) (turn: obj) : bool =
        (mapsOf maps).provisionalHas (turnOf turn)

    let consumedHas (maps: obj) (turn: obj) : bool = (mapsOf maps).consumedHas (turnOf turn)

    let publishDecision (maps: obj) (turn: obj) : obj =
        let result = ReconcileProgram.publishDecision (mapsOf maps) (turnOf turn)

        box
            {| shouldPublish = result.shouldPublish
               maps = PublishMapsHandle(result.maps) :> obj |}

    let clearProvisional (maps: obj) (session: string) : obj =
        PublishMapsHandle(ReconcileProgram.clearProvisional (mapsOf maps) session) :> obj

    let private schedulerMessage
        (id: string)
        (role: string)
        (parentId: string option)
        (finish: string option)
        (errorName: string option)
        (completed: bool)
        (parts: MessagePart array)
        : SessionMessage =
        { Id = id
          Role = role
          Agent = None
          Finish = finish
          ErrorName = errorName
          Model = None
          ParentId = parentId
          Completed = completed
          IsCompaction = false
          PromptKey = None
          Parts = parts
          PartIds = Array.create parts.Length None
          ToolParts = [||] }

    let unboundFailureScenario () : Task<obj> =
        task {
            let sessionId = SessionId.create "unbound-failure-session"
            let store = TurnBinding.Store()
            // DSL-MUTABLE: algorithm-scratch — proves an unbound coarse failure performs zero reads.
            let mutable snapshotReads = 0

            let snapshot =
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        snapshotReads <- snapshotReads + 1
                        Task.FromResult(Ok []) }

            let scheduler =
                Reconciler.Scheduler(snapshot, store, (fun _ -> Task.FromResult(()) :> Task))

            scheduler.Signal(
                ProviderFailure
                    { SessionId = sessionId
                      Failure = ExecutionFailure.ProviderPermanent
                      Diagnostic = "APIError" }
            )

            do! scheduler.StopAndDrain()
            return box {| snapshotReads = snapshotReads |}
        }

    let private projectionEdgeScenario (failureWake: bool) : Task<obj> =
        task {
            let sessionId = SessionId.create "projection-edge-session"
            let rootPhysical = PhysicalUserMessageId.create "projection-edge-root"
            let currentPhysical = PhysicalUserMessageId.create "projection-edge-current"
            let store = TurnBinding.Store()
            store.BindUserMessage(sessionId, rootPhysical)
            store.BindContinuationUserMessage(sessionId, currentPhysical)

            // DSL-MUTABLE: algorithm-scratch — fake Host projection visibility.
            let mutable currentVisible = false
            // DSL-MUTABLE: algorithm-scratch — proves one read per causal edge.
            let mutable snapshotReads = 0
            let firstSnapshotObserved = TaskCompletionSource<unit>()
            let currentTurnObserved = TaskCompletionSource<ReconciledTurnContext>()

            let user id =
                schedulerMessage id "user" None None None false [||]

            let oldAssistant =
                schedulerMessage
                    "projection-edge-old-run"
                    "assistant"
                    (Some "projection-edge-root")
                    (Some "stop")
                    None
                    true
                    [| MessagePart.Text "old terminal" |]

            let currentAssistant =
                if failureWake then
                    schedulerMessage
                        "projection-edge-current-run"
                        "assistant"
                        (Some "projection-edge-current")
                        None
                        (Some "APIError")
                        true
                        [||]
                else
                    schedulerMessage
                        "projection-edge-current-run"
                        "assistant"
                        (Some "projection-edge-current")
                        (Some "stop")
                        None
                        true
                        [| MessagePart.Text "current terminal" |]

            let snapshot =
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        snapshotReads <- snapshotReads + 1

                        let messages =
                            [ user "projection-edge-root"; oldAssistant; user "projection-edge-current" ]
                            |> fun prefix ->
                                if currentVisible then
                                    prefix @ [ currentAssistant ]
                                else
                                    prefix

                        Task.FromResult(Ok messages) }

            let observeSnapshot (_: SessionId) (_: SessionMessage list) : Task =
                AsyncSupport.trySetResult firstSnapshotObserved () |> ignore
                Task.FromResult(()) :> Task

            let onTurn (context: ReconciledTurnContext) : Task =
                if ProviderRunIdentity.value context.Turn.ProviderRun = "projection-edge-current-run" then
                    AsyncSupport.trySetResult currentTurnObserved context |> ignore

                Task.FromResult(()) :> Task

            let scheduler =
                Reconciler.Scheduler(snapshot, store, onTurn, ?onSnapshot = Some observeSnapshot)

            if failureWake then
                scheduler.Signal(
                    ProviderFailure
                        { SessionId = sessionId
                          Failure = ExecutionFailure.ProviderTransient
                          Diagnostic = "APIError" }
                )
            else
                let quiescence = SessionQuiescenceGate()
                quiescence.BeginProviderAttempt sessionId
                scheduler.SignalIdle(sessionId, quiescence.ObserveIdle sessionId)

            do! firstSnapshotObserved.Task
            currentVisible <- true
            scheduler.NotifyProjectionChanged(sessionId, currentPhysical)

            let! observed = currentTurnObserved.Task
            do! scheduler.StopAndDrain()

            return
                box
                    {| snapshotReads = snapshotReads
                       providerRun = ProviderRunIdentity.value observed.Turn.ProviderRun
                       outcome =
                        match observed.Turn.Outcome with
                        | ReconcileProgram.TurnCompleted -> "TurnCompleted"
                        | ReconcileProgram.TurnFailed _ -> "TurnFailed"
                        | ReconcileProgram.TurnAborted _ -> "TurnAborted"
                        | ReconcileProgram.TurnInProgress -> "TurnInProgress"
                        | ReconcileProgram.TurnNeedsContinuation _ -> "TurnNeedsContinuation"
                       hasQuiescence = Option.isSome observed.Quiescence |}
        }

    let idleProjectionEdgeScenario () : Task<obj> = projectionEdgeScenario false

    let failureProjectionEdgeScenario () : Task<obj> = projectionEdgeScenario true

    let private outcomeAndReason (context: ReconciledTurnContext) =
        match context.Turn.Outcome with
        | ReconcileProgram.TurnFailed reason -> "TurnFailed", reason
        | ReconcileProgram.TurnCompleted -> "TurnCompleted", ""
        | ReconcileProgram.TurnAborted reason -> "TurnAborted", reason
        | ReconcileProgram.TurnInProgress -> "TurnInProgress", ""
        | ReconcileProgram.TurnNeedsContinuation reason -> "TurnNeedsContinuation", reason

    let private formatObserved (snapshotReads: int) (observed: ReconciledTurnContext option) : obj =
        match observed with
        | None ->
            box
                {| snapshotReads = snapshotReads
                   observed = false
                   providerRun = ""
                   outcome = ""
                   reason = ""
                   hasQuiescence = false |}
        | Some context ->
            let outcome, reason = outcomeAndReason context

            box
                {| snapshotReads = snapshotReads
                   observed = true
                   providerRun = ProviderRunIdentity.value context.Turn.ProviderRun
                   outcome = outcome
                   reason = reason
                   hasQuiescence = Option.isSome context.Quiescence |}

    let failureWitnessCurrentAssistantScenario () : Task<obj> =
        task {
            let sessionId = SessionId.create "failure-witness-session"
            let rootPhysical = PhysicalUserMessageId.create "failure-witness-root"
            let currentPhysical = PhysicalUserMessageId.create "failure-witness-current"
            let store = TurnBinding.Store()
            store.BindUserMessage(sessionId, rootPhysical)
            store.BindContinuationUserMessage(sessionId, currentPhysical)

            // DSL-MUTABLE: algorithm-scratch — test observation capture.
            let mutable snapshotReads = 0
            let mutable observed: ReconciledTurnContext option = None

            let snapshot =
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        snapshotReads <- snapshotReads + 1

                        Task.FromResult(
                            Ok
                                [ schedulerMessage "failure-witness-root" "user" None None None false [||]
                                  schedulerMessage "failure-witness-current" "user" None None None false [||]
                                  schedulerMessage
                                      "failure-witness-current-run"
                                      "assistant"
                                      (Some "failure-witness-current")
                                      None
                                      None
                                      false
                                      [||] ]
                        ) }

            let onTurn (context: ReconciledTurnContext) : Task =
                observed <- Some context
                Task.FromResult(()) :> Task

            let scheduler = Reconciler.Scheduler(snapshot, store, onTurn)

            scheduler.Signal(
                ProviderFailure
                    { SessionId = sessionId
                      Failure = ExecutionFailure.ProviderPermanent
                      Diagnostic = "Bad Request: input_invalid" }
            )

            do! scheduler.StopAndDrain()

            return formatObserved snapshotReads observed
        }
