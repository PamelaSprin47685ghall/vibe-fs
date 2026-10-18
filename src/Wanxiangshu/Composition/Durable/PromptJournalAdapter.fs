namespace Wanxiangshu.Composition.Durable

open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Persistence.Journal

module PromptJournalAdapter =

    [<Emit("new WeakMap()")>]
    let private newRuntimeCache () : obj = jsNative

    [<Emit("$0.get($1)")>]
    let private tryRuntime (cache: obj) (journal: AgentJournal) : PromptDispatcher.Runtime option = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private rememberRuntime (cache: obj) (journal: AgentJournal) (runtime: PromptDispatcher.Runtime) : unit =
        jsNative

    // DSL-MUTABLE: resource — one dispatch runtime per live Journal, not a durable authority cache.
    let private runtimes = newRuntimeCache ()

    let create (journal: AgentJournal) : IPromptJournal =
        { new IPromptJournal with
            member _.RuntimeId = AgentJournal.runtimeId journal

            member _.ProjectionFor sessionId =
                let snapshot = AgentJournal.snapshot journal

                let authority =
                    Map.tryFind sessionId snapshot.AgentProjections.Sessions
                    |> Option.bind (fun s -> s.PromptAuthority)
                    |> Option.defaultValue PromptAuthorityLedger.empty

                let targetSessionId =
                    FissionProjection.tryOwnerOfLane sessionId snapshot.AgentProjections.Fission
                    |> Option.defaultValue sessionId

                let ownerAuthority =
                    if targetSessionId = sessionId then
                        None
                    else
                        Map.tryFind targetSessionId snapshot.AgentProjections.Sessions
                        |> Option.bind (fun s -> s.PromptAuthority)

                let inheritedRun =
                    ownerAuthority |> Option.bind (fun ownerAuth -> ownerAuth.ActiveLogicalRun)

                match authority.ActiveLogicalRun, inheritedRun with
                | None, Some run ->
                    { authority with
                        ActiveLogicalRun = Some run }
                | _ -> authority

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

    let forJournal (journal: AgentJournal) =
        match tryRuntime runtimes journal with
        | Some runtime -> runtime
        | None ->
            let runtime = PromptDispatcher.forPrompts (create journal)
            rememberRuntime runtimes journal runtime
            runtime
