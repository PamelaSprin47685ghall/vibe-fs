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
open Wanxiangshu.Recovery
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
        (snapshot: ProjectionSnapshot)
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
                    snapshot.CommittedPrefix
                    blog.Coverage.CoverableTurnCutoffExclusive
                    blog.Coverage.CoveredPrefixDigest
                    requestCutoff
                    blob.BlobRef
                    blob.BlobDigest
                    (ProjectionRenderer.cutoffDigest HostDigest.sha256Hex snapshot)

    let private applyStrengthReplicaPlan
        (snapshotPort: ISessionSnapshotPort)
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (binding: StrengthReplicaBinding)
        (output: obj)
        : Task<unit> =
        task {
            let rawMessages = Projection.messagesFromTransformOutput output

            match Projection.lastUserMessageId rawMessages with
            | None -> raise (InvalidOperationException "StrengthReplica request has no physical user message")
            | Some physical ->
                let! snapshotResult = snapshotPort.GetMessages sessionId

                match snapshotResult with
                | Error reason -> raise (InvalidOperationException reason)
                | Ok messages ->
                    let bindingDiagnostic =
                        messages
                        |> List.map (fun message ->
                            sprintf
                                "%s:%b:%b"
                                message.Role
                                message.Completed
                                (message.ParentId = Some(PhysicalUserMessageId.value physical)))
                        |> String.concat ","

                    Diagnostic.emit
                        "strength-replica-bind-snapshot"
                        [ "session_id", SessionId.value sessionId; "result", bindingDiagnostic ]

                    match ReviewSeal.bindableRun (PhysicalUserMessageId.value physical) messages with
                    | Error rejection ->
                        raise (InvalidOperationException(sprintf "StrengthReplica run binding failed: %A" rejection))
                    | Ok assistant ->
                        let providerRun = ProviderRunIdentity.create assistant.Id
                        let projections = AgentJournal.snapshot durable

                        match PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections with
                        | None -> raise (InvalidOperationException "StrengthReplica has no active Authority Root")
                        | Some authority when authority.CanonicalRole <> binding.CanonicalRole ->
                            raise (
                                InvalidOperationException "StrengthReplica Authority Root role changed after binding"
                            )
                        | Some authority ->
                            let plan =
                                AttemptPlanner.plan
                                    authority
                                    AgentPairCursor.initial
                                    physical
                                    providerRun
                                    (PromptAuthority.PromptOrigin.AuthorityRoot
                                        PromptAuthority.RootAuthorityKind.AgentOwnerRoot)
                                    ProviderRequestKind.StrengthReplica
                                    false
                                    (fun () -> Error NoCandidateReason.NoCoverage)

                            if plan.Profile.ToolCapabilitySet <> binding.ToolCapabilitySet then
                                raise (
                                    InvalidOperationException
                                        "StrengthReplica PromptAuthority capabilities disagree with live execution gate"
                                )

                            scope.RecordAttemptPlan sessionId providerRun plan
        }

    let applyTransform
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (output: obj)
        : Task<unit> =
        task {
            match journal, sessionIdOfOutput output with
            | Some durable, Some sessionId when not (isCompanionSession durable sessionId) ->
                let replicaBinding = scope.StrengthRuntime.TryFindByReplica sessionId

                match replicaBinding with
                | Some binding ->
                    match snapshot with
                    | Some snapshotPort ->
                        do! applyStrengthReplicaPlan snapshotPort durable scope sessionId binding output
                        return ()
                    | None ->
                        raise (
                            InvalidOperationException "StrengthReplica cannot plan without the public session snapshot"
                        )

                | None ->
                    let rawMessages = Projection.messagesFromTransformOutput output
                    let physical = Projection.lastUserMessageId rawMessages

                    match scope.TryRecoveryArming sessionId, physical, snapshot with
                    | None, _, _ -> return ()
                    | Some _, None, _ ->
                        raise (InvalidOperationException "X-wire cannot plan a retry without a physical user message")
                    | Some _, _, None ->
                        raise (
                            InvalidOperationException "X-wire cannot plan a retry without the public session snapshot"
                        )
                    | Some arming, Some physical, Some snapshotPort ->
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
                                    FallbackEvidence.tryCurrentState sessionId projections,
                                    sessionProjection durable sessionId
                                with
                                | Some authority, Some fallback, Some state ->
                                    let current =
                                        Projection.decodeMessageView rawMessages |> ProviderProjection.toSemantic

                                    let cutoff = requestStartCutoff physical rawMessages
                                    let blog = state.Blog |> Option.defaultValue BlogProjection.empty
                                    let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty
                                    // Reuse the arming bound before the snapshot await: a session
                                    // deleted inside that window would otherwise make a second
                                    // TryRecoveryArming return None and Option.get throw (TOCTOU).
                                    let arming = arming

                                    // PROJ-002: the attempt-local projection snapshot is built once
                                    // and feeds both the probe proof (cutoffDigest) and the prefix
                                    // decision (requiredBlob / forChoice).
                                    // PROJ-008 Step6: reanchor 后 CommittedPrefix=None 且声明
                                    // ReanchorAfterCompaction（wire no-op）。若 epoch 仍有 snapshot，
                                    // 则走既有 Activate/Keep 路径。HostReanchor 由 Coordinator 填充。
                                    let hostReanchor: HostReanchorFact option =
                                        match prefix.Snapshot, Set.isEmpty prefix.ReanchoredRuns with
                                        | None, false ->
                                            // 最近一次 reanchor 观察：集合有元素但无 snapshot。
                                            // 生产路径不重放 observed run id 到 wire；仅作事实侧。
                                            Some
                                                { PreviousEpochId =
                                                    string (max 0L (PrefixEpochId.value prefix.EpochId - 1L))
                                                  NextEpochId = string (PrefixEpochId.value prefix.EpochId)
                                                  ObservedCompactionRunId = "" }
                                        | _ -> None

                                    let snapshot =
                                        { CurrentProjection = current
                                          CommittedPrefix = prefix.Snapshot
                                          BlogFrames = []
                                          TransportMessages = Set.empty
                                          HostReanchor = hostReanchor }

                                    let mayRecover =
                                        RecoverySlot.mayRecover
                                            arming
                                            fallback.Cursor.Offset
                                            (BlogProjection.hasCoverage blog)

                                    let selectProbe () =
                                        candidate durable sessionId snapshot state cutoff

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

                                    // `requiredBlob` is the single answer to "which blob does
                                    // this choice need" — the adapter reads, never guesses
                                    // (CTX-010: reading the COMMITTED blob for a probe attempt
                                    // would inject the old prefix under the candidate's id).
                                    let frozenRecordPrefixBody =
                                        match
                                            XPrefixProjection.requiredBlob
                                                plan.Profile.ProjectionChoice
                                                snapshot.CommittedPrefix
                                        with
                                        | None -> ""
                                        | Some blobRef ->
                                            match durable.Writer.BlobWriter.Read blobRef with
                                            | Ok body -> body
                                            | Error reason -> raise (InvalidOperationException reason)

                                    let prefixIntent =
                                        XPrefixProjection.forChoice
                                            plan.Profile.ProjectionChoice
                                            snapshot.CommittedPrefix
                                            frozenRecordPrefixBody

                                    // reanchor 后：prefix intent 已是 KeepPhysicalPrefix（Snapshot=None）；
                                    // 再声明 ReanchorAfterCompaction 表达 HOST-006 投影语义（wire no-op）。
                                    // Activate + Reanchor 同批 → ConflictingPrefixLifecycle（fail-closed）。
                                    // hostReanchor 仅在 Snapshot=None 时填充 → prefixIntent 必为 Keep。
                                    // Activate + Reanchor 冲突由 plan fail-closed 覆盖（unit 已证明）。
                                    let intents =
                                        match hostReanchor with
                                        | Some _ -> [ prefixIntent; ProjectionIntent.ReanchorAfterCompaction ]
                                        | None -> [ prefixIntent ]

                                    let transformed =
                                        match ProjectionPlanner.plan intents with
                                        | Error conflict ->
                                            raise (
                                                InvalidOperationException(
                                                    sprintf "X-wire projection conflict: %A" conflict
                                                )
                                            )
                                        | Ok ordered ->
                                            // ReanchorAfterCompaction 是 wire no-op；prefix 写回仍用
                                            // applyRenderedPrefix（与既有 Host id 字节合同一致）。
                                            let rendered = ProjectionRenderer.renderPrefix ordered
                                            Projection.applyRenderedPrefix rawMessages rendered

                                    Wanxiangshu.Session.HostMessageProjection.replaceMessagesInPlace output transformed

                                    scope.RecordAttemptPlan sessionId providerRun plan

                                    // FALLBACK-012 / CTX-011: arming is a one-shot
                                    // recovery opportunity. Consume it only when a
                                    // probe was actually selected, OR when recovery
                                    // was not possible for a durable reason. Temporary
                                    // NoCoverage (blog frames still catching up) must
                                    // keep arming so a later main can still probe —
                                    // otherwise a retry that races the first BlogEntry
                                    // burns the armed slot and PrefixRebase never lands.
                                    match AttemptPlanner.probeOf plan with
                                    | Some _ -> scope.ClearRecovery sessionId
                                    | None ->
                                        match plan.NoProbeReason with
                                        | Some NoCandidateReason.NoCoverage -> ()
                                        | _ -> scope.ClearRecovery sessionId
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
            // Keep the plan across provisional / unknown rereads of the SAME
            // provider run. A first idle wake often sees finish=None (Unknown) or
            // a needs-continuation race before the terminal finish lands; clearing
            // here made the subsequent TurnCompleted promote with TryAttemptPlan=None
            // (measured: FallbackCursorAdvanced without PrefixRebaseCommitted).
            match turn.Outcome with
            | ReconcileProgram.TurnCompleted ->
                match AttemptPlanner.promotableProbe plan AttemptOutcome.Completed with
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
            | ReconcileProgram.TurnFailed _
            | ReconcileProgram.TurnAborted _ -> scope.ClearAttemptPlan turn.SessionId turn.ProviderRun
            | ReconcileProgram.TurnNeedsContinuation _
            | ReconcileProgram.TurnInProgress -> ()
        | _ -> ()
