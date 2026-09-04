namespace Wanxiangshu.Mission.Finality.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

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
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private refuse (ctx: HostToolContext) path =
        ToolHostCodec.tomlObjectWithInstructions (ProviderProse.instructionLines (lang ctx) path Map.empty) []

    /// GLORY-062/076 + §9.2.4: at-rest second-suicide tool result (session language).
    let private restInPeaceInstructions (sessionId: SessionId) =
        ProviderProse.instructionLines (SessionProviderLanguage.languageOf sessionId) FinalityPrompt.Path.Rest Map.empty

    let private tString = ToolHostCodec.TString

    let private fatalInfrastructure (sid: SessionId) (ex: exn) : 'T =
        Diagnostic.fatal "finality-infrastructure-failed" [ "session_id", SessionId.value sid; "result", ex.ToString() ]
        failwith ("finality-fatal-unreachable:" + ex.Message)

    let private infrastructureError operation error : 'T =
        raise (InvalidOperationException(operation + ":" + error))

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
        (observer: IWaitObserver)
        (sid: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (source: string)
        (pending: Task<FinalityOutcome>)
        : Task<FinalityOutcome> =
        CausalAwait.awaitTask observer (describeFinalityRequest sid lifeId requestId source) pending

    let private finalityPorts (scope: ToolRuntimeScope) (sessionId: SessionId) =
        FinalityHostPort.create
            scope
            sessionId
            (defaultArg scope.FinalityReviewerTimeoutMs Distillation.AwaitAgentTimeoutMs)

    let private treeHashOf (hash: string) =
        if String.IsNullOrWhiteSpace hash then
            None
        else
            Some(GitTreeHash.create hash)

    let private readTreeHash (port: GitTreePort) =
        try
            Ok(port.GetTreeHash().Trim())
        with ex ->
            Error ex.Message

    let private requireTreeHash =
        function
        | Some tree -> Ok tree
        | None -> Error "tree-adapter-empty-hash"

    let private tryTreeHash (port: GitTreePort) =
        readTreeHash port |> Result.bind (treeHashOf >> requireTreeHash)

    let private treeOf (scope: ToolRuntimeScope) (sessionId: string) =
        match scope.TreePortFor sessionId with
        | None -> Error "tree-adapter-unavailable"
        | Some port -> tryTreeHash port

    /// GLORY-037.11-13: any outstanding child work blocks the ending.
    let private outstandingWork (scope: ToolRuntimeScope) (context: HostToolContext) =
        let sid = SessionId.create context.SessionId

        let durableOutstanding =
            TerminalPolicy.outstandingBackground scope.Journal scope.HasLivePty (Some Role.Manager) sid

        let runtimeOutstanding =
            match scope.RuntimeFor context with
            | Ok runtime -> runtime.PendingRunCount > 0
            | Error error -> infrastructureError "runtime-lookup" error

        durableOutstanding || runtimeOutstanding

    /// GLORY-062 durable half of the second suicide. `LifeCompleted` is a
    /// business fact, not physical provider-terminal evidence. The tool-call
    /// step still has to return its result to the same physical execution so the
    /// provider can emit the final assistant message; that exact Host terminal
    /// is published by the ordinary terminal reporter.
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
            | Error error -> return infrastructureError "blessed-life-completion" (sprintf "%A" error)
            | Ok BlessedLifeCompletion.AlreadyCompleted ->
                return ToolHostCodec.tomlObjectWithInstructions (restInPeaceInstructions sid) []
            | Ok(BlessedLifeCompletion.Completed _) ->
                return ToolHostCodec.tomlObjectWithInstructions (restInPeaceInstructions sid) []
        }

    let private renderOutcome (outcome: FinalityOutcome) =
        match outcome with
        | FinalityOutcome.Rejected prompt
        | FinalityOutcome.Blessed prompt -> prompt

    let private endingPrerequisiteRefusal (scope: ToolRuntimeScope) (context: HostToolContext) (lastWords: string) =
        if String.IsNullOrWhiteSpace lastWords then
            Some(refuse context Path.CallAgainWithLastWords)
        elif context.ToolCallId.IsNone || context.ProviderRunId.IsNone then
            Some(refuse context Path.TryAgainLater)
        elif outstandingWork scope context then
            Some(refuse context Path.CallJoinBeforeEnd)
        else
            None

    let private resumeFinalityRequest
        (scope: ToolRuntimeScope)
        (sid: SessionId)
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        =
        task {
            let reviewerPort, _ = finalityPorts scope sid

            match! FinalityWorkflow.resume reviewerPort scope.Journal sid life.LifeId request.RequestId with
            | Some outcome -> return renderOutcome outcome
            | None -> return ToolHostCodec.tomlObject [ "status", tString "already_received" ]
        }

    let private recoverEmptyMembers
        (scope: ToolRuntimeScope)
        (sid: SessionId)
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        =
        task {
            let reviewerPort, treePort = finalityPorts scope sid

            let! outcome =
                awaitFinalityStart
                    scope.WaitObserver
                    sid
                    life.LifeId
                    request.RequestId
                    "FinalityTool.recoverEmptyMembers"
                    (FinalityWorkflow.start
                        scope.WaitObserver
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
        }

    let private startFinalityFromBlob
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (life: LifeProjection)
        (tree: GitTreeHash)
        (lastWords: string)
        =
        task {
            let! blob =
                task {
                    match! journal.WriteBlob lastWords with
                    | Ok blob -> return blob
                    | Error error -> return infrastructureError "last-words-blob-write" error
                }

            let requestId = FinalityRequestId.create (Guid.NewGuid().ToString("N"))
            let reviewerPort, treePort = finalityPorts scope sid

            let! outcome =
                awaitFinalityStart
                    scope.WaitObserver
                    sid
                    life.LifeId
                    requestId
                    "FinalityTool.acceptSuicide"
                    (FinalityWorkflow.start
                        scope.WaitObserver
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

    let private beginFinalityEnding
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (sessionId: string)
        (life: LifeProjection)
        (lastWords: string)
        =
        match endingPrerequisiteRefusal scope context lastWords, treeOf scope sessionId with
        | Some refusal, _ -> Task.FromResult refusal
        | None, Error error -> task { return invalidOp ("finality-tree-read-failed:" + error) }
        | None, Ok tree -> startFinalityFromBlob scope journal context sid life tree lastWords

    let private completeBlessedEnding
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (life: LifeProjection)
        (blessing: BlessingEvidence)
        (lastWords: string)
        =
        match endingPrerequisiteRefusal scope context lastWords with
        | Some refusal -> Task.FromResult refusal
        | None -> completeBlessedLife scope journal context sid life blessing lastWords

    let private acceptSuicide
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (sessionId: string)
        (life: LifeProjection)
        (lastWords: string)
        (snapshot: ProjectionSet)
        =
        let hasPlanCommitment =
            MagicTodoProjection.tryLife life.LifeId snapshot.AgentProjections.MagicTodo
            |> Option.exists MagicTodoProjection.isPlanCommitted

        let disposition =
            ManagerFinality.classifyEnding context.ToolCallId life hasPlanCommitment

        let exec: ManagerFinality.FinalityEndingExecution =
            { AlreadyCompleted = fun () -> ToolHostCodec.tomlObject [ "status", tString "already_completed" ]
              ResumeRequest = fun request -> resumeFinalityRequest scope sid life request
              RecoverEmptyMembers = fun request -> recoverEmptyMembers scope sid life request
              CompleteBlessedLife =
                fun blessing -> completeBlessedEnding scope journal context sid life blessing lastWords
              BeginFinality = fun () -> beginFinalityEnding scope journal context sid sessionId life lastWords }

        task {
            let! outcome = ManagerFinality.handleEnding disposition exec

            return
                match outcome with
                | ManagerFinality.FinalityEndingOutcome.Refused path -> refuse context path
                | ManagerFinality.FinalityEndingOutcome.Result result -> result
        }

    let private executeManagerEnding
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (sessionId: string)
        (lastWords: string)
        =
        task {
            match! ManagerLifeWorkflow.ensureEndingLife journal sid with
            | Error error -> return infrastructureError "ending-life-admission" (sprintf "%A" error)
            | Ok None -> return refuse context Path.ContinueWorking
            | Ok(Some life) ->
                let snapshot = AgentJournal.snapshot journal
                return! acceptSuicide scope journal context sid sessionId life lastWords snapshot
        }

    let private executeManagerEndingFatal
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (context: HostToolContext)
        (sid: SessionId)
        (sessionId: string)
        (lastWords: string)
        =
        task {
            try
                return! executeManagerEnding scope journal context sid sessionId lastWords
            with ex ->
                return fatalInfrastructure sid ex
        }

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let lastWords = args.Text "last_words"

            match scope.RoleFor context, String.IsNullOrWhiteSpace context.SessionId, scope.Journal with
            | Some Role.Manager, true, _ -> return refuse context Path.TryAgainLater
            | Some Role.Manager, false, None ->
                let sid = SessionId.create context.SessionId
                return fatalInfrastructure sid (InvalidOperationException "finality-journal-required")
            | Some Role.Manager, false, Some journal ->
                let sessionId = context.SessionId
                let sid = SessionId.create sessionId
                return! executeManagerEndingFatal scope journal context sid sessionId lastWords
            | Some _, _, _ -> return refuse context Path.WrongRole
            | None, _, _ -> return refuse context Path.TryAgainLater
        }

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Finality)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "suicide"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "last_words", ToolHostCodec.stringSchema factory ]
          Admission = admission
          Execute = execute scope }
