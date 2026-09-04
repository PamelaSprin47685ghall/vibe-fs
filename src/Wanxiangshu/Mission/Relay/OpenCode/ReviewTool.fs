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

    let private fields = ScoreDimension.all |> List.map ScoreDimension.fieldName

    let private providerText (path: string) substitutions =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path substitutions

    let private rejected reason =
        ToolHostCodec.tomlObjectWithInstructions
            [ providerText Path.Rejected (Map [ "reason", reason ]) ]
            [ "recorded", ToolHostCodec.TBool false ]

    let private publicNarrativeBefore (toolCallId: ToolCallId) (message: SessionMessage) =
        let expected = ToolCallId.value toolCallId

        let rec loop index texts =
            if index >= message.Parts.Length then
                Error "tool call is absent from its provider message"
            else
                match message.Parts.[index] with
                | MessagePart.ToolCall(callId, name, _) when callId = expected && name = "review" ->
                    let narrative = texts |> List.rev |> String.concat "\n" |> fun text -> text.Trim()

                    if String.IsNullOrWhiteSpace narrative then
                        Error "public assessment narrative before review is empty"
                    else
                        Ok narrative
                | MessagePart.Text text -> loop (index + 1) (text :: texts)
                | MessagePart.Reasoning _
                | MessagePart.ToolCall _
                | MessagePart.ToolResult _
                | MessagePart.Activity _ -> loop (index + 1) texts

        loop 0 []

    let private rootText (physicalUserMessageId: string) (messages: SessionMessage list) =
        match messages |> List.tryFind (fun message -> message.Id = physicalUserMessageId) with
        | None -> Error "current physical user root is unavailable from the transcript"
        | Some message ->
            let text =
                message.Parts
                |> Array.choose (function
                    | MessagePart.Text text -> Some text
                    | _ -> None)
                |> String.concat "\n"
                |> fun value -> value.Trim()

            if String.IsNullOrWhiteSpace text then
                Error "current physical user root is unavailable from the transcript"
            else
                Ok text

    let private captureBinding
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (physicalUserMessageId: string)
        (rootAuthorityUserMessageId: string)
        (toolCallId: ToolCallId)
        (providerRun: ProviderRunIdentity)
        : Task<Result<AssessmentBinding, string>> =
        task {
            match scope.Snapshot with
            | None -> return Error "session snapshot port is unavailable"
            | Some snapshot ->
                let sessionId = SessionId.create context.SessionId

                match! snapshot.GetMessages sessionId with
                | Error error -> return Error("cannot load assessment transcript: " + error)
                | Ok messages ->
                    match SessionSnapshot.locateToolCall toolCallId messages with
                    | Error _ -> return Error "tool call cannot be uniquely located in the transcript"
                    | Ok location when location.ToolName <> "review" ->
                        return Error "located tool call is not review"
                    | Ok location when location.ProviderRun <> providerRun ->
                        return Error "provider run does not match the located review tool call"
                    | Ok location ->
                        let providerMessage =
                            match messages |> List.tryFind (fun message -> message.Id = ProviderRunIdentity.value providerRun) with
                            | Some message -> Ok message
                            | None -> Error "provider message for review is unavailable"

                        match providerMessage, rootText rootAuthorityUserMessageId messages with
                        | Error error, _
                        | _, Error error -> return Error error
                        | Ok message, Ok root ->
                            match publicNarrativeBefore toolCallId message with
                            | Error error -> return Error error
                            | Ok narrative ->
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

                                return
                                    Ok
                                        { PhysicalUserMessageId = physicalUserMessageId
                                          ProviderRunId = ProviderRunIdentity.value providerRun
                                          ToolCallId = ToolCallId.value toolCallId
                                          NarrativeDigest = narrativeDigest
                                          PayloadDigest = payloadDigest
                                          RootRequestDigest = rootDigest
                                          RequirementSetDigest = requirementSetDigest
                                          EvidenceFrontierDigest = evidenceFrontierDigest }
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

        let eventsResult =
            match state |> Option.bind (fun current -> Wanxiangshu.Mission.Relay.Fold.view current roadId) with
            | None ->
                Ok
                    [ RelayEvent.RoadOpened(roadId, authorityRevision)
                      RelayEvent.IncumbencyOpened(incumbencyId, snapshotId, BatonSource.ExistingWorld)
                      assessment ]
            | Some view ->
                match view.ActiveIncumbency, view.ActiveSnapshotId, view.ActiveAuthorityRevision with
                | None, _, _ -> Error "no active incumbency is available for assessment"
                | Some active, _, _ when active <> incumbencyId -> Error "active incumbency identity changed"
                | _, Some expected, _ when expected <> snapshotId -> Error "AuditSnapshotStale"
                | _, _, Some expected when expected <> authorityRevision -> Error "AuthorityRevisionStale"
                | _ -> Ok [ assessment ]

        eventsResult |> Result.bind RelayTransaction.create

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match args.ExactBoundedIntegers(fields, 0, 10) with
            | Error error -> return rejected error
            | Ok scoredFields ->
                let scores = scoredFields |> List.map snd |> ScoreVector.tryCreate

                match scores with
                | Error error -> return rejected error
                | Ok scores ->
                    match
                        context.ToolCallId,
                        context.ProviderRunId,
                        scope.CurrentPhysicalUserMessage context.SessionId,
                        scope.WorkspaceDirectory,
                        scope.Journal
                    with
                    | None, _, _, _, _ -> return rejected "tool call identity is unavailable"
                    | _, None, _, _, _ -> return rejected "provider run identity is unavailable"
                    | _, _, None, _, _ -> return rejected "current physical user root is unavailable"
                    | _, _, _, None, _ -> return rejected "workspace directory is unavailable"
                    | _, _, _, _, None -> return rejected "durable journal is unavailable"
                    | Some toolCallId, Some providerRun, Some physicalUserMessageId, Some directory, Some journal ->
                        let sessionId = SessionId.create context.SessionId
                        let roadId = RoadId.create context.SessionId
                        let snapshotId = WorkspaceSnapshot.capture directory
                        let state = currentRelayState journal sessionId
                        let view = state |> Option.bind (fun relay -> Wanxiangshu.Mission.Relay.Fold.view relay roadId)

                        let rootAuthorityUserMessageId =
                            scope.ActiveProfileFor sessionId
                            |> Option.map (fun profile ->
                                AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)

                        match rootAuthorityUserMessageId with
                        | None -> return rejected "root authority identity is unavailable"
                        | Some rootAuthorityUserMessageId ->
                            let authorityRevision =
                                view
                                |> Option.map (fun road -> road.AuthorityRevision)
                                |> Option.defaultValue (AuthorityRevision.create rootAuthorityUserMessageId)

                            let incumbencyId =
                                view
                                |> Option.bind (fun road -> road.ActiveIncumbency)
                                |> Option.defaultWith (fun () ->
                                    HostDigest.sha256Hex (
                                        "incumbency-v1\n" + context.SessionId + "\n" + rootAuthorityUserMessageId
                                    )
                                    |> fun digest -> IncumbencyId.create ("incumbency:" + digest))

                            let assessmentId =
                                HostDigest.sha256Hex (
                                    "assessment-v1\n" + context.SessionId + "\n" + ToolCallId.value toolCallId
                                )
                                |> fun digest -> AssessmentId.create ("assessment:" + digest)

                            match!
                                captureBinding
                                    scope
                                    context
                                    physicalUserMessageId
                                    rootAuthorityUserMessageId
                                    toolCallId
                                    providerRun
                            with
                            | Error error -> return rejected error
                            | Ok binding ->
                                match
                                    assessmentTransaction
                                        state
                                        roadId
                                        incumbencyId
                                        assessmentId
                                        binding
                                        snapshotId
                                        authorityRevision
                                        scores
                                with
                                | Error error -> return rejected error
                                | Ok transaction ->
                                    let fact =
                                        AgentFact.Relay(
                                            RelayFactCases.TransactionCommitted
                                                {| RoadId = roadId
                                                   Transaction = transaction |}
                                        )

                                    match!
                                        AgentJournal.appendAgent
                                            (StreamId.Session sessionId)
                                            (Some providerRun)
                                            fact
                                            journal
                                    with
                                    | Error failure -> return rejected (JournalAppendFailure.describe failure)
                                    | Ok projection ->
                                        let phase =
                                            AgentProjection.tryFind sessionId projection.AgentProjections
                                            |> Option.bind (fun session -> session.Relay)
                                            |> Option.bind (fun state -> Wanxiangshu.Mission.Relay.Fold.view state roadId)
                                            |> Option.bind (fun view -> view.ActivePhase)
                                            |> Option.map (function
                                                | IncumbencyPhase.AuditPending -> "AuditPending"
                                                | IncumbencyPhase.WorkOwned -> "WorkOwned"
                                                | IncumbencyPhase.PerfectAwaitingRetirement -> "PerfectAwaitingRetirement"
                                                | IncumbencyPhase.RetirementCleanupBlocked -> "RetirementCleanupBlocked")
                                            |> Option.defaultValue "Unknown"

                                    return
                                        ToolHostCodec.tomlObjectWithInstructions
                                            [ providerText Path.Received Map.empty ]
                                            [ "recorded", ToolHostCodec.TBool true
                                              "phase", ToolHostCodec.TString phase
                                              "all_perfect", ToolHostCodec.TBool(ScoreVector.allPerfect scores) ]
        }

    let admission =
        ToolAdmission.OfficeRole(fun _ role ->
            OfficeCapability.isAllowed role ToolPermission.ReviewAssessment)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        let score field =
            ToolHostCodec.boundedIntegerSchema
                0
                10
                (field + " quality score; integer 0 through 10")
                factory

        { Name = "review"
          Description = providerText Path.Description Map.empty
          Arguments = fields |> List.map (fun field -> field, score field)
          Admission = admission
          Execute = execute scope }

