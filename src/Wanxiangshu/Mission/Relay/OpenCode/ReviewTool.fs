namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module ReviewTool =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/review/description"

        [<Literal>]
        let Received = "tool/review/received"

        [<Literal>]
        let Rejected = "tool/review/rejected"

        [<Literal>]
        let EmptyNarrative = "tool/review/empty-narrative"

        [<Literal>]
        let AbsentToolCall = "tool/review/absent-tool-call"

        [<Literal>]
        let MissingUserRoot = "tool/review/missing-user-root"

        [<Literal>]
        let WrongToolName = "tool/review/wrong-tool-name"

        [<Literal>]
        let RunMismatch = "tool/review/run-mismatch"

        [<Literal>]
        let AmbiguousToolCall = "tool/review/ambiguous-tool-call"

        [<Literal>]
        let MissingProviderMessage = "tool/review/missing-provider-message"

        [<Literal>]
        let NoSnapshotPort = "tool/review/no-snapshot-port"

        [<Literal>]
        let TranscriptLoadFailed = "tool/review/transcript-load-failed"

        [<Literal>]
        let NoActiveIncumbency = "tool/review/no-active-incumbency"

        [<Literal>]
        let IncumbencyChanged = "tool/review/incumbency-changed"

        [<Literal>]
        let MissingToolCallId = "tool/review/missing-tool-call-id"

        [<Literal>]
        let MissingProviderRun = "tool/review/missing-provider-run"

        [<Literal>]
        let MissingUserRootContext = "tool/review/missing-user-root-context"

        [<Literal>]
        let MissingWorkspace = "tool/review/missing-workspace"

        [<Literal>]
        let MissingJournal = "tool/review/missing-journal"

        [<Literal>]
        let MissingAuthorityRoot = "tool/review/missing-authority-root"

        [<Literal>]
        let ScoreArgument = "tool/review/score-argument"

    let private fields = ScoreDimension.all |> List.map ScoreDimension.fieldName

    let private providerText (path: string) substitutions =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path substitutions

    let private rejected reason =
        ToolHostCodec.tomlObjectWithInstructions
            [ providerText Path.Rejected (Map [ "reason", reason ]) ]
            [ "recorded", ToolHostCodec.TBool false ]

    let private requireSome error =
        function
        | Some value -> Ok value
        | None -> Error error

    let private bindTaskResult binder pending =
        task {
            let! outcome = pending

            match outcome with
            | Ok value -> return! binder value
            | Error error -> return Error error
        }

    let private bindResultTask binder result =
        match result with
        | Ok value -> binder value
        | Error error -> Task.FromResult(Error error)

    let private narrativeOf texts =
        let narrative = texts |> List.rev |> String.concat "\n" |> (fun text -> text.Trim())

        if String.IsNullOrWhiteSpace narrative then
            Error(providerText Path.EmptyNarrative Map.empty)
        else
            Ok narrative

    let private publicNarrativeBefore (toolCallId: ToolCallId) (message: SessionMessage) =
        let expected = ToolCallId.value toolCallId

        let nextPart index texts part =
            match part with
            | MessagePart.ToolCall(callId, name, _) when callId = expected && name = "review" ->
                narrativeOf texts |> Result.map Choice2Of2
            | MessagePart.Text text -> Choice1Of2(index + 1, text :: texts) |> Ok
            | MessagePart.Reasoning _
            | MessagePart.ToolCall _
            | MessagePart.ToolResult _
            | MessagePart.Activity _ -> Choice1Of2(index + 1, texts) |> Ok

        let rec loop index texts =
            if index >= message.Parts.Length then
                Error(providerText Path.AbsentToolCall Map.empty)
            else
                nextPart index texts message.Parts.[index]
                |> Result.bind (function
                    | Choice1Of2(nextIndex, nextTexts) -> loop nextIndex nextTexts
                    | Choice2Of2 narrative -> Ok narrative)

        loop 0 []

    let private messageText (message: SessionMessage) =
        message.Parts
        |> Array.choose (function
            | MessagePart.Text text -> Some text
            | _ -> None)
        |> String.concat "\n"
        |> fun value -> value.Trim()

    let private requireRootText text =
        if String.IsNullOrWhiteSpace text then
            Error(providerText Path.MissingUserRoot Map.empty)
        else
            Ok text

    let private rootText (physicalUserMessageId: string) (messages: SessionMessage list) =
        match messages |> List.tryFind (fun message -> message.Id = physicalUserMessageId) with
        | None -> Error(providerText Path.MissingUserRoot Map.empty)
        | Some message -> message |> messageText |> requireRootText

    let private validateLocation providerRun (location: SessionSnapshot.ToolCallLocation) =
        if location.ToolName <> "review" then
            Error(providerText Path.WrongToolName Map.empty)
        elif location.ProviderRun <> providerRun then
            Error(providerText Path.RunMismatch Map.empty)
        else
            Ok location

    let private bindingFromMessages physicalUserMessageId rootAuthorityUserMessageId toolCallId providerRun messages =
        SessionSnapshot.locateToolCall toolCallId messages
        |> Result.mapError (fun _ -> providerText Path.AmbiguousToolCall Map.empty)
        |> Result.bind (validateLocation providerRun)
        |> Result.bind (fun location ->
            messages
            |> List.tryFind (fun message -> message.Id = ProviderRunIdentity.value providerRun)
            |> requireSome (providerText Path.MissingProviderMessage Map.empty)
            |> Result.bind (fun message ->
                rootText rootAuthorityUserMessageId messages
                |> Result.bind (fun root ->
                    publicNarrativeBefore toolCallId message
                    |> Result.map (fun narrative ->
                        let narrativeDigest = HostDigest.sha256Hex narrative
                        let rootDigest = HostDigest.sha256Hex root
                        let payloadDigest = HostDigest.sha256Hex location.InputCanonical

                        let requirementSetDigest =
                            HostDigest.sha256Hex (
                                "requirements-v1\n" + rootAuthorityUserMessageId + "\n" + rootDigest
                            )

                        let evidenceFrontierDigest =
                            HostDigest.sha256Hex (
                                String.concat
                                    "\n"
                                    [ ProviderRunIdentity.value providerRun
                                      ToolCallId.value toolCallId
                                      narrativeDigest
                                      rootDigest
                                      payloadDigest ]
                            )

                        { PhysicalUserMessageId = physicalUserMessageId
                          ProviderRunId = ProviderRunIdentity.value providerRun
                          ToolCallId = ToolCallId.value toolCallId
                          NarrativeDigest = narrativeDigest
                          PayloadDigest = payloadDigest
                          RootRequestDigest = rootDigest
                          RequirementSetDigest = requirementSetDigest
                          EvidenceFrontierDigest = evidenceFrontierDigest }))))

    let private captureBinding
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (physicalUserMessageId: string)
        (rootAuthorityUserMessageId: string)
        (toolCallId: ToolCallId)
        (providerRun: ProviderRunIdentity)
        : Task<Result<AssessmentBinding, string>> =
        match scope.Snapshot with
        | None -> Task.FromResult(Error(providerText Path.NoSnapshotPort Map.empty))
        | Some snapshot ->
            task {
                let! loaded = snapshot.GetMessages(SessionId.create context.SessionId)

                return
                    loaded
                    |> Result.mapError (fun error -> providerText Path.TranscriptLoadFailed (Map [ "detail", error ]))
                    |> Result.bind (
                        bindingFromMessages physicalUserMessageId rootAuthorityUserMessageId toolCallId providerRun
                    )
            }

    let private currentRelayState (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)

    let private assessmentTransaction
        (state: RelayState option)
        (roadId: RoadId)
        (incumbencyId: IncumbencyId)
        (assessmentId: AssessmentId)
        (binding: AssessmentBinding)
        (snapshotId: WorkspaceSnapshotId)
        (authorityRevision: AuthorityRevision)
        (scores: ScoreVector)
        =
        let assessment =
            RelayEvent.AssessmentCommitted(assessmentId, binding, snapshotId, authorityRevision, scores)

        let existingEvents (view: RoadView) =
            match view.ActiveIncumbency, view.ActiveSnapshotId, view.ActiveAuthorityRevision with
            | None, _, _ -> Error(providerText Path.NoActiveIncumbency Map.empty)
            | Some active, _, _ when active <> incumbencyId -> Error(providerText Path.IncumbencyChanged Map.empty)
            | _, Some expected, _ when expected <> snapshotId -> Error "AuditSnapshotStale"
            | _, _, Some expected when expected <> authorityRevision -> Error "AuthorityRevisionStale"
            | _ -> Ok [ assessment ]

        let eventsResult =
            match
                state
                |> Option.bind (fun current -> Wanxiangshu.Mission.Relay.Fold.view current roadId)
            with
            | None ->
                Ok
                    [ RelayEvent.RoadOpened(
                          roadId,
                          authorityRevision,
                          PhysicalUserMessageId.create (AuthorityRevision.value authorityRevision)
                      )
                      RelayEvent.IncumbencyOpened(incumbencyId, snapshotId, BatonSource.ExistingWorld)
                      assessment ]
            | Some view -> existingEvents view

        eventsResult |> Result.bind RelayTransaction.create

    type private BoundReviewInvocation =
        { Scores: ScoreVector
          ToolCallId: ToolCallId
          ProviderRun: ProviderRunIdentity
          PhysicalUserMessageId: string
          Directory: string
          Journal: AgentJournal }

    type private PreparedAssessment =
        { Bound: BoundReviewInvocation
          SessionId: SessionId
          RoadId: RoadId
          SnapshotId: WorkspaceSnapshotId
          State: RelayState option
          RootAuthorityUserMessageId: string
          AuthorityRevision: AuthorityRevision
          IncumbencyId: IncumbencyId
          AssessmentId: AssessmentId }

    let private boundInvocation (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        args.ExactBoundedIntegers(fields, 0, 10)
        |> Result.bind (fun scoredFields -> scoredFields |> List.map snd |> ScoreVector.tryCreate)
        |> Result.bind (fun scores ->
            match
                context.ToolCallId,
                context.ProviderRunId,
                scope.CurrentPhysicalUserMessage context.SessionId,
                scope.WorkspaceDirectory,
                scope.Journal
            with
            | None, _, _, _, _ -> Error(providerText Path.MissingToolCallId Map.empty)
            | _, None, _, _, _ -> Error(providerText Path.MissingProviderRun Map.empty)
            | _, _, None, _, _ -> Error(providerText Path.MissingUserRootContext Map.empty)
            | _, _, _, None, _ -> Error(providerText Path.MissingWorkspace Map.empty)
            | _, _, _, _, None -> Error(providerText Path.MissingJournal Map.empty)
            | Some toolCallId, Some providerRun, Some physicalUserMessageId, Some directory, Some journal ->
                Ok
                    { Scores = scores
                      ToolCallId = toolCallId
                      ProviderRun = providerRun
                      PhysicalUserMessageId = physicalUserMessageId
                      Directory = directory
                      Journal = journal })

    let private prepareAssessment (scope: ToolRuntimeScope) (context: HostToolContext) (bound: BoundReviewInvocation) =
        let sessionId = SessionId.create context.SessionId
        let roadId = RoadId.create context.SessionId
        let snapshotId = WorkspaceSnapshot.capture bound.Directory
        let state = currentRelayState bound.Journal sessionId

        let view =
            state
            |> Option.bind (fun relay -> Wanxiangshu.Mission.Relay.Fold.view relay roadId)

        scope.ActiveProfileFor sessionId
        |> Option.map (fun profile -> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)
        |> requireSome (providerText Path.MissingAuthorityRoot Map.empty)
        |> Result.map (fun rootAuthorityUserMessageId ->
            let authorityRevision =
                view
                |> Option.map (fun road -> road.AuthorityRevision)
                |> Option.defaultValue (AuthorityRevision.create rootAuthorityUserMessageId)

            let incumbencyId =
                view
                |> Option.bind (fun road -> road.ActiveIncumbency)
                |> Option.defaultWith (fun () ->
                    HostDigest.sha256Hex ("incumbency-v1\n" + context.SessionId + "\n" + rootAuthorityUserMessageId)
                    |> fun digest -> IncumbencyId.create ("incumbency:" + digest))

            let assessmentId =
                HostDigest.sha256Hex ("assessment-v1\n" + context.SessionId + "\n" + ToolCallId.value bound.ToolCallId)
                |> fun digest -> AssessmentId.create ("assessment:" + digest)

            { Bound = bound
              SessionId = sessionId
              RoadId = roadId
              SnapshotId = snapshotId
              State = state
              RootAuthorityUserMessageId = rootAuthorityUserMessageId
              AuthorityRevision = authorityRevision
              IncumbencyId = incumbencyId
              AssessmentId = assessmentId })

    let private phaseName =
        function
        | IncumbencyPhase.AuditPending -> "AuditPending"
        | IncumbencyPhase.WorkOwned -> "WorkOwned"
        | IncumbencyPhase.PerfectAwaitingRetirement -> "PerfectAwaitingRetirement"
        | IncumbencyPhase.RetirementCleanupBlocked -> "RetirementCleanupBlocked"

    let private acceptedResult (prepared: PreparedAssessment) projection =
        let phase =
            AgentProjection.tryFind prepared.SessionId projection.AgentProjections
            |> Option.bind (fun session -> session.Relay)
            |> Option.bind (fun state -> Wanxiangshu.Mission.Relay.Fold.view state prepared.RoadId)
            |> Option.bind (fun view -> view.ActivePhase)
            |> Option.map phaseName
            |> Option.defaultValue "Unknown"

        ToolHostCodec.tomlObjectWithInstructions
            [ providerText Path.Received Map.empty ]
            [ "recorded", ToolHostCodec.TBool true
              "phase", ToolHostCodec.TString phase
              "all_perfect", ToolHostCodec.TBool(ScoreVector.allPerfect prepared.Bound.Scores) ]

    let private appendAssessment (prepared: PreparedAssessment) transaction =
        let fact =
            AgentFact.Relay(
                RelayFactCases.TransactionCommitted
                    {| RoadId = prepared.RoadId
                       Transaction = transaction |}
            )

        task {
            let! outcome =
                AgentJournal.appendAgent
                    (StreamId.Session prepared.SessionId)
                    (Some prepared.Bound.ProviderRun)
                    fact
                    prepared.Bound.Journal

            return outcome |> Result.mapError JournalAppendFailure.describe
        }

    let private runPrepared (scope: ToolRuntimeScope) (context: HostToolContext) (prepared: PreparedAssessment) =
        captureBinding
            scope
            context
            prepared.Bound.PhysicalUserMessageId
            prepared.RootAuthorityUserMessageId
            prepared.Bound.ToolCallId
            prepared.Bound.ProviderRun
        |> bindTaskResult (fun binding ->
            assessmentTransaction
                prepared.State
                prepared.RoadId
                prepared.IncumbencyId
                prepared.AssessmentId
                binding
                prepared.SnapshotId
                prepared.AuthorityRevision
                prepared.Bound.Scores
            |> bindResultTask (fun transaction ->
                appendAssessment prepared transaction
                |> bindTaskResult (fun projection -> Task.FromResult(Ok(acceptedResult prepared projection)))))

    let private renderExecution =
        function
        | Ok value -> value
        | Error error -> rejected error

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let pending =
                boundInvocation scope args context
                |> Result.bind (prepareAssessment scope context)
                |> bindResultTask (runPrepared scope context)

            let! outcome = pending
            return renderExecution outcome
        }

    let admission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.isAllowed role ToolPermission.ReviewAssessment)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        let score field =
            ToolHostCodec.boundedIntegerSchema 0 10 (providerText Path.ScoreArgument (Map [ "field", field ])) factory

        { Name = "review"
          Description = providerText Path.Description Map.empty
          Arguments = fields |> List.map (fun field -> field, score field)
          Admission = admission
          Execute = execute scope }
