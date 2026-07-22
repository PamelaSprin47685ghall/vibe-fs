namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

type PromptHistoryRecord =
    { PromptKey: string
      UserMessageId: MessageId option
      AssistantMessageId: MessageId option
      Outcome: PromptOutcome option
      CompletedAt: DateTimeOffset option }

type SessionProjection =
    { Todos: TodoSnapshot option
      LastReview: ReviewVerdict option
      SettledResult: SessionResult option
      HumanTurnId: TurnId option
      Children: Map<ChildId, ChildResult>
      Processes: Map<ProcessId, ProcessResult>
      SquadTasks: Map<string, SquadTaskResult> }

type ProjectionSet =
    { Todos: TodoSnapshot option
      LastReview: ReviewVerdict option
      SessionProjections: Map<SessionId, SessionProjection>
      HistoricalPrompts: Map<string, PromptHistoryRecord>
      RuntimeId: RuntimeId option }

type RuntimeSnapshot =
    { Frontier: Frontier
      Projections: ProjectionSet
      OwnRuntimeId: RuntimeId option
      OwnLocalSeq: int64 }

module Fold =

    let emptySessionProjection: SessionProjection =
        { Todos = None
          LastReview = None
          SettledResult = None
          HumanTurnId = None
          Children = Map.empty
          Processes = Map.empty
          SquadTasks = Map.empty }

    let empty: ProjectionSet =
        { Todos = None
          LastReview = None
          SessionProjections = Map.empty
          HistoricalPrompts = Map.empty
          RuntimeId = None }

    let private updatePromptRecord key updateFn map =
        let existing =
            match Map.tryFind key map with
            | Some r -> r
            | None ->
                { PromptKey = key
                  UserMessageId = None
                  AssistantMessageId = None
                  Outcome = None
                  CompletedAt = None }

        Map.add key (updateFn existing) map

    let private updateSessionProjection sessionId updateFn (map: Map<SessionId, SessionProjection>) =
        let existing =
            match Map.tryFind sessionId map with
            | Some s -> s
            | None -> emptySessionProjection

        Map.add sessionId (updateFn existing) map

    let private foldPrompt (proj: ProjectionSet) (env: Envelope) : ProjectionSet =
        match env.Fact with
        | Prompt(PromptRequested p) ->
            let prompts = updatePromptRecord p.PromptKey id proj.HistoricalPrompts

            { proj with
                HistoricalPrompts = prompts }
        | Prompt(PromptSubmitted p) ->
            let prompts =
                updatePromptRecord
                    p.PromptKey
                    (fun r ->
                        { r with
                            UserMessageId = Some p.MessageId })
                    proj.HistoricalPrompts

            { proj with
                HistoricalPrompts = prompts }
        | Prompt(PromptTerminal p) ->
            let prompts =
                updatePromptRecord
                    p.PromptKey
                    (fun r ->
                        { r with
                            Outcome = Some p.Outcome
                            AssistantMessageId =
                                match p.AssistantMessageId with
                                | Some _ as a -> a
                                | None -> r.AssistantMessageId
                            CompletedAt = Some env.ObservedAt })
                    proj.HistoricalPrompts

            { proj with
                HistoricalPrompts = prompts }
        | _ -> proj

    let private foldTodo (proj: ProjectionSet) (env: Envelope) (t: {| Snapshot: TodoSnapshot |}) : ProjectionSet =
        match env.Stream with
        | StreamId.Workspace -> { proj with Todos = Some t.Snapshot }
        | StreamId.Session sessionId ->
            let sessionProjs =
                updateSessionProjection sessionId (fun s -> { s with Todos = Some t.Snapshot }) proj.SessionProjections

            { proj with
                SessionProjections = sessionProjs }
        | _ -> proj

    let private foldReview
        (proj: ProjectionSet)
        (env: Envelope)
        (r:
            {| Verdict: ReviewVerdict
               Round: int
               ResultingTodo: TodoSnapshot option |})
        : ProjectionSet =
        match env.Stream with
        | StreamId.Workspace ->
            let proj1 =
                { proj with
                    LastReview = Some r.Verdict }

            match r.ResultingTodo with
            | Some todo -> { proj1 with Todos = Some todo }
            | None -> proj1
        | StreamId.Session sessionId ->
            let sessionProjs =
                updateSessionProjection
                    sessionId
                    (fun s ->
                        let s1 = { s with LastReview = Some r.Verdict }

                        match r.ResultingTodo with
                        | Some todo -> { s1 with Todos = Some todo }
                        | None -> s1)
                    proj.SessionProjections

            { proj with
                SessionProjections = sessionProjs }
        | _ -> proj

    let private foldSession (proj: ProjectionSet) (env: Envelope) (sFact: SessionFact) : ProjectionSet =
        match env.Stream with
        | StreamId.Session sessionId ->
            let sessionProjs =
                updateSessionProjection
                    sessionId
                    (fun s ->
                        match sFact with
                        | HumanTurnStarted h -> { s with HumanTurnId = Some h.TurnId }
                        | SessionSettled res ->
                            { s with
                                SettledResult = Some res.Result })
                    proj.SessionProjections

            { proj with
                SessionProjections = sessionProjs }
        | _ -> proj

    let private foldChild (proj: ProjectionSet) (env: Envelope) (cFact: ChildFact) : ProjectionSet =
        match env.Stream with
        | StreamId.Session sessionId ->
            let sessionProjs =
                updateSessionProjection
                    sessionId
                    (fun s ->
                        match cFact with
                        | ChildCreated _ -> s
                        | ChildCompletedFact c ->
                            { s with
                                Children = Map.add c.ChildId c.Result s.Children })
                    proj.SessionProjections

            { proj with
                SessionProjections = sessionProjs }
        | _ -> proj

    let private foldProcess (proj: ProjectionSet) (env: Envelope) (prFact: ProcessFact) : ProjectionSet =
        match env.Stream with
        | StreamId.Session sessionId ->
            let sessionProjs =
                updateSessionProjection
                    sessionId
                    (fun s ->
                        match prFact with
                        | ProcessSpawned _ -> s
                        | ProcessExited p ->
                            { s with
                                Processes = Map.add p.ProcessId p.Result s.Processes })
                    proj.SessionProjections

            { proj with
                SessionProjections = sessionProjs }
        | _ -> proj

    let private foldSquad (proj: ProjectionSet) (env: Envelope) (sqFact: SquadFact) : ProjectionSet =
        match env.Stream with
        | StreamId.Session sessionId ->
            let sessionProjs =
                updateSessionProjection
                    sessionId
                    (fun s ->
                        match sqFact with
                        | TaskVerifiedFact t ->
                            { s with
                                SquadTasks = Map.add t.TaskId t.Result s.SquadTasks }
                        | _ -> s)
                    proj.SessionProjections

            { proj with
                SessionProjections = sessionProjs }
        | _ -> proj

    let foldEnvelope (proj: ProjectionSet) (env: Envelope) : ProjectionSet =
        match env.Fact with
        | Runtime(RuntimeStarted r) ->
            { proj with
                RuntimeId = Some r.RuntimeId }
        | Session s -> foldSession proj env s
        | Todo(TodoChanged t) -> foldTodo proj env t
        | Review(ReviewApplied r) -> foldReview proj env r
        | Prompt _ -> foldPrompt proj env
        | Child c -> foldChild proj env c
        | Process p -> foldProcess proj env p
        | Squad sq -> foldSquad proj env sq

    let apply (proj: ProjectionSet) (envelopes: Envelope list) : ProjectionSet = List.fold foldEnvelope proj envelopes
