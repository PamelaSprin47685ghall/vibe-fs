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

type ProjectionSet =
    { Todos: TodoSnapshot option
      LastReview: ReviewVerdict option
      HistoricalPrompts: Map<string, PromptHistoryRecord>
      RuntimeId: RuntimeId option }

type RuntimeSnapshot =
    { Frontier: Frontier
      Projections: ProjectionSet
      OwnRuntimeId: RuntimeId option
      OwnLocalSeq: int64 }

module Fold =

    let empty: ProjectionSet =
        { Todos = None
          LastReview = None
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

    let foldEnvelope (proj: ProjectionSet) (env: Envelope) : ProjectionSet =
        match env.Fact with
        | Runtime(RuntimeStarted r) ->
            { proj with
                RuntimeId = Some r.RuntimeId }

        | Todo(TodoChanged t) -> { proj with Todos = Some t.Snapshot }

        | Review(ReviewApplied r) ->
            let proj1 =
                { proj with
                    LastReview = Some r.Verdict }

            match r.ResultingTodo with
            | Some todo -> { proj1 with Todos = Some todo }
            | None -> proj1

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

    let apply (proj: ProjectionSet) (envelopes: Envelope list) : ProjectionSet = List.fold foldEnvelope proj envelopes
