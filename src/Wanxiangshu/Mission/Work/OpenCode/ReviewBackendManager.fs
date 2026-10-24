namespace Wanxiangshu.Mission.Work.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Ports
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Mission.Work
open Wanxiangshu.Mission.Work.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// work-038: the Manager backend of the shared `review` shell.
/// Owns the review binding, transaction and failure paths; the
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

    /// The reviewer is never the incumbency owner. A manager work opens with
    /// the session id as owner, so the reviewer must be a distinct identity;
    /// the kernel refuses an owner reviewing its own incumbency.
    [<Literal>]
    let private ReviewerId = "manager-reviewer"

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
        ToolResultText.tomlObjectWithInstructions
            [ providerText (failurePath failure) Map.empty ]
            [ "recorded", ToolResultText.TBool false ]

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
        : Task<Result<ReviewBinding, ReviewFailure>> =
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

    /// The one lifecycle work this session owns. It is independent of the
    /// workspace EventStore; only the git common directory selects its journal.
    let private lifecycleWorkOf (directory: string) (sessionId: string) : Task<DurableWorkHost.InspectedWork option> =
        task {
            let! commonDir = RuntimePath.stateRoot directory

            if String.IsNullOrWhiteSpace commonDir then
                return None
            else
                return DurableWorkHost.inspected commonDir sessionId
        }

    type private BoundReviewInvocation =
        { Findings: ReviewFindings
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

    let private resolveAssessment
        (scope: ToolRuntimeScope)
        (sessionId: SessionId)
        (directorySessionId: string)
        (bound: BoundReviewInvocation)
        : Task<Result<PreparedAssessment, ReviewFailure>> =
        task {
            let! workOpt = lifecycleWorkOf bound.Directory directorySessionId

            return
                workOpt
                |> requireSome BindingUnavailable
                |> Result.bind (fun work ->
                    match work.ActiveId with
                    | None -> Error BindingUnavailable
                    | Some activeId ->
                        scope.ActiveProfileFor sessionId
                        |> Option.map (fun profile ->
                            AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)
                        |> requireSome BindingUnavailable
                        |> Result.map (fun rootAuthorityUserMessageId ->
                            { Bound = bound
                              SessionId = sessionId
                              RootAuthorityUserMessageId = rootAuthorityUserMessageId
                              IncumbencyId = activeId }))
        }

    let private prepareAssessment
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (bound: BoundReviewInvocation)
        : Task<Result<PreparedAssessment, ReviewFailure>> =
        task {
            let sessionId = SessionId.create context.SessionId

            try
                return! resolveAssessment scope sessionId context.SessionId bound
            with _ ->
                return Error BindingUnavailable
        }

    let private acceptedResult (receipt: DurableWorkHost.HostReceipt) =
        let outcome = DurableWorkHost.resultText receipt "outcome"

        let firstReview = DurableWorkHost.resultText receipt "firstReview"

        let instructionPath = if outcome = "Promoted" then Path.Finish else Path.Work

        ToolResultText.tomlObjectWithInstructions
            [ providerText instructionPath Map.empty ]
            [ "recorded", ToolResultText.TBool true
              "outcome", ToolResultText.TString outcome
              "first_review", ToolResultText.TString firstReview ]

    /// The write receipt decides the result. Multiple reviews per tenure are
    /// legal; a committed or replayed write is accepted.
    let private reviewOutcome (receipt: DurableWorkHost.HostReceipt) : Result<string, ReviewFailure> =
        if receipt.Status = "Committed" || receipt.Status = "Replayed" then
            Ok(acceptedResult receipt)
        else
            Error RecordFailed

    /// The review member's failure envelope: a stable code plus a localized
    /// reason. Data path only — the standalone tool renders TOML instead.
    let private memberFailure (failure: ReviewFailure) : obj =
        createObj
            [ "ok" ==> false
              "code" ==> failurePath failure
              "reason" ==> providerText (failurePath failure) Map.empty ]

    let private boolField (result: obj) (name: string) : bool =
        let field = if isNull result then null else result?(name)
        not (isNull field) && emitJsExpr field "!!$0"

    let private obligationsField (result: obj) : obj array =
        let obligations = if isNull result then null else result?obligations

        if isNull obligations || not (emitJsExpr obligations "Array.isArray($0)") then
            [||]
        else
            unbox<obj array> obligations

    /// The review member's success envelope: outcome, firstReview and the
    /// remaining keyed obligation ledger, all read from the durable receipt.
    let private memberAccepted (receipt: DurableWorkHost.HostReceipt) : obj =
        createObj
            [ "ok" ==> true
              "outcome" ==> DurableWorkHost.resultText receipt "outcome"
              "firstReview" ==> boolField receipt.Result "firstReview"
              "obligations" ==> obligationsField receipt.Result ]

    /// Keyed nullable updates: a null value deletes the key (the kernel folds
    /// a null into a removal); an empty string is NOT a deletion alias.
    let private updatesOf (findings: ReviewFindings) : obj =
        findings
        |> ReviewFindings.values
        |> List.map (fun finding ->
            createObj
                [ "key", box finding.Key
                  "value", finding.Value |> Option.map box |> Option.toObj ])
        |> List.toArray
        |> box

    let private runRecorded
        (context: HostToolContext)
        (prepared: PreparedAssessment)
        : Task<Result<string, ReviewFailure>> =
        task {
            match! lifecycleWorkOf prepared.Bound.Directory context.SessionId with
            | None -> return Error RecordFailed
            | Some _ ->
                let! gitCommonDir = RuntimePath.stateRoot prepared.Bound.Directory

                // The request identity is the exact invocation fence, not the
                // incumbency alone and not a content hash: two distinct calls
                // with identical findings are two distinct review facts, while
                // an exact retry replays.
                let requestId =
                    ProviderRunIdentity.value prepared.Bound.ProviderRun
                    + ":"
                    + ToolCallId.value prepared.Bound.ToolCallId

                let updates =
                    prepared.Bound.Findings
                    |> ReviewFindings.values
                    |> List.map (fun finding ->
                        createObj
                            [ "key", box finding.Key
                              "value", finding.Value |> Option.map box |> Option.toObj ])
                    |> List.toArray
                    |> box

                let command =
                    DurableWorkHost.command
                        "Review"
                        [ "incumbencyId", box prepared.IncumbencyId
                          "reviewerId", box ReviewerId
                          "updates", updates ]

                let receipt =
                    DurableWorkHost.execute gitCommonDir context.SessionId requestId command

                return reviewOutcome receipt
        }

    /// The one review write: send `Review` to the wanxiangli work owner. The
    /// binding is still validated first (a rejected invocation writes nothing
    /// and consumes no slot), but its eight fields do
    /// not enter the event.
    let private afterValidatedReviewBinding (scope: ToolRuntimeScope) (context: HostToolContext) (prepared: PreparedAssessment) =
        task {
            let empty = ReviewFindings.isEmpty prepared.Bound.Findings
            let! kickoff = scope.TryAskForInitialEmptyManagerReview(context, empty)
            match kickoff with
            | Error _ -> return Error RecordFailed
            | Ok true ->
                return Ok "已向用户询问具体任务与验收标准。询问已经登记；等待用户的真实答复后继续本次工作。"
            | Ok false -> return! runRecorded context prepared
        }

    let private runPrepared
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (prepared: PreparedAssessment)
        : Task<Result<string, ReviewFailure>> =
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
            | Ok _ -> return! afterValidatedReviewBinding scope context prepared
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

    let private receiptMemberOutcome (receipt: DurableWorkHost.HostReceipt) : obj =
        if receipt.Status = "Committed" || receipt.Status = "Replayed" then
            memberAccepted receipt
        else
            memberFailure RecordFailed

    let private recordMemberCommand
        (directory: string)
        (sessionId: string)
        (incumbencyId: string)
        (requestId: string)
        (findings: ReviewFindings)
        : Task<obj> =
        task {
            let! gitCommonDir = RuntimePath.stateRoot directory

            if String.IsNullOrWhiteSpace gitCommonDir then
                return memberFailure RecordFailed
            else
                let command =
                    DurableWorkHost.command
                        "Review"
                        [ "incumbencyId", box incumbencyId
                          "reviewerId", box ReviewerId
                          "updates", updatesOf findings ]

                let receipt = DurableWorkHost.execute gitCommonDir sessionId requestId command

                return receiptMemberOutcome receipt
        }

    let private resolveMemberIncumbency (directory: string) (sessionId: string) : Task<string option> =
        task {
            let! workOpt = lifecycleWorkOf directory sessionId
            return workOpt |> Option.bind (fun work -> work.ActiveId)
        }

    let private executeInDirectory
        (directory: string)
        (sessionId: string)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        (findings: ReviewFindings)
        : Task<obj> =
        task {
            match! resolveMemberIncumbency directory sessionId with
            | Some incumbencyId ->
                let requestId =
                    ProviderRunIdentity.value providerRun + ":" + ToolCallId.value toolCallId

                return! recordMemberCommand directory sessionId incumbencyId requestId findings
            | None -> return memberFailure BindingUnavailable
        }

    let private executeParsedMember
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (findings: ReviewFindings)
        : Task<obj> =
        match context.ToolCallId, context.ProviderRunId, scope.WorkspaceDirectory with
        | Some toolCallId, Some providerRun, Some directory ->
            let (Ports.WorkspacePath _ws) = Ports.WorkspacePath directory
            executeInDirectory directory context.SessionId providerRun toolCallId findings
        | _ -> Task.FromResult(memberFailure BindingUnavailable)

    /// The `this.review(findings)` member path. `findings` is a plain JS array
    /// of `{ key: string, value: string | null }`; a null value deletes the
    /// key. Returns a data envelope (`{ ok, outcome, firstReview, obligations }`
    /// or `{ ok: false, code, reason }`), never rendered prose.
    let private executeParsedMemberOrAsk (scope: ToolRuntimeScope) (context: HostToolContext) (findings: ReviewFindings) =
        task {
            let! kickoff = scope.TryAskForInitialEmptyManagerReview(context, ReviewFindings.isEmpty findings)
            match kickoff with
            | Error error -> return createObj [ "ok" ==> false; "code" ==> "review-question-failed"; "reason" ==> error ]
            | Ok true ->
                return createObj [ "ok" ==> true; "outcome" ==> "WaitingForUser"; "firstReview" ==> false; "awaitingUser" ==> true; "obligations" ==> box [||] ]
            | Ok false -> return! executeParsedMember scope context findings
        }

    let executeMember (scope: ToolRuntimeScope) (context: HostToolContext) (findings: obj) : Task<obj> =
        task {
            let parsed =
                if isNull findings then
                    Error InvalidFindings
                else
                    Model.tryParse (createObj [ "findings" ==> findings ])
                    |> Result.mapError (fun _ -> InvalidFindings)

            match parsed with
            | Error failure -> return memberFailure failure
            | Ok findings -> return! executeParsedMemberOrAsk scope context findings
        }
