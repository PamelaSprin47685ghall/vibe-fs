namespace Wanxiangshu.Mission.Finality.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.OpenCode
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// GLORY-034/035/037/041: the Manager's end-of-life tool.
///
/// The tool is deliberately opaque to the Manager: the description never
/// mentions review, the reviewer, PERFECT, or the barrier (SURFACE-005). A
/// legal call validates the immediate contract, persists submitted last_words,
/// builds Host ports, and enters Application `FinalityWorkflow`; Application owns
/// `FinalityRequested` and every later lifecycle fact. Every precondition failure
/// returns a narrative refusal before a new Finality request is admitted
/// (GLORY-038/039).
module FinalityTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/suicide/description"

        [<Literal>]
        let TryAgainLater = "tool/suicide/try-again-later"

        [<Literal>]
        let ContinueWorking = "tool/suicide/continue-working"

        [<Literal>]
        let CallAgainWithLastWords = "tool/suicide/call-again-with-last-words"

        [<Literal>]
        let CallJoinBeforeEnd = "tool/suicide/call-join-before-end"

        [<Literal>]
        let SeekEndWhenReady = "tool/suicide/seek-end-when-ready"

        [<Literal>]
        let WaitForCurrentEnding = "tool/suicide/wait-for-current-ending"

        [<Literal>]
        let WrongRole = "tool/suicide/wrong-role"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private refuse (ctx: HostToolContext) path =
        ToolHostCodec.tomlObjectWithInstructions (ProviderProse.instructionLines (lang ctx) path Map.empty) []

    /// GLORY-062/076 + §9.2.4: at-rest second-suicide tool result (session language).
    let private restInPeaceInstructions (sessionId: SessionId) =
        ProviderProse.instructionLines (ProviderProse.languageOf sessionId) FinalityPrompt.Path.Rest Map.empty

    let private tString = ToolHostCodec.TString

    let private describeFinalityRequest
        (sid: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (source: string)
        : DiagnosticWait =
        DiagnosticWait.create
            "finality-request"
            (CausalOwner.create "ManagerWorkflow" [ "session", SessionId.value sid; "life", ManagerLifeId.value lifeId ])
            [ "session", SessionId.value sid
              "life", ManagerLifeId.value lifeId
              "request", FinalityRequestId.value requestId ]
            (WorkflowProducer(
                CausalOwner.create
                    "FinalityWorkflow"
                    [ "request", FinalityRequestId.value requestId; "session", SessionId.value sid ]
            ))
            [ WaitEscape.SessionLifetime
              WaitEscape.CancelledBy(CausalOwner.create "ManagerWorkflow" [ "session", SessionId.value sid ]) ]
            source

    let private awaitFinalityStart
        (sid: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (source: string)
        (pending: Task<FinalityOutcome>)
        : Task<FinalityOutcome> =
        CausalAwait.awaitTask CausalWaitHub.observer (describeFinalityRequest sid lifeId requestId source) pending

    let private finalityPorts (scope: ToolRuntimeScope) (sessionId: SessionId) =
        FinalityHostPort.create
            scope
            sessionId
            (defaultArg scope.FinalityReviewerTimeoutMs Distillation.AwaitAgentTimeoutMs)

    let private treeOf (scope: ToolRuntimeScope) (sessionId: string) =
        match scope.TreePortFor sessionId with
        | None -> None
        | Some port ->
            try
                let hash = port.GetTreeHash().Trim()

                if String.IsNullOrWhiteSpace hash then
                    None
                else
                    Some(GitTreeHash.create hash)
            with _ ->
                None

    /// GLORY-037.11-13: any outstanding child work blocks the ending.
    let private outstandingWork (scope: ToolRuntimeScope) (context: HostToolContext) =
        let sid = SessionId.create context.SessionId

        let durableOutstanding =
            TerminalPolicy.outstandingBackground scope.Journal scope.HasLivePty (Some Role.Manager) sid

        let runtimeOutstanding =
            match scope.RuntimeFor context with
            | Ok runtime -> runtime.PendingRunCount > 0
            | Error _ -> false

        durableOutstanding || runtimeOutstanding

    /// GLORY-062 physical half of the second suicide. Application owns every
    /// durable Life transition; this adapter only publishes the resulting Host
    /// terminal after `LifeCompleted` has landed.
    let private completeBlessedLife
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (life: LifeProjection)
        (blessing: BlessingEvidence)
        (lastWords: string)
        : Task<string> =
        task {
            let providerRun = context.ProviderRunId.Value

            match! ManagerLifeWorkflow.completeBlessedLife journal sid life blessing lastWords providerRun with
            | Error _ -> return refuse context Path.TryAgainLater
            | Ok BlessedLifeCompletion.AlreadyCompleted ->
                return ToolHostCodec.tomlObjectWithInstructions (restInPeaceInstructions sid) []
            | Ok(BlessedLifeCompletion.Completed authorityRoot) ->
                match scope.EventPort with
                | Some eventPort ->
                    let runResult: AgentRunResult =
                        { SessionId = sid
                          AuthorityRootUserMessageId = authorityRoot
                          ProviderRun = providerRun
                          Role = Role.Manager
                          Directory = scope.DirectoryFor(SessionId.value sid)
                          TerminalText = lastWords
                          TurnFormalText = lastWords }

                    eventPort.NotifyTerminal sid (TerminalOutcome.Completed runResult) |> ignore
                | None -> ()

                return ToolHostCodec.tomlObjectWithInstructions (restInPeaceInstructions sid) []
        }

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let lastWords = args.Text "last_words"

            match scope.RoleFor context with
            | Some Role.Manager ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return refuse context Path.TryAgainLater
                else
                    let sessionId = context.SessionId
                    let sid = SessionId.create sessionId

                    match scope.Journal with
                    | None -> return refuse context Path.TryAgainLater
                    | Some journal ->
                        let renderOutcome outcome =
                            match outcome with
                            | FinalityOutcome.Rejected prompt
                            | FinalityOutcome.Blessed prompt
                            | FinalityOutcome.Undecided prompt -> prompt

                        let acceptSuicide (life: LifeProjection) =
                            task {
                                let hasPlanCommitment =
                                    MagicTodoProjection.tryLife
                                        life.LifeId
                                        (AgentJournal.snapshot journal).AgentProjections.MagicTodo
                                    |> Option.exists MagicTodoProjection.isPlanCommitted

                                match ManagerFinality.classifyEnding context.ToolCallId life hasPlanCommitment with
                                | ManagerFinality.EndingDisposition.ContinuePlanning ->
                                    return refuse context Path.ContinueWorking

                                | ManagerFinality.EndingDisposition.AlreadyCompleted ->
                                    return ToolHostCodec.tomlObject [ "status", tString "already_completed" ]

                                | ManagerFinality.EndingDisposition.ResumeRequest request ->
                                    let reviewerPort, _ = finalityPorts scope sid

                                    match!
                                        FinalityWorkflow.resume
                                            reviewerPort
                                            scope.Journal
                                            sid
                                            life.LifeId
                                            request.RequestId
                                    with
                                    | Some outcome -> return renderOutcome outcome
                                    | None -> return ToolHostCodec.tomlObject [ "status", tString "already_received" ]

                                | ManagerFinality.EndingDisposition.RecoverRequestWithoutReviewers request ->
                                    let reviewerPort, treePort = finalityPorts scope sid

                                    let! outcome =
                                        awaitFinalityStart
                                            sid
                                            life.LifeId
                                            request.RequestId
                                            "FinalityTool.recoverEmptyMembers"
                                            (FinalityWorkflow.start
                                                reviewerPort
                                                treePort
                                                scope.Journal
                                                sid
                                                life.LifeId
                                                request.RequestId
                                                request.GitTreeHash
                                                request.LastWordsRef
                                                request.LastWordsDigest
                                                request.ProviderRun
                                                request.ToolCallId)

                                    return renderOutcome outcome

                                | ManagerFinality.EndingDisposition.WaitForCurrentRequest ->
                                    return refuse context Path.WaitForCurrentEnding

                                // Split arms (no `(A|B) as disposition`) — Fable FS0038 double-bind.
                                | ManagerFinality.EndingDisposition.CompleteBlessedLife blessing ->
                                    if String.IsNullOrWhiteSpace lastWords then
                                        return refuse context Path.CallAgainWithLastWords
                                    elif context.ToolCallId.IsNone || context.ProviderRunId.IsNone then
                                        return refuse context Path.TryAgainLater
                                    elif outstandingWork scope context then
                                        return refuse context Path.CallJoinBeforeEnd
                                    else
                                        return! completeBlessedLife scope journal context sid life blessing lastWords

                                | ManagerFinality.EndingDisposition.BeginFinality ->
                                    if String.IsNullOrWhiteSpace lastWords then
                                        return refuse context Path.CallAgainWithLastWords
                                    elif context.ToolCallId.IsNone || context.ProviderRunId.IsNone then
                                        return refuse context Path.TryAgainLater
                                    elif outstandingWork scope context then
                                        return refuse context Path.CallJoinBeforeEnd
                                    else
                                        match treeOf scope sessionId with
                                        | None -> return refuse context Path.SeekEndWhenReady
                                        | Some tree ->
                                            match! journal.WriteBlob lastWords with
                                            | Error _ -> return refuse context Path.SeekEndWhenReady
                                            | Ok blob ->
                                                let requestId = FinalityRequestId.create (Guid.NewGuid().ToString("N"))

                                                let reviewerPort, treePort = finalityPorts scope sid

                                                let! outcome =
                                                    awaitFinalityStart
                                                        sid
                                                        life.LifeId
                                                        requestId
                                                        "FinalityTool.acceptSuicide"
                                                        (FinalityWorkflow.start
                                                            reviewerPort
                                                            treePort
                                                            scope.Journal
                                                            sid
                                                            life.LifeId
                                                            requestId
                                                            tree
                                                            blob.BlobRef
                                                            blob.BlobDigest
                                                            context.ProviderRunId.Value
                                                            context.ToolCallId.Value)

                                                return renderOutcome outcome
                            }

                        match! ManagerLifeWorkflow.ensureEndingLife journal sid with
                        | Error _ -> return refuse context Path.TryAgainLater
                        | Ok None -> return refuse context Path.ContinueWorking
                        | Ok(Some life) -> return! acceptSuicide life
            | Some _ -> return refuse context Path.WrongRole
            | None -> return refuse context Path.TryAgainLater
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "suicide"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "last_words", ToolHostCodec.stringSchema factory ]
          Execute = execute scope }
