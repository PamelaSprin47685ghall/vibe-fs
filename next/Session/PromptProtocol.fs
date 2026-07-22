namespace Wanxiangshu.Next.Session

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

type PromptHistory =
    { Key: string
      UserMessageId: MessageId option
      AssistantMessageId: MessageId option
      Outcome: Fact.PromptOutcome option
      CompletedAt: DateTimeOffset option }

type PendingPrompt =
    { RequestKey: PromptKey
      DispatchId: DispatchId
      UserMessageId: MessageId option
      SubmittedAt: DateTimeOffset }

type HistoricalPromptIndex = Map<string, PromptHistory>

type LocalPromptProtocol = Map<SessionId, PendingPrompt option>

type SendOnceDecision =
    | HistoricalHit of PromptHistory
    | LocalPending of PendingPrompt
    | SendNew
    | Uncertain of reason: string

module PromptProtocol =

    let emptyHistoricalIndex: HistoricalPromptIndex = Map.empty

    let emptyLocalProtocol: LocalPromptProtocol = Map.empty

    let rebuildHistoricalIndex (historicalPrompts: Map<string, PromptHistoryRecord>) : HistoricalPromptIndex =
        historicalPrompts
        |> Map.map (fun k record ->
            { Key = record.PromptKey
              UserMessageId = record.UserMessageId
              AssistantMessageId = record.AssistantMessageId
              Outcome = record.Outcome
              CompletedAt = record.CompletedAt })

    let evaluateSendOnce
        (historical: HistoricalPromptIndex)
        (local: LocalPromptProtocol)
        (key: PromptKey)
        : SendOnceDecision =
        let sessionId = PromptKey.sessionId key

        match Map.tryFind sessionId local with
        | Some(Some pending) -> LocalPending pending
        | _ ->
            let keyString = PromptKey.asString key

            match Map.tryFind keyString historical with
            | Some history ->
                if history.Outcome.IsSome then
                    HistoricalHit history
                elif history.UserMessageId.IsSome then
                    Uncertain "submitted-without-terminal"
                else
                    Uncertain "requested-without-terminal"
            | None -> SendNew

    let recordSubmitted
        (local: LocalPromptProtocol)
        (key: PromptKey)
        (dispatchId: DispatchId)
        (userMessageId: MessageId)
        (now: DateTimeOffset)
        : LocalPromptProtocol =
        let sessionId = PromptKey.sessionId key

        let pending =
            { RequestKey = key
              DispatchId = dispatchId
              UserMessageId = Some userMessageId
              SubmittedAt = now }

        Map.add sessionId (Some pending) local

    let recordTerminal
        (historical: HistoricalPromptIndex)
        (local: LocalPromptProtocol)
        (key: PromptKey)
        (userMessageId: MessageId option)
        (assistantMessageId: MessageId option)
        (outcome: Fact.PromptOutcome)
        (now: DateTimeOffset)
        : HistoricalPromptIndex * LocalPromptProtocol =
        let keyString = PromptKey.asString key

        let history =
            { Key = keyString
              UserMessageId = userMessageId
              AssistantMessageId = assistantMessageId
              Outcome = Some outcome
              CompletedAt = Some now }

        let newHistorical = Map.add keyString history historical
        let sessionId = PromptKey.sessionId key

        let newLocal =
            match Map.tryFind sessionId local with
            | Some(Some pending) when PromptKey.asString pending.RequestKey = keyString -> Map.add sessionId None local
            | _ -> local

        (newHistorical, newLocal)
