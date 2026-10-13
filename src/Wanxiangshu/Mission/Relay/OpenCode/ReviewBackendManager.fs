namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git
open Wanxiangshu.Host
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Mission.Relay.Assessment
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

/// relay-assessment-011: the Manager backend of the shared `review` shell.
/// Owns the relay assessment binding, transaction and failure paths; the
/// shell (ReviewShell) dispatches to it for every non-Plan caller. The logic
/// below is the former ReviewTool body, moved without behavioral change.
module ReviewBackendManager =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Finish = "runtime/manager-finish"

        [<Literal>]
        let Work = "runtime/manager-work"

        [<Literal>]
        let BindingUnavailable = "tool/review/binding-unavailable"

        [<Literal>]
        let MissingRequest = "tool/review/missing-request"

        [<Literal>]
        let InvalidFindings = "tool/review/invalid-findings"

        [<Literal>]
        let RecordFailed = "tool/review/record-failed"

        [<Literal>]
        let ReplayConflict = "tool/review/replay-conflict"

        [<Literal>]
        let AlreadySubmitted = "tool/review/already-submitted"

    type private ReviewFailure =
        | BindingUnavailable
        | MissingRequest
        | InvalidFindings
        | RecordFailed
        | ReplayConflict
        | AlreadySubmitted

    let private failurePath =
        function
        | BindingUnavailable -> Path.BindingUnavailable
        | MissingRequest -> Path.MissingRequest
        | InvalidFindings -> Path.InvalidFindings
        | RecordFailed -> Path.RecordFailed
        | ReplayConflict -> Path.ReplayConflict
        | AlreadySubmitted -> Path.AlreadySubmitted

    let private providerText (path: string) substitutions =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path substitutions

    let private rejectedResult failure =
        ToolHostCodec.tomlObjectWithInstructions
            [ providerText (failurePath failure) Map.empty ]
            [ "recorded", ToolHostCodec.TBool false ]

    let private requireSome error =
        function
        | Some value -> Ok value
        | None -> Error error

    let private narrativeOf texts =
        texts |> List.rev |> String.concat "\n" |> (fun text -> text.Trim()) |> Ok

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
                Error BindingUnavailable
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
            Error MissingRequest
        else
            Ok text

    let private rootText (physicalUserMessageId: string) (messages: SessionMessage list) =
        match messages |> List.tryFind (fun message -> message.Id = physicalUserMessageId) with
        | None -> Error MissingRequest
        | Some message -> message |> messageText |> requireRootText

    let private validateLocation providerRun (location: SessionSnapshot.ToolCallLocation) =
        if location.ToolName <> "review" then
            Error BindingUnavailable
        elif location.ProviderRun <> providerRun then
            Error BindingUnavailable
        else
            Ok location

    let private bindingFromMessages physicalUserMessageId rootAuthorityUserMessageId toolCallId providerRun messages =
        SessionSnapshot.locateToolCall toolCallId messages
        |> Result.mapError (fun _ -> BindingUnavailable)
        |> Result.bind (validateLocation providerRun)
        |> Result.bind (fun location ->
            messages
            |> List.tryFind (fun message -> message.Id = ProviderRunIdentity.value providerRun)
            |> requireSome BindingUnavailable
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
        : Task<Result<AssessmentBinding, ReviewFailure>> =
        match scope.Snapshot with
        | None -> Task.FromResult(Error BindingUnavailable)
        | Some snapshot ->
            task {
                let! loaded = snapshot.GetMessages(SessionId.create context.SessionId)

                return
                    loaded
                    |> Result.mapError (fun _ -> BindingUnavailable)
                    |> Result.bind (
                        bindingFromMessages physicalUserMessageId rootAuthorityUserMessageId toolCallId providerRun
                    )
            }

    /// The one lifecycle work this session owns, read from the same workspace
    /// EventStore the plugin writes. No relay fact, no certificate.
    let private lifecycleWorkOf (directory: string) (sessionId: string) : (IEventStore * LifecycleWork) option =
        let commonDir = RuntimePath.gitCommonDir directory

        if String.IsNullOrWhiteSpace commonDir then
            None
        else
            match WorkspaceEventStore.tryCurrent commonDir with
            | None -> None
            | Some held ->
                match held.TryCurrent "Lifecycle" with
                | None -> None
                | Some current ->
                    let works = unbox<Map<string, LifecycleWork>> current
                    Map.tryFind sessionId works |> Option.map (fun work -> held, work)

    type private BoundReviewInvocation =
        { Findings: AssessmentFindings
          ToolCallId: ToolCallId
          ProviderRun: ProviderRunIdentity
          PhysicalUserMessageId: string
          Directory: string
          Journal: AgentJournal }

    type private PreparedAssessment =
        { Bound: BoundReviewInvocation
          SessionId: SessionId
          RootAuthorityUserMessageId: string
          IncumbencyId: string }

    let private boundInvocation (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        Model.tryParse args.Raw
        |> Result.mapError (fun _ -> InvalidFindings)
        |> Result.bind (fun findings ->
            match
                context.ToolCallId,
                context.ProviderRunId,
                scope.CurrentPhysicalUserMessage context.SessionId,
                scope.WorkspaceDirectory,
                scope.Journal
            with
            | None, _, _, _, _ -> Error BindingUnavailable
            | _, None, _, _, _ -> Error BindingUnavailable
            | _, _, None, _, _ -> Error MissingRequest
            | _, _, _, None, _ -> Error BindingUnavailable
            | _, _, _, _, None -> Error BindingUnavailable
            | Some toolCallId, Some providerRun, Some physicalUserMessageId, Some directory, Some journal ->
                Ok
                    { Findings = findings
                      ToolCallId = toolCallId
                      ProviderRun = providerRun
                      PhysicalUserMessageId = physicalUserMessageId
                      Directory = directory
                      Journal = journal })

    let private prepareAssessment (scope: ToolRuntimeScope) (context: HostToolContext) (bound: BoundReviewInvocation) =
        let sessionId = SessionId.create context.SessionId

        try
            match lifecycleWorkOf bound.Directory context.SessionId with
            | None -> Error BindingUnavailable
            | Some(_, work) ->
                match work.Active with
                | None -> Error BindingUnavailable
                | Some active when active.Review.IsSome -> Error AlreadySubmitted
                | Some active ->
                    scope.ActiveProfileFor sessionId
                    |> Option.map (fun profile ->
                        AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)
                    |> requireSome BindingUnavailable
                    |> Result.map (fun rootAuthorityUserMessageId ->
                        { Bound = bound
                          SessionId = sessionId
                          RootAuthorityUserMessageId = rootAuthorityUserMessageId
                          IncumbencyId = active.Id })
        with _ ->
            Error BindingUnavailable

    let private acceptedResult (findings: AssessmentFindings) =
        let instructionPath =
            if AssessmentFindings.isEmpty findings then
                Path.Finish
            else
                Path.Work

        let substitutions =
            if instructionPath = Path.Finish then
                // The review just recorded, so no confirmation is durable yet.
                Map [ "closeout", Wanxiangshu.Mission.Manager.ManagerWorkflow.finishCloseoutText false ]
            else
                Map.empty

        ToolHostCodec.tomlObjectWithInstructions
            [ providerText instructionPath substitutions ]
            [ "recorded", ToolHostCodec.TBool true ]

    /// The one review write: append `Reviewed` through the unified kernel. The
    /// binding is still validated first (relay-assessment-007: a rejected
    /// invocation writes nothing and consumes no slot), but its eight fields do
    /// not enter the event.
    let private runPrepared
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedAssessment)
        =
        task {
            let! binding =
                captureBinding
                    scope
                    context
                    prepared.Bound.PhysicalUserMessageId
                    prepared.RootAuthorityUserMessageId
                    prepared.Bound.ToolCallId
                    prepared.Bound.ProviderRun

            match binding with
            | Error failure -> return Error failure
            | Ok _ ->
                let outcome =
                    if AssessmentFindings.isEmpty prepared.Bound.Findings then
                        ReviewOutcome.Promoted
                    else
                        ReviewOutcome.Stayed

                match lifecycleWorkOf prepared.Bound.Directory context.SessionId with
                | None -> return Error RecordFailed
                | Some(held, work) ->
                    match LifecycleKernel.review work prepared.IncumbencyId outcome with
                    | KernelDecision.Refuse _ -> return Error RecordFailed
                    | KernelDecision.Done _ -> return Ok(acceptedResult prepared.Bound.Findings)
                    | KernelDecision.Write(events, _) ->
                        match! LifecycleKernel.commit held context.SessionId events with
                        | Ok CommitOutcome.AppendDurable -> return Ok(acceptedResult prepared.Bound.Findings)
                        | Ok CommitOutcome.AppendNotAttempted
                        | Ok CommitOutcome.CommitUnknown -> return Error RecordFailed
                        | Error _ -> return Error RecordFailed
        }

    let private renderExecution =
        function
        | Ok value -> value
        | Error failure -> rejectedResult failure

    let execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome =
                taskResult {
                    let! bound = boundInvocation scope args context
                    let! plan = prepareAssessment scope context bound
                    return! runPrepared scope context plan
                }

            return renderExecution outcome
        }
