namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type DegenerationKind =
    | TooRepetitive
    | TooRandom

/// Host boundary outcome for an abort observed by reconciliation. A guarded
/// abort has already transferred recovery ownership to LoopSensor.
[<RequireQualifiedAccess>]
type AbortCause =
    | DegenerationGuard of DegenerationKind
    | External

/// Streaming degeneration owner. It owns the whole process-local protocol:
/// detect -> arm typed anomaly -> interrupt -> consume reconciled abort -> continue.
/// Anomaly ownership is bounded to exact execution/run: an armed anomaly records the
/// exact (SessionId * ProviderRunIdentity) taken from the streaming message id at
/// interruption time. Reconciled aborts from stale runs, different messages, or
/// external causes are safely ignored/observed without consuming an anomaly from a
/// newer attempt. Deltas without a message id are observed into detector scratch
/// only and never arm an anomaly.
type LoopSensor
    (
        isOwned: SessionId -> bool,
        abortSession: SessionId -> Task<Result<unit, string>>,
        continueSession: SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>,
        emitDiagnostic: string -> (string * string) list -> unit,
        ?runOwnedWork: (unit -> Task) -> Task
    ) =

    let gate = obj ()
    /// DSL-cross-callback-proof: physical resource — bounded detector algorithm scratch per physical attempt
    // DSL-MUTABLE: resource — bounded detector state per physical attempt.
    let detectors = Dictionary<string, LoopDetector.Detector>()
    /// DSL-cross-callback-proof: physical single-flight — process-local anomaly ownership until reconcile
    // DSL-MUTABLE: single-flight — process-local anomaly ownership until reconcile.
    let armed = Dictionary<string, DegenerationKind * ProviderRunIdentity>()
    /// DSL-cross-callback-proof: physical cancellation-token — active interruption/continuation tasks
    // DSL-MUTABLE: single-flight — active interrupt/continuation task tracking by exact run
    let activeInterrupts = Dictionary<string, ProviderRunIdentity * Task * Task>()

    let keyOf (sessionId: SessionId) = SessionId.value sessionId

    let executeWork (work: unit -> Task) : Task =
        match runOwnedWork with
        | Some run -> run work
        | None -> work ()

    let kindName kind =
        match kind with
        | DegenerationKind.TooRepetitive -> "too-repetitive"
        | DegenerationKind.TooRandom -> "too-random"

    let observeDiagnostic operation fields =
        try
            emitDiagnostic operation fields
        with _ ->
            ()

    let reportPhysicalOutcome operation sessionId kind outcome =
        let baseFields =
            [ "session_id", SessionId.value sessionId
              "kind", kindName kind
              "operation", operation ]

        match outcome with
        | Ok() -> observeDiagnostic "degeneration-guard" (baseFields @ [ "result", "ok" ])
        | Error reason ->
            observeDiagnostic "degeneration-guard" (baseFields @ [ "result", "failed"; "provider_error", reason ])

    member private this.RemoveArmedIfRun(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () ->
            let key = keyOf sessionId

            match armed.TryGetValue key with
            | true, (_, storedRun) when storedRun = providerRun -> armed.Remove key |> ignore
            | _ -> ())

    member private this.RemoveActiveIfProxy(sessionId: SessionId, providerRun: ProviderRunIdentity, proxy: Task) =
        lock gate (fun () ->
            let key = keyOf sessionId

            match activeInterrupts.TryGetValue key with
            | true, (storedRun, storedTask, _) when
                storedRun = providerRun && Object.ReferenceEquals(storedTask, proxy)
                ->
                activeInterrupts.Remove key |> ignore
            | _ -> ())

    member private this.AttachExecution
        (sessionId: SessionId, providerRun: ProviderRunIdentity, proxy: Task, execution: Task)
        =
        lock gate (fun () ->
            let key = keyOf sessionId

            match activeInterrupts.TryGetValue key with
            | true, (storedRun, storedTask, _) when
                storedRun = providerRun && Object.ReferenceEquals(storedTask, proxy)
                ->
                activeInterrupts.[key] <- (storedRun, storedTask, execution)
            | _ -> ())

    member private this.FailInterrupt
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            reason: string,
            proxy: TaskCompletionSource<unit>
        ) =
        reportPhysicalOutcome "interrupt" sessionId kind (Error reason)
        this.RemoveArmedIfRun(sessionId, providerRun)
        this.RemoveActiveIfProxy(sessionId, providerRun, proxy.Task :> Task)
        proxy.SetResult(())

    member private this.ClassifyDegeneration state =
        match state with
        | LoopDetector.State.TooRepetitive -> Some DegenerationKind.TooRepetitive
        | LoopDetector.State.TooRandom -> Some DegenerationKind.TooRandom
        | LoopDetector.State.Normal -> None

    member private this.TryArmInterrupt
        (sessionId: SessionId, providerRun: ProviderRunIdentity, kind: DegenerationKind, publicTask: Task)
        : bool =
        lock gate (fun () ->
            let key = keyOf sessionId

            if armed.ContainsKey key || activeInterrupts.ContainsKey key then
                false
            else
                armed.[key] <- (kind, providerRun)
                activeInterrupts.[key] <- (providerRun, publicTask, publicTask)
                true)

    /// Narrow physical exception boundary: only the abort effect may throw.
    /// Outcome interpretation lives in SettleInterruptOutcome.
    member private this.TryAbortSession(sessionId: SessionId) : Task<Result<unit, string>> =
        task {
            try
                let! outcome = abortSession sessionId
                return outcome
            with ex ->
                return Error ex.Message
        }

    /// Exact-run outcome decision for one interrupt attempt.
    member private this.SettleInterruptOutcome
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            outcome: Result<unit, string>,
            proxy: TaskCompletionSource<unit>
        ) =
        match outcome with
        | Ok() ->
            reportPhysicalOutcome "interrupt" sessionId kind outcome
            proxy.SetResult(())
        | Error reason -> this.FailInterrupt(sessionId, providerRun, kind, reason, proxy)

    member private this.InterruptWorkerTask
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            proxy: TaskCompletionSource<unit>
        ) : Task =
        (task {
            let! outcome = this.TryAbortSession sessionId
            this.SettleInterruptOutcome(sessionId, providerRun, kind, outcome, proxy)
        }
        :> Task)

    member private this.MonitorInterruptExecution
        (
            execution: Task,
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            proxy: TaskCompletionSource<unit>
        ) : Task =
        (task {
            try
                do! execution
            with ex ->
                this.FailInterrupt(sessionId, providerRun, kind, ex.Message, proxy)
        }
        :> Task)

    member private this.StartInterruptExecution
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            proxy: TaskCompletionSource<unit>,
            publicTask: Task,
            launch: unit -> Task
        ) =
        try
            let execution = executeWork launch

            let monitored =
                this.MonitorInterruptExecution(execution, sessionId, providerRun, kind, proxy)

            this.AttachExecution(sessionId, providerRun, publicTask, monitored)
        with ex ->
            this.FailInterrupt(sessionId, providerRun, kind, ex.Message, proxy)

    member private this.LaunchArmedInterrupt
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            proxy: TaskCompletionSource<unit>,
            publicTask: Task
        ) : Task option =
        let launch () : Task =
            this.InterruptWorkerTask(sessionId, providerRun, kind, proxy)

        this.StartInterruptExecution(sessionId, providerRun, kind, proxy, publicTask, launch)
        Some publicTask

    /// Pre-registered interrupt proxy: arms the exact run and stores the public
    /// awaitable under gate before executeWork runs, so synchronous completion
    /// cannot race the registration. The worker leaves the entry+armed on success
    /// until TurnAborted consume; on error it removes both only when the exact
    /// run/proxy is still current, then completes the proxy.
    member private this.RunInterrupt
        (sessionId: SessionId, providerRun: ProviderRunIdentity, kind: DegenerationKind)
        : Task option =
        let proxy =
            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let publicTask = proxy.Task :> Task

        let armedNow = this.TryArmInterrupt(sessionId, providerRun, kind, publicTask)

        if not armedNow then
            None
        else
            this.LaunchArmedInterrupt(sessionId, providerRun, kind, proxy, publicTask)

    /// Narrow physical exception boundary: only the continue effect may throw.
    member private this.TryContinueSession
        (sessionId: SessionId, kind: DegenerationKind, directory: string option)
        : Task<Result<unit, string>> =
        task {
            try
                let! outcome = continueSession sessionId kind directory
                return outcome
            with ex ->
                return Error ex.Message
        }

    member private this.ReportContinueOutcome
        (sessionId: SessionId, kind: DegenerationKind, outcome: Result<unit, string>)
        =
        reportPhysicalOutcome "continue" sessionId kind outcome

    member private this.FinishContinue
        (sessionId: SessionId, providerRun: ProviderRunIdentity, publicTask: Task, proxy: TaskCompletionSource<unit>)
        =
        this.RemoveActiveIfProxy(sessionId, providerRun, publicTask)
        proxy.SetResult(())

    member private this.ContinueWorkerTask
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            directory: string option,
            publicTask: Task,
            proxy: TaskCompletionSource<unit>
        ) : Task =
        (task {
            try
                let! outcome = this.TryContinueSession(sessionId, kind, directory)
                this.ReportContinueOutcome(sessionId, kind, outcome)
            finally
                this.FinishContinue(sessionId, providerRun, publicTask, proxy)
        }
        :> Task)

    member private this.MonitorContinueExecution
        (
            execution: Task,
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            publicTask: Task,
            proxy: TaskCompletionSource<unit>
        ) : Task =
        (task {
            try
                do! execution
            with ex ->
                this.ReportContinueOutcome(sessionId, kind, (Error ex.Message))
                this.FinishContinue(sessionId, providerRun, publicTask, proxy)
        }
        :> Task)

    member private this.StartContinueExecution
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            directory: string option,
            proxy: TaskCompletionSource<unit>,
            publicTask: Task,
            launch: unit -> Task
        ) =
        try
            let execution = executeWork launch

            let monitored =
                this.MonitorContinueExecution(execution, sessionId, providerRun, kind, publicTask, proxy)

            this.AttachExecution(sessionId, providerRun, publicTask, monitored)
        with ex ->
            this.ReportContinueOutcome(sessionId, kind, (Error ex.Message))
            this.FinishContinue(sessionId, providerRun, publicTask, proxy)

    /// Pre-registered continuation proxy: stored under gate before executeWork
    /// runs. The worker always completes the proxy and removes the active entry
    /// only when the exact run/proxy is still current.
    member private this.StartContinueWork
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            directory: string option,
            proxy: TaskCompletionSource<unit>,
            publicTask: Task
        ) =
        let launch () : Task =
            this.ContinueWorkerTask(sessionId, providerRun, kind, directory, publicTask, proxy)

        this.StartContinueExecution(sessionId, providerRun, kind, directory, proxy, publicTask, launch)

    member private this.Interrupt
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            kind: DegenerationKind,
            evaluation: LoopDetector.Evaluation
        ) =
        match this.RunInterrupt(sessionId, providerRun, kind) with
        | None -> ()
        | Some _ ->
            observeDiagnostic
                "degeneration-guard"
                [ "session_id", SessionId.value sessionId
                  "kind", kindName kind
                  "operation", "interrupt"
                  "result", "armed"
                  "detector_step", string evaluation.Step
                  "weighted_distinct_token_count", sprintf "%.4f" evaluation.WeightedDistinctTokenCount ]

    member private this.DetectorFor(sessionId: SessionId) =
        let key = keyOf sessionId

        match detectors.TryGetValue key with
        | true, detector -> detector
        | false, _ ->
            let created = LoopDetector.create ()
            detectors.[key] <- created
            created

    member private this.Evaluate(delta: LoopEventCodec.TextDelta) : LoopDetector.Evaluation =
        lock gate (fun () ->
            let key = keyOf delta.SessionId
            let detector = this.DetectorFor delta.SessionId
            let updated, evaluation = LoopDetector.pushText detector delta.Delta
            detectors.[key] <- updated
            evaluation)

    member private this.InterruptForEvaluation(delta: LoopEventCodec.TextDelta, evaluation: LoopDetector.Evaluation) =
        // An anomaly requires its exact provider run: without a message id the
        // delta is observed into detector scratch only and never interrupts.
        match this.ClassifyDegeneration evaluation.State, delta.MessageId with
        | Some kind, Some messageId ->
            let providerRun = ProviderRunIdentity.create messageId
            this.Interrupt(delta.SessionId, providerRun, kind, evaluation)
        | _ -> ()

    member private this.ObserveEligible(delta: LoopEventCodec.TextDelta) =
        let evaluation = this.Evaluate delta
        this.InterruptForEvaluation(delta, evaluation)

    member private this.ObserveOwned(delta: LoopEventCodec.TextDelta) =
        let alreadyArmed = lock gate (fun () -> armed.ContainsKey(keyOf delta.SessionId))

        if isOwned delta.SessionId && not alreadyArmed then
            this.ObserveEligible delta

    /// Raw stream edge. Non-text/reasoning events fail closed in LoopEventCodec.
    member this.Observe(raw: obj) =
        match LoopEventCodec.tryDecodeTextDelta raw with
        | None -> ()
        | Some delta -> this.ObserveOwned delta

    /// Exact-run consumption decision: armed anomaly plus active slot jointly
    /// decide whether the reconciled run may take ownership. Tuple match keeps
    /// the whole state space in one flat decision.
    member private this.TryTakeArmedForContinue
        (sessionId: SessionId, expectedRun: ProviderRunIdentity)
        : (DegenerationKind * TaskCompletionSource<unit> * Task) option =
        lock gate (fun () ->
            let key = keyOf sessionId
            let armedFound, armedEntry = armed.TryGetValue key
            let activeFound, activeEntry = activeInterrupts.TryGetValue key

            match armedFound, armedEntry, activeFound, activeEntry with
            | true, (kind, armedRun), true, (storedRun, _, _) when armedRun = expectedRun && storedRun = expectedRun ->
                armed.Remove key |> ignore
                activeInterrupts.Remove key |> ignore

                let proxy =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                let publicTask = proxy.Task :> Task
                activeInterrupts.[key] <- (expectedRun, publicTask, publicTask)
                Some(kind, proxy, publicTask)
            | true, (kind, armedRun), _, _ when armedRun = expectedRun ->
                armed.Remove key |> ignore

                let proxy =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                let publicTask = proxy.Task :> Task
                activeInterrupts.[key] <- (expectedRun, publicTask, publicTask)
                Some(kind, proxy, publicTask)
            | _ -> None)

    /// Reconciliation boundary: atomically transfer one armed anomaly into its
    /// exactly-once continuation and return a typed cause so downstream yields.
    /// Consumption requires the exact reconciled provider run: a wrong/late run
    /// never consumes the armed anomaly and never clears newer owned work.
    member this.ConsumeAbortCause
        (sessionId: SessionId, expectedRun: ProviderRunIdentity, directory: string option)
        : AbortCause =
        let consumed = this.TryTakeArmedForContinue(sessionId, expectedRun)

        match consumed with
        | None -> AbortCause.External
        | Some(kind, proxy, publicTask) ->
            this.StartContinueWork(sessionId, expectedRun, kind, directory, proxy, publicTask)
            AbortCause.DegenerationGuard kind

    member _.DropSession(sessionId: SessionId) =
        lock gate (fun () ->
            let key = keyOf sessionId
            detectors.Remove key |> ignore
            armed.Remove key |> ignore
            activeInterrupts.Remove key |> ignore)

    /// Owned interrupt/continuation task for the exact requested run. A run
    /// mismatch yields None while the stored run's task remains owned.
    member _.ActiveInterruptTask(sessionId: SessionId, expectedRun: ProviderRunIdentity) : Task option =
        lock gate (fun () ->
            match activeInterrupts.TryGetValue(keyOf sessionId) with
            | true, (storedRun, owned, _) when storedRun = expectedRun -> Some owned
            | _ -> None)

    /// Attempt boundary resets detector scratch but deliberately preserves an
    /// armed anomaly until TurnAborted reconciliation consumes its ownership.
    member _.ResetDetector(sessionId: SessionId) =
        lock gate (fun () -> detectors.[keyOf sessionId] <- LoopDetector.create ())

module LoopSensor =

    let kindName kind =
        match kind with
        | DegenerationKind.TooRepetitive -> "TooRepetitive"
        | DegenerationKind.TooRandom -> "TooRandom"

    let continuationPath kind =
        match kind with
        | DegenerationKind.TooRepetitive -> "runtime/degeneration-too-repetitive"
        | DegenerationKind.TooRandom -> "runtime/degeneration-too-random"

    let private interruptiblePredicate
        (ownedSessions: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        : SessionId -> bool =
        fun sessionId ->
            let key = SessionId.value sessionId
            ownedSessions.Contains key && sessionParents.ContainsKey key

    let create
        (ownedSessions: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        (abortSession: SessionId -> Task<Result<unit, string>>)
        (continueSession: SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>)
        (emitDiagnostic: string -> (string * string) list -> unit)
        =
        LoopSensor(interruptiblePredicate ownedSessions sessionParents, abortSession, continueSession, emitDiagnostic)
