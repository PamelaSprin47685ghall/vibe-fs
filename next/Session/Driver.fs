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

type SessionDriver(gateway: IGateway, sessionId: SessionId, inbox: ISessionInbox) =
    let cts = new CancellationTokenSource()

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

    let dispatchEvent (eventOpt: SessionInboxEvent) : Task<bool> =
        task {
            match eventOpt with
            | SessionCommandEvent cmd ->
                dispatchCommand cmd
                return true

            | HumanMessageEvent(turnId, _text) ->
                let turnFact = Fact.Session(HumanTurnStarted {| TurnId = turnId |})
                gateway.Append (StreamId.Session sessionId) (Some turnId) turnFact |> ignore
                return true

            | AssistantTerminalEvent(_userMsgId, assistantMsgId, outcome) ->
                let pFact =
                    Fact.Prompt(
                        PromptTerminal
                            {| PromptKey = SessionId.value sessionId
                               Outcome = outcome
                               AssistantMessageId = Some assistantMsgId |}
                    )

                gateway.Append (StreamId.Session sessionId) None pFact |> ignore
                return true

            | CancelEvent _ ->
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
                with _ ->
                    keepGoing <- false
        }

    let workerTask = startWorker ()

    member _.SessionId = sessionId
    member _.Inbox = inbox
    member _.CancellationToken = cts.Token
    member _.Worker = workerTask

    member _.Cancel() =
        try
            cts.Cancel()
        with _ ->
            ()

    interface IDisposable with
        member _.Dispose() =
            try
                cts.Cancel()
            with _ ->
                ()

            try
                cts.Dispose()
            with _ ->
                ()
