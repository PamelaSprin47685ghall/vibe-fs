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

module private SessionAsync =

    let continueWork
        (gateway: IGateway)
        (sessionId: SessionId)
        (turnId: TurnId)
        (waiterMapRef: ref<WaiterMap>)
        (port: IPromptPort option)
        (ct: CancellationToken)
        : Task<Result<unit, SessionError>> =
        task {
            let promptKeyText =
                sprintf "continue:%s:%s" (SessionId.value sessionId) (TurnId.value turnId)

            let pKey =
                PromptKey.create sessionId turnId PromptPurpose.ContinueTodo None 1 None promptKeyText

            let promptKeyStr = PromptKey.asString pKey

            let pFact =
                Fact.Prompt(
                    Fact.PromptFact.PromptRequested
                        {| PromptKey = promptKeyStr
                           TurnId = turnId
                           Purpose = "ContinueTodo" |}
                )

            match gateway.Append (StreamId.Session sessionId) (Some turnId) pFact with
            | CommitUnknown _ ->
                return Error(SessionError.ProjectionBroken "prompt-requested write failed")
            | Committed _ ->
                let newWaiters, tcs = PromptWaiters.registerWaiter waiterMapRef.Value promptKeyStr
                waiterMapRef.Value <- newWaiters

                let! sendResult =
                    match port with
                    | Some p ->
                        task {
                            let options: PromptOptions =
                                { Model = None
                                  Agent = None
                                  Parts = [] }

                            let! outcome =
                                p.SendPrompt sessionId "Continue the current task according to the todo snapshot." options

                            match outcome with
                            | Delivered msgId ->
                                let submitted =
                                    Fact.Prompt(
                                        Fact.PromptFact.PromptSubmitted
                                            {| PromptKey = promptKeyStr
                                               MessageId = msgId |}
                                    )

                                match gateway.Append (StreamId.Session sessionId) (Some turnId) submitted with
                                | Committed _ -> return Ok()
                                | CommitUnknown _ ->
                                    return Error(SessionError.ProjectionBroken "prompt-submitted write failed")
                            | AcceptanceUnknown(reason, _) ->
                                return Error(SessionError.Protocol ("Prompt submission status unknown: " + reason))
                            | Retryable reason ->
                                return Error(SessionError.Protocol ("Prompt submission retryable error: " + reason))
                            | Fatal reason ->
                                return Error(SessionError.Protocol ("Prompt submission fatal error: " + reason))
                        }
                    | None -> Task.FromResult(Ok())

                match sendResult with
                | Error error ->
                    waiterMapRef.Value <- Map.remove promptKeyStr waiterMapRef.Value
                    return Error error
                | Ok () ->
                    let! outcome = tcs.Task
                    ct.ThrowIfCancellationRequested()

                    let tFact =
                        Fact.Prompt(
                            Fact.PromptFact.PromptTerminal
                                {| PromptKey = promptKeyStr
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

    let commitTodoFrom
        (gateway: IGateway)
        (sessionId: SessionId)
        (outcome: SendOutcome)
        (ct: CancellationToken)
        : Task<Result<unit, SessionError>> =
        task {
            match outcome with
            | Delivered _ ->
                match Map.tryFind sessionId gateway.ProjectionSet.SessionProjections with
                | Some proj ->
                    match proj.Todos with
                    | Some snap when not (List.isEmpty snap.Items) ->
                        let remaining = List.tail snap.Items
                        let fact = Fact.Todo(Fact.TodoChanged {| Snapshot = { Items = remaining } |})
                        match gateway.Append (StreamId.Session sessionId) None fact with
                        | Committed _ -> return Ok()
                        | CommitUnknown _ -> return Error(SessionError.ProjectionBroken "commitTodoFrom write failed")
                    | _ -> return Ok()
                | None -> return Ok()
            | _ -> return Ok()
        }


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
        (waiterMapRef: ref<WaiterMap>)
        (port: IPromptPort option)
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
                          ProgressStamp = proj.Version }
                    | None ->
                        { Unfinished = false
                          ProgressStamp = proj.Version }
                | None ->
                    { Unfinished = false
                      ProgressStamp = 0L }

          GetReview =
            fun () ->
                match Map.tryFind sessionId (projs ()) with
                | Some proj ->
                    let req =
                        match proj.LastReview with
                        | Some Fact.ReviewVerdict.Passed -> false
                        | _ -> true
                    { Required = req
                      Round = 0
                      MaxRound = config.MaxInvalidRetries
                      Verdict = proj.LastReview }
                | None ->
                    { Required = true
                      Round = 0
                      MaxRound = config.MaxInvalidRetries
                      Verdict = None }

          GetProgressStamp =
            fun () ->
                match Map.tryFind sessionId (projs ()) with
                | Some proj -> proj.Version
                | None -> 0L
          Config = config

          ContinueWork =
            fun () ->
                Flow.create (fun _ctx ct ->
                    SessionAsync.continueWork gateway sessionId turnId waiterMapRef port ct)

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
                    SessionAsync.commitTodoFrom gateway sessionId outcome ct)
        }
