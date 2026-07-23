namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

type PromptOptions =
    { Model: string option
      Agent: string option
      Parts: obj list }

type IPromptPort =
    abstract SendPrompt: sessionId: SessionId -> promptText: string -> options: PromptOptions -> Task<SendOutcome>

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

type WaiterMap = Map<string, JsTcs<Fact.PromptOutcome>>

module PromptWaiters =
    let emptyWaiters : WaiterMap = Map.empty

    let registerWaiter (waiters: WaiterMap) (keyString: string) : WaiterMap * JsTcs<Fact.PromptOutcome> =
        let tcs = JsTcs<Fact.PromptOutcome>()
        (Map.add keyString tcs waiters, tcs)

    let trySignalWaiter (waiters: WaiterMap) (keyString: string) (outcome: Fact.PromptOutcome) : WaiterMap =
        match Map.tryFind keyString waiters with
        | Some tcs ->
            tcs.TrySetResult(outcome) |> ignore
            Map.remove keyString waiters
        | None -> waiters
