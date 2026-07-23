namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

type DriverSlot =
    | Idle
    | Running of cancellationSource: CancellationTokenSource

type SessionDriversKey =
    { RuntimeId: RuntimeId
      SessionId: SessionId }

type SessionDrivers() =
    let drivers = Dictionary<SessionDriversKey, DriverSlot>()
    let localEpochs = Dictionary<SessionDriversKey, LocalEpoch>()
    let lockObj = obj ()

    member _.GetLocalEpoch(key: SessionDriversKey) : LocalEpoch =
        lock lockObj (fun () ->
            match localEpochs.TryGetValue(key) with
            | true, v -> v
            | false, _ ->
                localEpochs.[key] <- 0L
                0L)

    member _.BumpLocalEpochOnHuman(key: SessionDriversKey) : LocalEpoch =
        lock lockObj (fun () ->
            let current =
                match localEpochs.TryGetValue(key) with
                | true, v -> v
                | false, _ -> 0L

            let next = current + 1L
            localEpochs.[key] <- next
            next)

    member _.Activate(key: SessionDriversKey, cts: CancellationTokenSource) : bool =
        lock lockObj (fun () ->
            match drivers.TryGetValue(key) with
            | true, Running _ -> false
            | true, Idle
            | false, _ ->
                drivers.[key] <- Running cts
                true)

    member _.Cancel(key: SessionDriversKey) : unit =
        let ctsOpt =
            lock lockObj (fun () ->
                match drivers.TryGetValue(key) with
                | true, Running cts ->
                    drivers.[key] <- Idle
                    Some cts
                | _ -> None)

        match ctsOpt with
        | Some cts ->
            try
                cts.Cancel()
            with _ ->
                ()

            try
                cts.Dispose()
            with _ ->
                ()
        | None -> ()

    member _.Deactivate(key: SessionDriversKey) : unit =
        let ctsOpt =
            lock lockObj (fun () ->
                match drivers.TryGetValue(key) with
                | true, Running cts ->
                    drivers.Remove(key) |> ignore
                    Some cts
                | true, Idle ->
                    drivers.Remove(key) |> ignore
                    None
                | false, _ -> None)

        match ctsOpt with
        | Some cts ->
            try
                cts.Cancel()
            with _ ->
                ()

            try
                cts.Dispose()
            with _ ->
                ()
        | None -> ()

/// A SessionDriver runs the event-processing loop AND launches
/// SessionFlows.run as a separate task when a HumanTurn arrives.
type SessionDriver(gateway: IGateway, sessionId: SessionId, inbox: ISessionInbox, ?port: IPromptPort) =
    let cts = new CancellationTokenSource()
    let waiterMapRef = ref PromptWaiters.emptyWaiters
    let mutable flowTask: Task<Result<SessionOutcome, SessionError>> option = None
    let mutable flowRunning = false

    let signalWaiters outcome =
        let waiters = waiterMapRef.Value
        waiterMapRef.Value <-
            Map.fold (fun remaining key _ -> PromptWaiters.trySignalWaiter remaining key outcome) waiters waiters

    let defaultConfig: SessionScriptConfig =
        { FallbackModels = [ "default" ]
          MaxRetriesPerModel = 3
          MaxInvalidRetries = 3 }

    let dispatchCommand (cmd: SessionCommand) =
        match cmd with
        | UpsertTodo(snap, reply) ->
            let fact = Fact.Todo(TodoChanged {| Snapshot = snap |})
            let commitRes = gateway.Append (StreamId.Session sessionId) None fact

            match commitRes with
            | Committed _ -> reply (Ok SessionCommandResult.Upserted)
            | _ -> reply (Error SessionCommandError.InboxFull)
        | QuerySnapshot reply ->
            match Map.tryFind sessionId gateway.ProjectionSet.SessionProjections with
            | Some proj ->
                let todoSnap = defaultArg proj.Todos { Items = [] }
                reply todoSnap
            | None -> reply { Items = [] }
        | SubmitReview(reportText, reply) ->
            let fact =
                Fact.Review(
                    ReviewApplied
                        {| Verdict = ReviewVerdict.NeedsChanges [ reportText ]
                           Round = 1
                           ResultingTodo = None |}
                )

            let commitRes = gateway.Append (StreamId.Session sessionId) None fact

            match commitRes with
            | Committed _ -> reply (Ok SessionCommandResult.ReviewSubmitted)
            | _ -> reply (Error SessionCommandError.InboxFull)
        | ReturnVerdict(verdictText, reply) ->
            let verdict =
                if verdictText.Equals("Passed", StringComparison.OrdinalIgnoreCase) then
                    ReviewVerdict.Passed
                else
                    ReviewVerdict.NeedsChanges [ verdictText ]

            let fact =
                Fact.Review(
                    ReviewApplied
                        {| Verdict = verdict
                           Round = 1
                           ResultingTodo = None |}
                )

            let commitRes = gateway.Append (StreamId.Session sessionId) None fact

            match commitRes with
            | Committed _ -> reply (Ok SessionCommandResult.VerdictReturned)
            | _ -> reply (Error SessionCommandError.InboxFull)

    let startFlow (turnId: TurnId) : bool =
        if flowRunning then
            false
        else
            flowRunning <- true
            let script = SessionScript.create gateway sessionId inbox waiterMapRef port turnId defaultConfig

            let task =
                task {
                    try
                        return! SessionFlows.runFlow script cts.Token (SessionFlows.run script)
                    finally
                        flowRunning <- false
                }

            flowTask <- Some task
            true

    let dispatchEvent (eventOpt: SessionInboxEvent) : Task<bool> =
        task {
            match eventOpt with
            | SessionCommandEvent cmd ->
                dispatchCommand cmd
                return true

            | HumanMessageEvent(turnId, _text) ->
                let fact = Fact.Session(HumanTurnStarted {| TurnId = turnId |})

                match gateway.Append (StreamId.Session sessionId) (Some turnId) fact with
                | Committed _ ->
                    startFlow turnId |> ignore
                    return true
                | CommitUnknown _ -> return false

            | AssistantTerminalEvent(_userMsgId, _assistantMsgId, outcome) ->
                let pFact =
                    Fact.Prompt(
                        PromptTerminal
                            {| PromptKey = sprintf "terminal:%s" (MessageId.value _userMsgId)
                               Outcome = outcome
                               AssistantMessageId = Some _assistantMsgId |}
                    )

                gateway.Append (StreamId.Session sessionId) None pFact |> ignore
                signalWaiters outcome
                return true

            | CancelEvent _ ->
                let pFact =
                    Fact.Prompt(
                        PromptTerminal
                            {| PromptKey = sprintf "terminal:cancel"
                               Outcome = Fact.PromptOutcome.FatalFailure "cancelled"
                               AssistantMessageId = None |}
                    )
                gateway.Append (StreamId.Session sessionId) None pFact |> ignore
                signalWaiters (Fact.PromptOutcome.FatalFailure "cancelled")

                try
                    cts.Cancel()
                with _ ->
                    ()

                return false

            | PluginEvent _
            | ToolAfterEvent _
            | LifecycleEvent _
            | LoopCommandEvent _
            | SquadCommandEvent _ -> return true
        }

    let startWorker () : Task<unit> =
        task {
            let mutable keepGoing = true

            while keepGoing && not cts.IsCancellationRequested do
                try
                    let! evt = inbox.Receive cts.Token
                    let! continueLoop = dispatchEvent evt
                    keepGoing <- continueLoop
                with
                | :? OperationCanceledException -> keepGoing <- false
                | _ -> keepGoing <- false
        }

    let workerTask = startWorker ()

    member _.SessionId = sessionId
    member _.Inbox = inbox
    member _.CancellationToken = cts.Token
    member _.Worker = workerTask

    member _.Cancel() =
        signalWaiters (Fact.PromptOutcome.FatalFailure "cancelled")

        try
            cts.Cancel()
        with _ ->
            ()

    interface IDisposable with
        member this.Dispose() =
            this.Cancel()

            try
                cts.Dispose()
            with _ ->
                ()
