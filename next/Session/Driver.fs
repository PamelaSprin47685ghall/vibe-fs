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
    let pendingUserMsgToKeyRef = ref (Map.empty<string, string>)
    let mutable flowTask: Task<Result<SessionOutcome, SessionError>> option = None
    let mutable flowCts: CancellationTokenSource option = None
    let mutable currentTurnId: TurnId option = None
    let mutable awaitingNativeTerminal: bool = false
    let mutable localHistoricalIndex = PromptProtocol.emptyHistoricalIndex
    let mutable localPromptProtocol = PromptProtocol.emptyLocalProtocol

    let signalWaiterByKey keyString outcome =
        let waiters = waiterMapRef.Value
        waiterMapRef.Value <- PromptWaiters.trySignalWaiter waiters keyString outcome

    let defaultConfig: SessionScriptConfig =
        { FallbackModels = [ "default" ]
          MaxRetriesPerModel = 3
          MaxInvalidRetries = 3 }

    let dispatchCommand cmd =
        DriverDispatch.dispatchCommand gateway sessionId cmd

    let cancelCurrentFlow () =
        match flowCts with
        | Some c ->
            try
                c.Cancel()
            with _ ->
                ()

            try
                c.Dispose()
            with _ ->
                ()

            flowCts <- None
        | None -> ()

    let startFlow (turnId: TurnId) : bool =
        cancelCurrentFlow ()
        let newFlowCts = new CancellationTokenSource()
        flowCts <- Some newFlowCts
        currentTurnId <- Some turnId

        let script =
            SessionScript.create gateway sessionId inbox waiterMapRef port turnId defaultConfig pendingUserMsgToKeyRef

        let task =
            task {
                try
                    return! SessionFlows.runFlow script newFlowCts.Token (SessionFlows.run script)
                finally
                    if flowCts = Some newFlowCts then
                        flowCts <- None
            }

        flowTask <- Some task
        true

    let dispatchEvent (eventOpt: SessionInboxEvent) : Task<bool> =
        task {
            match eventOpt with
            | SessionCommandEvent cmd ->
                dispatchCommand cmd
                return true

            | ToolAfterEvent(toolName, _callId, argsJson, _outJson) -> return true

            | HumanMessageEvent(turnId, _text) ->
                let fact = Fact.Session(HumanTurnStarted {| TurnId = turnId |})

                match gateway.Append (StreamId.Session sessionId) (Some turnId) fact with
                | Committed _ ->
                    currentTurnId <- Some turnId
                    awaitingNativeTerminal <- true
                    cancelCurrentFlow ()
                    return true
                | CommitUnknown _ -> return false

            | AssistantTerminalEvent(userMsgId, assistantMsgId, outcome) ->
                let userMsgStr = MessageId.value userMsgId

                match Map.tryFind userMsgStr pendingUserMsgToKeyRef.Value with
                | Some promptKeyStr ->
                    pendingUserMsgToKeyRef.Value <- Map.remove userMsgStr pendingUserMsgToKeyRef.Value

                    let pFact =
                        Fact.Prompt(
                            PromptTerminal
                                {| PromptKey = promptKeyStr
                                   Outcome = outcome
                                   AssistantMessageId = Some assistantMsgId |}
                        )

                    gateway.Append (StreamId.Session sessionId) None pFact |> ignore

                    let now = DateTimeOffset.UtcNow
                    let pkOpt = PromptKey.parse promptKeyStr

                    let pKey =
                        defaultArg
                            pkOpt
                            (PromptKey.create
                                sessionId
                                (defaultArg currentTurnId (TurnId.create "unknown"))
                                PromptPurpose.ContinueTodo
                                None
                                1
                                (Some userMsgId)
                                promptKeyStr)

                    let (newHist, newLocal) =
                        PromptProtocol.recordTerminal
                            localHistoricalIndex
                            localPromptProtocol
                            pKey
                            (Some userMsgId)
                            (Some assistantMsgId)
                            outcome
                            now

                    localHistoricalIndex <- newHist
                    localPromptProtocol <- newLocal

                    signalWaiterByKey promptKeyStr outcome
                | None ->
                    if awaitingNativeTerminal || flowCts.IsNone then
                        awaitingNativeTerminal <- false

                        if currentTurnId.IsSome then
                            startFlow currentTurnId.Value |> ignore

                return true

            | CancelEvent _ ->
                cancelCurrentFlow ()

                let pFact =
                    Fact.Prompt(
                        PromptTerminal
                            {| PromptKey = "terminal:cancel"
                               Outcome = Fact.PromptOutcome.FatalFailure "cancelled"
                               AssistantMessageId = None |}
                    )

                gateway.Append (StreamId.Session sessionId) None pFact |> ignore
                signalWaiterByKey "terminal:cancel" (Fact.PromptOutcome.FatalFailure "cancelled")

                try
                    cts.Cancel()
                with _ ->
                    ()

                return false

            | PluginEvent _
            | LifecycleEvent _
            | LoopCommandEvent _ -> return true
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
        signalWaiterByKey "terminal:cancel" (Fact.PromptOutcome.FatalFailure "cancelled")

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
