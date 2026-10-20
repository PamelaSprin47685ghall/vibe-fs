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

/// 人类等待只有一份 durable 来源；通知与物理 interrupt 由宿主组合者执行。
module UserQuestionJournal =
    let private authority (journal: AgentJournal) sessionId =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind sessionId
        |> Option.bind (fun session -> session.PromptAuthority)

    let tryFind journal sessionId callId =
        authority journal sessionId
        |> Option.bind (fun state -> Map.tryFind callId state.UserQuestions)

    let private pendingQuestion journal sessionId =
        authority journal sessionId
        |> Option.bind (fun state ->
            state.ActiveLogicalRun
            |> Option.bind (fun active ->
                state.UserQuestions
                |> Map.toSeq
                |> Seq.tryPick (fun (_, question) ->
                    if
                        question.Answer.IsNone
                        && question.Request.LogicalRunId = active.LogicalRunId
                        && question.Request.AuthorityRootUserMessageId = active.AuthorityRootUserMessageId
                    then
                        Some question
                    else
                        None)))

    let pending journal sessionId =
        pendingQuestion journal sessionId
        |> Option.map (fun question -> question.Request)

    let suppressesTurn journal sessionId authorityRoot providerRun =
        authority journal sessionId
        |> Option.exists (fun state ->
            state.UserQuestions
            |> Map.exists (fun _ question ->
                question.Request.AuthorityRootUserMessageId = authorityRoot
                && (question.Answer.IsNone || question.Request.ProviderRunId = providerRun)))

    let request (journal: AgentJournal) (question: UserQuestionAskedPayload) =
        task {
            let! result =
                AgentJournal.appendAgent
                    (StreamId.Session question.SessionId)
                    (Some question.ProviderRunId)
                    (AgentFact.Prompt(PromptFactCases.UserQuestionAsked question))
                    journal

            return result |> Result.map ignore |> Result.mapError (sprintf "%A")
        }

    /// 只接受已落盘的真实用户准入；插件续跑、通知、旧输入重放都不能回答提问。
    let observeHumanInput (journal: AgentJournal) sessionId physicalId =
        task {
            let key: Wanxiangshu.Execution.Session.ChatExecution.ChatExecutionKey =
                { SessionId = sessionId
                  PhysicalUserMessageId = physicalId }

            let accepted =
                (AgentJournal.snapshot journal).AgentProjections.ChatExecutions
                |> Wanxiangshu.Execution.Session.ChatExecution.ChatExecutionProjection.byKey key
                |> Option.map (fun execution -> execution.acceptedEvidence)

            match pendingQuestion journal sessionId, accepted with
            | Some question, Some input when
                input.LogicalRunId = question.Request.LogicalRunId
                && input.PhysicalUserMessageId <> question.Request.PhysicalUserMessageId
                // LC-015: 提问登记时已准入的输入重放不构成真实新答复。
                && not (Set.contains physicalId question.PriorPhysicalInputs)
                && (input.Origin = PromptOrigin.AuthorityRoot PromptRootAuthorityKind.HumanRoot
                    || input.Origin = PromptOrigin.Continuation PromptContinuationKind.HumanMessage)
                ->
                let! result =
                    AgentJournal.appendAgent
                        (StreamId.Session sessionId)
                        None
                        (AgentFact.Prompt(
                            PromptFactCases.UserQuestionAnswered
                                { SessionId = sessionId
                                  LogicalRunId = question.Request.LogicalRunId
                                  ToolCallId = question.Request.ToolCallId
                                  PhysicalUserMessageId = physicalId }
                        ))
                        journal

                return result |> Result.map ignore |> Result.mapError (sprintf "%A")
            | _ -> return Ok()
        }
