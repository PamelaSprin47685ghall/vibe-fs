namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegatedToolEstimateLedger =

    let tryState (journal: AgentJournal) sessionId =
        AgentJournal.snapshot journal
        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.DelegatedToolEstimate)

    let tryRemaining journal sessionId =
        tryState journal sessionId
        |> Option.map DelegatedToolEstimateProjection.remaining

    let private append journal sessionId fact =
        task {
            try
                let! _ = AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal
                return ()
            with _ ->
                return ()
        }

    let replace (journal: AgentJournal) sessionId expectedToolCalls : Task<unit> =
        append
            journal
            sessionId
            (DelegationFact.DelegatedToolEstimateReplaced
                {| SessionId = sessionId
                   ExpectedToolCalls = expectedToolCalls |})

    let observe (journal: AgentJournal) sessionId toolCallId : Task<unit> =
        task {
            match tryRemaining journal sessionId with
            | Some remaining when remaining > 0 ->
                do!
                    append
                        journal
                        sessionId
                        (DelegationFact.DelegatedToolCallObserved
                            {| SessionId = sessionId
                               ToolCallId = toolCallId |})
            | _ -> ()
        }
