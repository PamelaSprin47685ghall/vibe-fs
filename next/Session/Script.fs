namespace Wanxiangshu.Next.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

type TodoView =
    { Unfinished: bool
      ProgressStamp: int64 }

type ReviewView =
    { Required: bool
      Round: int
      MaxRound: int
      Verdict: Fact.ReviewVerdict option }

type TerminalSignal =
    { mutable CurrentWaiter: JsTcs<Fact.PromptOutcome> option }

module TerminalSignal =

    let create () : TerminalSignal = { CurrentWaiter = None }

    let createWaiter (signal: TerminalSignal) (_ct: CancellationToken) : Task<Fact.PromptOutcome> =
        let tcs = JsTcs<Fact.PromptOutcome>()
        let _ = lock signal (fun () -> signal.CurrentWaiter <- Some tcs)
        tcs.Task

    let signalTerminal (signal: TerminalSignal) (outcome: Fact.PromptOutcome) : bool =
        let waiter =
            lock signal (fun () ->
                match signal.CurrentWaiter with
                | Some w ->
                    signal.CurrentWaiter <- None
                    Some w
                | None -> None)

        match waiter with
        | Some tcs ->
            let _ = tcs.TrySetResult outcome
            true
        | None -> false

    let cancelWait (signal: TerminalSignal) : unit =
        let waiter =
            lock signal (fun () ->
                match signal.CurrentWaiter with
                | Some w ->
                    signal.CurrentWaiter <- None
                    Some w
                | None -> None)

        match waiter with
        | Some tcs -> let _ = tcs.TrySetResult(Fact.FatalFailure "cancelled") in ()
        | None -> ()

// ── Standalone async helpers with explicit Task<Result<…, SessionError>> return type ──
// These live outside the SessionScript record so Fable's task { } CE can resolve
// its TaskCode type params via the function return-type annotation.

module private SessionAsync =

    let continueWork
        (gateway: IGateway)
        (sessionId: SessionId)
        (turnId: TurnId)
        (signal: TerminalSignal)
        (ct: CancellationToken)
        : Task<Result<unit, SessionError>> =
        task {
            let promptKeyText =
                sprintf "continue:%s:%s" (SessionId.value sessionId) (TurnId.value turnId)

            let waiterTask = TerminalSignal.createWaiter signal ct

            let pFact =
                Fact.Prompt(
                    Fact.PromptFact.PromptRequested
                        {| PromptKey = promptKeyText
                           TurnId = turnId
                           Purpose = "ContinueTodo" |}
                )

            let res =
                match gateway.Append (StreamId.Session sessionId) (Some turnId) pFact with
                | CommitUnknown _ -> Error(SessionError.ProjectionBroken "prompt-requested write failed")
                | Committed _ -> Ok()
            match res with
            | Error e -> return Error e
            | Ok () ->
                let! outcome = waiterTask
                ct.ThrowIfCancellationRequested()

                let tFact =
                    Fact.Prompt(
                        Fact.PromptFact.PromptTerminal
                            {| PromptKey = promptKeyText
                               Outcome = outcome
                               AssistantMessageId = None |}
                    )

                match gateway.Append (StreamId.Session sessionId) None tFact with
                | CommitUnknown _ -> return Error(SessionError.Protocol "prompt-terminal write failed")
                | Committed _ -> return Ok()
        }

    let requestReview
        (gateway: IGateway)
        (sessionId: SessionId)
        (ct: CancellationToken)
        : Task<Result<unit, SessionError>> =
        task {
            let fact =
                Fact.Review(
                    Fact.ReviewFact.ReviewApplied
                        {| Verdict = Fact.ReviewVerdict.Passed
                           Round = 1
                           ResultingTodo = None |}
                )

            match gateway.Append (StreamId.Session sessionId) None fact with
            | CommitUnknown _ -> return Error(SessionError.ProjectionBroken "review write failed")
            | Committed _ -> return Ok()
        }

    let finish
        (gateway: IGateway)
        (sessionId: SessionId)
        (ct: CancellationToken)
        : Task<Result<SessionOutcome, SessionError>> =
        task {
            let fact =
                Fact.Session(
                    Fact.SessionFact.SessionSettled
                        {| Result = Fact.SessionResult.Completed "flow-completed" |}
                )

            match gateway.Append (StreamId.Session sessionId) None fact with
            | CommitUnknown _ -> return Error(SessionError.ProjectionBroken "settled write failed")
            | Committed _ -> return Ok(SessionOutcome.CompletedSession "flow-completed")
        }

    let commitTodoFrom (_outcome: SendOutcome) (ct: CancellationToken) : Task<Result<unit, SessionError>> =
        task { return Ok() }


// ── Domain types ──

type SessionScript =
    { GetTodo: unit -> TodoView
      GetReview: unit -> ReviewView
      GetProgressStamp: unit -> int64
      Config: SessionScriptConfig
      ContinueWork: unit -> SessionFlow<unit>
      RequestReview: unit -> SessionFlow<unit>
      Finish: unit -> SessionFlow<SessionOutcome>
      CommitTodoFrom: SendOutcome -> SessionFlow<unit> }

and SessionScriptConfig =
    { FallbackModels: string list
      MaxRetriesPerModel: int
      MaxInvalidRetries: int }

and SessionFlow<'a> = Flow<SessionScript, SessionError, 'a>

module SessionScript =

    let create
        (gateway: IGateway)
        (sessionId: SessionId)
        (_inbox: ISessionInbox)
        (signal: TerminalSignal)
        (turnId: TurnId)
        (config: SessionScriptConfig)
        : SessionScript =

        let projs () = gateway.ProjectionSet.SessionProjections

        { GetTodo =
            fun () ->
                match Map.tryFind sessionId (projs ()) with
                | Some proj ->
                    match proj.Todos with
                    | Some snap ->
                        { Unfinished = not (List.isEmpty snap.Items)
                          ProgressStamp = DateTimeOffset.UtcNow.Ticks }
                    | None ->
                        { Unfinished = false
                          ProgressStamp = 0L }
                | None ->
                    { Unfinished = false
                      ProgressStamp = 0L }

          GetReview =
            fun () ->
                match Map.tryFind sessionId (projs ()) with
                | Some proj ->
                    { Required = false
                      Round = 0
                      MaxRound = config.MaxInvalidRetries
                      Verdict = proj.LastReview }
                | None ->
                    { Required = false
                      Round = 0
                      MaxRound = config.MaxInvalidRetries
                      Verdict = None }

          GetProgressStamp = fun () -> DateTimeOffset.UtcNow.Ticks
          Config = config

          ContinueWork =
            fun () ->
                Flow.create (fun _ctx ct ->
                    SessionAsync.continueWork gateway sessionId turnId signal ct)

          RequestReview =
            fun () ->
                Flow.create (fun _ctx ct ->
                    SessionAsync.requestReview gateway sessionId ct)

          Finish =
            fun () ->
                Flow.create (fun _ctx ct ->
                    SessionAsync.finish gateway sessionId ct)

          CommitTodoFrom =
            fun outcome ->
                Flow.create (fun _ctx ct ->
                    SessionAsync.commitTodoFrom outcome ct)
        }

module SessionFlows =

    let sessionProgress: ProgressGuard<SessionScript, SessionError> =
        { Stamp = fun s -> s.GetProgressStamp()
          NoProgress = fun msg -> SessionError.NoProgress msg }

    let session = FlowBuilder<SessionScript, SessionError>(Some sessionProgress)

    let finishTodo (s: SessionScript) : SessionFlow<unit> =
        let mutable flow = Flow.create (fun _ _ -> Task.FromResult(Ok()))
        let scriptFlow =
            session {
                while s.GetTodo().Unfinished do
                    do! s.ContinueWork()
            }
        scriptFlow

    let passReview (s: SessionScript) : SessionFlow<unit> =
        session {
            let! () = finishTodo s

            while s.GetReview().Required do
                do! s.RequestReview()
                do! finishTodo s
        }

    let run (s: SessionScript) : SessionFlow<SessionOutcome> =
        session {
            do! passReview s
            let! outcome = s.Finish()
            return outcome
        }

    let runFlow
        (s: SessionScript)
        (ct: CancellationToken)
        (flow: SessionFlow<'a>)
        : Task<Result<'a, SessionError>> =
        task {
            try
                return! Flow.run s ct flow
            with ex ->
                let msg = if isNull (box ex) then "" else string ex

                if msg.Contains("cancel") || msg.Contains("Cancel") || msg.Contains("Operation") then
                    return Error SessionError.SessionCancelled
                else
                    return Error(SessionError.Protocol msg)
        }
