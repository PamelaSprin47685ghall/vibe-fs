namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

module XWire =

    let private sessionIdOfOutput (output: obj) : SessionId option =
        Projection.projectionSessionIdFromMessages output |> Option.map SessionId.create

    let private sessionProjection (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections

    let private isCompanionSession (journal: AgentJournal) (sessionId: SessionId) =
        SessionAssociationProjection.isCompanion sessionId (AgentJournal.snapshot journal).AgentProjections.Associations

    let private readFrameBodies (journal: AgentJournal) (frames: BlogFrame list) : Result<string list, string> =
        let rec loop remaining collected =
            match remaining with
            | [] -> Ok(List.rev collected)
            | frame :: tail ->
                journal.Writer.BlobWriter.Read frame.TextRef
                |> Result.bind (fun text ->
                    if HostDigest.sha256Hex text = BlobDigest.value frame.Digest then
                        loop tail (text :: collected)
                    else
                        Error(sprintf "Companion blob digest mismatch: %s" (BlobDigest.value frame.Digest)))

        loop frames []

    let private readFrames (journal: AgentJournal) (frames: BlogFrame list) : Result<string, string> =
        readFrameBodies journal frames
        |> Result.map (fun bodies -> String.concat "\n\n" bodies)

    let private requestStartCutoff (physical: PhysicalUserMessageId) (rawMessages: obj list) =
        rawMessages
        |> List.tryFindIndex (fun raw -> Projection.hostMessageId raw = Some(PhysicalUserMessageId.value physical))
        |> Option.defaultWith (fun () ->
            raise (InvalidOperationException "X-wire cannot bind the physical user message to the transform snapshot"))

    let private rawWithPrefix (rawMessages: obj list) (plan: XPrefixPlan) =
        match plan.CompanionMemory with
        | None -> rawMessages
        | Some(syntheticId, memory) -> Projection.prependCompanionMemory rawMessages syntheticId memory plan.DropLeading

    /// COMPANION-009 / CTX-011: FrozenRecordPrefix = Opening + coverable Y frame
    /// prefix. RawGap never participates — it has no Y coverage proof.
    let private materializeFrozenRecordPrefix
        (journal: AgentJournal)
        (state: SessionAgentProjection)
        (frames: BlogFrame list)
        : Result<string, string> =
        match readFrameBodies journal frames with
        | Error reason -> Error reason
        | Ok frameBodies ->
            let opening =
                state.XTrace
                |> Option.bind (fun trace -> trace.Opening)
                |> Option.defaultValue
                    { AssignmentText = ""
                      AuthoritativeRequirements = [] }

            // Opening + frames only. Gap/terminal are live X material and must not
            // enter a frozen replacement (COMPANION-009).
            // COMPANION-009: FrozenRecordPrefix is same-session X memory, not a
            // parent/child hand-off. Opening stays (includeOpening=true).
            Ok(
                LifecycleWorkRecord.render
                    true
                    { Opening = opening
                      Frames = frameBodies
                      Gap = []
                      Terminal = None }
            )

    let private candidate
        (journal: AgentJournal)
        (sessionId: SessionId)
        (current: ProviderSemanticProjection)
        (state: SessionAgentProjection)
        (requestCutoff: int)
        : Result<PrefixProbe, NoCandidateReason> =
        let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty
        let blog = state.Blog |> Option.defaultValue BlogProjection.empty
        let frames = BlogProjection.coverableFrames blog

        match materializeFrozenRecordPrefix journal state frames with
        | Error reason -> raise (InvalidOperationException reason)
        | Ok frozenRecordPrefix ->
            match journal.WriteBlob frozenRecordPrefix with
            | Error reason -> raise (InvalidOperationException reason)
            | Ok blob ->
                PrefixProbeSelection.select
                    HostDigest.sha256Hex
                    sessionId
                    prefix.EpochId
                    (prefix.Snapshot)
                    blog.Coverage.CoverableTurnCutoffExclusive
                    blog.Coverage.CoveredPrefixDigest
                    requestCutoff
                    blob.BlobRef
                    blob.BlobDigest
                    (fun cutoff ->
                        current.Messages
                        |> List.truncate cutoff
                        |> fun messages -> ProviderProjection.renderSemantic { current with Messages = messages }
                        |> HostDigest.sha256Hex)

    let applyTransform
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (output: obj)
        : Task<unit> =
        task {
            match journal, sessionIdOfOutput output with
            | Some durable, Some sessionId when not (isCompanionSession durable sessionId) ->
                match scope.TryRecoveryArming sessionId with
                | None -> return ()
                | Some _ ->
                    let rawMessages = Projection.messagesFromTransformOutput output
                    let physical = Projection.lastUserMessageId rawMessages

                    match physical, snapshot with
                    | None, _ ->
                        raise (InvalidOperationException "X-wire cannot plan a retry without a physical user message")
                    | _, None ->
                        raise (
                            InvalidOperationException "X-wire cannot plan a retry without the public session snapshot"
                        )
                    | Some physical, Some snapshotPort ->
                        let! snapshotResult = snapshotPort.GetMessages sessionId

                        match snapshotResult with
                        | Error reason -> raise (InvalidOperationException reason)
                        | Ok messages ->
                            match ReviewSeal.bindableRun (PhysicalUserMessageId.value physical) messages with
                            | Error rejection ->
                                raise (InvalidOperationException(sprintf "X-wire run binding failed: %A" rejection))
                            | Ok assistant ->
                                let providerRun = ProviderRunIdentity.create assistant.Id
                                let projections = AgentJournal.snapshot durable

                                match
                                    PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections,
                                    DurableFallback.tryCurrentState sessionId projections,
                                    sessionProjection durable sessionId
                                with
                                | Some authority, Some fallback, Some state ->
                                    let current =
                                        Projection.decodeMessageView rawMessages |> ProviderProjection.toSemantic

                                    let cutoff = requestStartCutoff physical rawMessages
                                    let blog = state.Blog |> Option.defaultValue BlogProjection.empty
                                    let arming = scope.TryRecoveryArming sessionId |> Option.get

                                    let mayRecover =
                                        RecoverySlot.mayRecover
                                            arming
                                            fallback.Cursor.Offset
                                            (BlogProjection.hasCoverage blog)

                                    let selectedProbe = ref None

                                    let selectProbe () =
                                        let selected = candidate durable sessionId current state cutoff

                                        match selected with
                                        | Ok probe ->
                                            selectedProbe.Value <- Some probe
                                            Ok probe
                                        | Error reason -> Error reason

                                    let plan =
                                        AttemptPlanner.plan
                                            authority
                                            fallback.Cursor
                                            physical
                                            providerRun
                                            (PromptAuthority.PromptOrigin.Continuation
                                                PromptAuthority.ContinuationKind.ProviderRetryAttempt)
                                            ProviderRequestKind.WorkMain
                                            mayRecover
                                            selectProbe

                                    let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty

                                    let frozenRecordPrefixBody =
                                        match plan.Profile.ProjectionChoice with
                                        | XProjectionChoice.UseCommittedEpoch ->
                                            match prefix.Snapshot with
                                            | None -> ""
                                            | Some committed ->
                                                match
                                                    durable.Writer.BlobWriter.Read committed.FrozenRecordPrefixRef
                                                with
                                                | Ok body -> body
                                                | Error reason -> raise (InvalidOperationException reason)
                                        | XProjectionChoice.UsePrefixProbe _ ->
                                            match selectedProbe.Value with
                                            | Some probe ->
                                                match
                                                    durable.Writer.BlobWriter.Read probe.Candidate.FrozenRecordPrefixRef
                                                with
                                                | Ok body -> body
                                                | Error reason -> raise (InvalidOperationException reason)
                                            | None ->
                                                raise (
                                                    InvalidOperationException
                                                        "X-wire planner selected a probe without a materialised candidate"
                                                )

                                    let prefixPlan =
                                        XPrefixProjection.forChoice
                                            plan.Profile.ProjectionChoice
                                            prefix.Snapshot
                                            frozenRecordPrefixBody

                                    let transformed = rawWithPrefix rawMessages prefixPlan

                                    Wanxiangshu.Session.HostMessageProjection.replaceMessagesInPlace output transformed

                                    scope.RecordAttemptPlan sessionId providerRun plan
                                    scope.ClearRecovery sessionId
                                | _ ->
                                    raise (
                                        InvalidOperationException
                                            "X-wire cannot plan a retry without authority, fallback, and session projections"
                                    )
            | _ -> return ()
        }

    let reconcileAttempt (journal: AgentJournal option) (scope: PluginRuntimeScope) (turn: ReconciledTurn) : unit =
        match journal, scope.TryAttemptPlan turn.SessionId turn.ProviderRun with
        | Some durable, Some plan ->
            let outcome =
                match turn.Outcome with
                | ReconcileProgram.TurnCompleted -> AttemptOutcome.Completed
                | ReconcileProgram.TurnFailed _ -> AttemptOutcome.Failed
                | ReconcileProgram.TurnAborted _ -> AttemptOutcome.Aborted
                | ReconcileProgram.TurnNeedsContinuation _
                | ReconcileProgram.TurnInProgress
                | ReconcileProgram.TurnUnknown -> AttemptOutcome.CompletedInvalid

            match AttemptPlanner.promotableProbe plan outcome with
            | Some probe ->
                let projections = AgentJournal.snapshot durable

                let epoch =
                    AgentProjection.tryFind turn.SessionId projections.AgentProjections
                    |> Option.bind (fun state -> state.PrefixEpoch)
                    |> Option.defaultValue PrefixEpochProjection.empty

                let fact =
                    ContextFact.PrefixRebaseCommitted
                        {| SessionId = turn.SessionId
                           PreviousEpochId = probe.BasedOnEpochId
                           NextEpochId = PrefixEpochId.next probe.BasedOnEpochId
                           FrozenRecordPrefixRef = probe.Candidate.FrozenRecordPrefixRef
                           FrozenRecordPrefixDigest = probe.Candidate.FrozenRecordPrefixDigest
                           CutoffExclusive = probe.Candidate.CutoffExclusive
                           CoveredPrefixDigest = probe.Candidate.CoveredPrefixDigest
                           SealRoot = probe.Candidate.SealRoot
                           SyntheticMessageId = probe.Candidate.SyntheticMessageId
                           ProbeId = probe.ProbeId
                           SolvingProviderRun = turn.ProviderRun |}

                if epoch.EpochId = probe.BasedOnEpochId then
                    AgentJournal.appendAgent (StreamId.Session turn.SessionId) (Some turn.ProviderRun) fact durable
                    |> ignore
            | None -> ()

            scope.ClearAttemptPlan turn.SessionId turn.ProviderRun
        | _ -> ()
