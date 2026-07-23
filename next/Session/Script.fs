namespace Wanxiangshu.Next.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

module private SessionAsync =

    let mutable private globalHistoricalIndex = PromptProtocol.emptyHistoricalIndex
    let mutable private globalLocalProtocol = PromptProtocol.emptyLocalProtocol

    let continueWork
        (gateway: IGateway)
        (sessionId: SessionId)
        (turnId: TurnId)
        (waiterMapRef: ref<WaiterMap>)
        (pendingMapRef: ref<Map<string, string>>)
        (port: IPromptPort option)
        (ct: CancellationToken)
        : Task<Result<unit, SessionError>> =
        task {
            let attempt =
                match Map.tryFind sessionId gateway.ProjectionSet.SessionProjections with
                | Some proj -> (int proj.Version) + 1
                | None -> 1

            let promptKeyText =
                sprintf "continue:%s:%s:%d" (SessionId.value sessionId) (TurnId.value turnId) attempt

            let pKey =
                PromptKey.create sessionId turnId PromptPurpose.ContinueTodo None attempt None promptKeyText

            let promptKeyStr = PromptKey.asString pKey

            let decision = PromptProtocol.evaluateSendOnce globalHistoricalIndex globalLocalProtocol pKey

            match decision with
            | HistoricalHit history ->
                match history.Outcome with
                | Some (Fact.PromptOutcome.Delivered _) -> return Ok()
                | Some (Fact.PromptOutcome.AcceptanceUnknown (reason, _)) ->
                    return Error(SessionError.Protocol ("Historical status unknown: " + reason))
                | Some (Fact.PromptOutcome.RetryableFailure msg)
                | Some (Fact.PromptOutcome.FatalFailure msg) ->
                    return Error(SessionError.Protocol ("Historical failure: " + msg))
                | None -> return Ok()
            | LocalPending _ ->
                return Ok()
            | Uncertain reason ->
                return Error(SessionError.Protocol ("Prompt state uncertain: " + reason))
            | SendNew ->
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
                    match port with
                    | None -> return Ok()
                    | Some p ->
                        let newWaiters, tcs = PromptWaiters.registerWaiter waiterMapRef.Value promptKeyStr
                        waiterMapRef.Value <- newWaiters

                        let options: PromptOptions =
                            { Model = None
                              Agent = None
                              Parts = [] }

                        let! outcome = p.SendPrompt sessionId "Continue the current task according to the todo snapshot." options

                        match outcome with
                        | Delivered msgId ->
                            let msgIdStr = MessageId.value msgId
                            pendingMapRef.Value <- Map.add msgIdStr promptKeyStr pendingMapRef.Value

                            let now = DateTimeOffset.UtcNow
                            let dispatchId = DispatchId.create (Guid.NewGuid().ToString("N"))
                            globalLocalProtocol <- PromptProtocol.recordSubmitted globalLocalProtocol pKey dispatchId msgId now

                            let submitted =
                                Fact.Prompt(
                                    Fact.PromptFact.PromptSubmitted
                                        {| PromptKey = promptKeyStr
                                           MessageId = msgId |}
                                )

                            match gateway.Append (StreamId.Session sessionId) (Some turnId) submitted with
                            | CommitUnknown _ ->
                                waiterMapRef.Value <- Map.remove promptKeyStr waiterMapRef.Value
                                return Error(SessionError.ProjectionBroken "prompt-submitted write failed")
                            | Committed _ ->
                                let! outcome = tcs.Task
                                ct.ThrowIfCancellationRequested()

                                let now = DateTimeOffset.UtcNow
                                let (newHist, newLocal) = PromptProtocol.recordTerminal globalHistoricalIndex globalLocalProtocol pKey None None outcome now
                                globalHistoricalIndex <- newHist
                                globalLocalProtocol <- newLocal

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

                        | AcceptanceUnknown(reason, _) ->
                            waiterMapRef.Value <- Map.remove promptKeyStr waiterMapRef.Value
                            return Error(SessionError.Protocol ("Prompt submission status unknown: " + reason))
                        | Retryable reason ->
                            waiterMapRef.Value <- Map.remove promptKeyStr waiterMapRef.Value
                            return Error(SessionError.Protocol ("Prompt submission retryable error: " + reason))
                        | Fatal reason ->
                            waiterMapRef.Value <- Map.remove promptKeyStr waiterMapRef.Value
                            return Error(SessionError.Protocol ("Prompt submission fatal error: " + reason))
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
            return Ok()
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
        (pendingMapRef: ref<Map<string, string>>)
        : SessionScript =

        { GetTodo = SessionScriptViews.getTodo gateway sessionId
          GetReview = SessionScriptViews.getReview gateway sessionId config
          GetProgressStamp = SessionScriptViews.getProgressStamp gateway sessionId
          Config = config

          ContinueWork =
            fun () ->
                Flow.create (fun _ctx ct ->
                    SessionAsync.continueWork gateway sessionId turnId waiterMapRef pendingMapRef port ct)

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
