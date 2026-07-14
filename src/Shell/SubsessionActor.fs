module Wanxiangshu.Shell.SubsessionActor

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Shell.PromiseQueue

// ── Deferred reply ──

type Deferred<'T> =
    { Resolve: 'T -> unit
      Reject: exn -> unit
      Promise: JS.Promise<'T> }

let createDeferred<'T> () : Deferred<'T> =
    let mutable resolveFn = fun (_: 'T) -> ()
    let mutable rejectFn = fun (_: exn) -> ()

    let p =
        Promise.create (fun resolve reject ->
            resolveFn <- resolve
            rejectFn <- reject)

    { Resolve = resolveFn
      Reject = rejectFn
      Promise = p }

// ── Host port ──

/// Host adapter port. Kernel never sees host JSON; the adapter translates
/// host facts into Commands and executes Effects.
type ISubsessionHost =
    abstract Dispatch: sessionId: SessionId * turn: TurnPlan -> JS.Promise<Result<HostStartReceipt, ErrorInput>>

    abstract Abort: sessionId: SessionId * turnId: TurnId -> JS.Promise<unit>

    abstract ReadTranscript: sessionId: SessionId -> JS.Promise<TranscriptSnapshot>

    /// Optional domain-event sink (EventLog append). Default no-op.
    abstract AppendEvents: events: SubsessionEvent list -> JS.Promise<unit>

// ── Timer handle ──

type TimerHandle = int

// ── Actor ──

/// One SubsessionActor owns one physical child session for its entire life.
/// Actor is the sole owner of run state. Host hooks / callers only Post facts.
type SubsessionActor(sessionId: SessionId, host: ISubsessionHost) as this =
    let queue = SerialQueue()
    let mutable state: SubsessionState = Available { SessionId = sessionId }
    let mutable pendingReplies: Map<RunId, Deferred<RunResult>> = Map.empty
    let mutable turnTimers: Map<TurnId, TimerHandle> = Map.empty
    let mutable abortTimers: Map<TurnId, TimerHandle> = Map.empty
    let mutable pendingReject: StartRunError option = None

    // ── Timer helpers (effect interpreter resources, not domain state) ──

    let clearTurnTimer (turnId: TurnId) =
        match Map.tryFind turnId turnTimers with
        | Some handle ->
            JS.clearTimeout handle
            turnTimers <- Map.remove turnId turnTimers
        | None -> ()

    let clearAbortTimer (turnId: TurnId) =
        match Map.tryFind turnId abortTimers with
        | Some handle ->
            JS.clearTimeout handle
            abortTimers <- Map.remove turnId abortTimers
        | None -> ()

    let armTurnTimer (turnId: TurnId) =
        clearTurnTimer turnId
        // 5-minute turn deadline
        let handle =
            JS.setTimeout (fun () -> this.Post(TurnDeadlineExpired turnId) |> ignore) 300_000

        turnTimers <- Map.add turnId handle turnTimers

    let armAbortTimer (turnId: TurnId) =
        clearAbortTimer turnId
        // 1-minute abort deadline
        let handle =
            JS.setTimeout (fun () -> this.Post(AbortDeadlineExpired turnId) |> ignore) 60_000

        abortTimers <- Map.add turnId handle abortTimers

    // ── Effect interpreter ──

    let executeEffect (effect: Effect) : JS.Promise<unit> =
        promise {
            match effect with
            | AppendDomainEvents events -> do! host.AppendEvents events

            | DispatchPrompt plan ->
                let! result = host.Dispatch(sessionId, plan)

                match result with
                | Ok receipt -> do! this.Post(DispatchAccepted(plan.TurnId, receipt))
                | Error err -> do! this.Post(DispatchRejected(plan.TurnId, err))

            | ReadTranscript sid ->
                let! snap = host.ReadTranscript sid
                do! this.Post(TranscriptLoaded snap)

            | AbortHostSession(sid, tid) -> do! host.Abort(sid, tid)

            | ArmTurnDeadline turnId -> armTurnTimer turnId

            | CancelTurnDeadline turnId -> clearTurnTimer turnId

            | ArmAbortDeadline turnId -> armAbortTimer turnId

            | CancelAbortDeadline turnId -> clearAbortTimer turnId

            | CompleteCaller(runId, result) ->
                match Map.tryFind runId pendingReplies with
                | Some reply ->
                    pendingReplies <- Map.remove runId pendingReplies
                    reply.Resolve result
                | None -> ()

            | RejectStart err ->
                // Consumed by StartRun caller via pendingReject handshake.
                pendingReject <- Some err
        }

    // ── Core step: pure decide → update state → run effects ──

    let handleCommand (cmd: Command) : JS.Promise<unit> =
        promise {
            match decide state cmd with
            | Ok(Decided decision) ->
                // Append domain events first (if any)
                if not (List.isEmpty decision.Events) then
                    do! host.AppendEvents decision.Events

                // Apply new state
                state <- decision.NextState

                // Arm deadlines before Dispatch/Abort so a hung host call cannot
                // prevent the watchdog from starting.
                let ordered =
                    decision.Effects
                    |> List.sortBy (function
                        | ArmTurnDeadline _
                        | ArmAbortDeadline _
                        | CancelTurnDeadline _
                        | CancelAbortDeadline _ -> 0
                        | AppendDomainEvents _ -> 1
                        | _ -> 2)

                for effect in ordered do
                    try
                        do! executeEffect effect
                    with ex ->
                        // Infrastructure failure during effect execution:
                        // poison the session and complete any pending caller.
                        state <- Poisoned(HostProtocolBroken ex.Message)

                        for KeyValue(runId, reply) in pendingReplies do
                            reply.Resolve(Failed(InfrastructureFailure("effect failed: " + ex.Message)))

                        pendingReplies <- Map.empty

            | Ok(NoChange _reason) ->
                // Named ignore — log-only in production; no state change.
                ()

            | Error(IllegalTransition(s, c)) ->
                // Loud failure: poison + complete pending callers.
                state <- Poisoned(HostProtocolBroken("illegal: " + s + " + " + c))

                for KeyValue(runId, reply) in pendingReplies do
                    reply.Resolve(Failed(InfrastructureFailure("illegal transition: " + s + " + " + c)))

                pendingReplies <- Map.empty

            | Error(StaleTurnCommand(expected, actual)) ->
                // Stale timer/marker — ignore after logging.
                let _ = TurnId.value expected, TurnId.value actual
                ()
        }

    // ── Public API ──

    member _.SessionId = sessionId

    member _.GetState() = state

    member _.IsPoisoned =
        match state with
        | Poisoned _ -> true
        | _ -> false

    /// Enqueue a typed Command. All external facts enter here.
    member _.Post(cmd: Command) : JS.Promise<unit> =
        queue.Enqueue(fun () -> handleCommand cmd)

    /// Start a run. Returns a Promise that resolves when the actor completes
    /// the run (or immediately rejects if the session is not Available).
    member this.StartRun(request: StartRunRequest) : JS.Promise<RunResult> =
        promise {
            let deferred = createDeferred<RunResult> ()

            // Enqueue the StartRun command and wait for either:
            //   - RejectStart (immediate reject via pendingReject)
            //   - CompleteCaller (normal completion via pendingReplies)
            do!
                queue.Enqueue(fun () ->
                    promise {
                        pendingReject <- None
                        pendingReplies <- Map.add request.RunId deferred pendingReplies
                        do! handleCommand (StartRun request)

                        match pendingReject with
                        | Some err ->
                            pendingReplies <- Map.remove request.RunId pendingReplies
                            pendingReject <- None

                            let result =
                                match err with
                                | AlreadyRunning -> Failed(ProtocolViolation "subagent session already running")
                                | StartRunError.SessionPoisoned reason ->
                                    Failed(InfrastructureFailure("session poisoned: " + string reason))
                                | NoModelAvailable -> Failed NoModelConfigured

                            deferred.Resolve result
                        | None -> ()
                    })

            return! deferred.Promise
        }
