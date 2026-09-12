namespace Wanxiangshu.Composition.Durable

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Persistence.Journal

module PromptJournalAdapter =

    let create (journal: AgentJournal) : IPromptJournal =
        { new IPromptJournal with
            member _.RuntimeId = AgentJournal.runtimeId journal

            member _.ProjectionFor sessionId =
                let snapshot = AgentJournal.snapshot journal

                Map.tryFind sessionId snapshot.AgentProjections.Sessions
                |> Option.bind (fun s -> s.PromptAuthority)
                |> Option.defaultValue PromptAuthorityLedger.empty

            member _.Append sessionId providerRun fact =
                task {
                    let agentFact =
                        match fact with
                        | PromptSessionFact.AuthorityRootAccepted payload -> PromptFact.AuthorityRootAccepted payload
                        | PromptSessionFact.PromptClaimed payload -> PromptFact.PluginPromptClaimed payload
                        | PromptSessionFact.PromptSubmitted payload -> PromptFact.PluginPromptSubmitted payload
                        | PromptSessionFact.PromptPhysicalAccepted payload ->
                            PromptFact.PluginPromptPhysicalAccepted payload
                        | PromptSessionFact.PromptAbandoned payload -> PromptFact.PluginPromptAbandoned payload

                    let! result = AgentJournal.appendAgent (StreamId.Session sessionId) providerRun agentFact journal

                    return Result.map ignore result
                }

            member _.HandleForChild(sessionId: SessionId) : Wanxiangshu.Execution.Delegation.HandleRecord option =
                Map.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections.HandleByChildSession

            member _.ChatAcceptancePersistence
                ()
                : Wanxiangshu.Execution.Session.ChatExecution.ManagedChatAcceptancePersistence =
                { ReadExact =
                    fun key ->
                        (AgentJournal.snapshot journal).AgentProjections.ChatExecutions
                        |> Wanxiangshu.Execution.Session.ChatExecution.ChatExecutionProjection.byKey key
                  AppendAccepted =
                    fun key evidence ->
                        task {
                            let! appended =
                                AgentJournal.appendAgent
                                    (StreamId.Session key.SessionId)
                                    None
                                    (Wanxiangshu.Composition.Durable.ChatExecutionFact.Accepted
                                        {| SchemaVersion = 1
                                           Key = key
                                           Evidence = evidence |})
                                    journal

                            return appended |> Result.map ignore
                        } } }
