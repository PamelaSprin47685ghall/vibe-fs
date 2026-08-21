namespace Wanxiangshu.Context.Prefix

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
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
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Execution.Session
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
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
open FsToolkit.ErrorHandling

module XWire =

    let private sessionIdOfOutput (output: obj) : SessionId option =
        ProviderWireDecode.projectionSessionIdFromMessages output
        |> Option.map SessionId.create

    let private sessionProjection (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections

    let private isCompanionSession (journal: AgentJournal) (sessionId: SessionId) =
        SessionAssociationProjection.isCompanion sessionId (AgentJournal.snapshot journal).AgentProjections.Associations

    let private requireOk (result: Result<'a, string>) : 'a =
        match result with
        | Ok value -> value
        | Error reason -> raise (InvalidOperationException reason)

    let private requireOkMapped (mapError: 'e -> string) (result: Result<'a, 'e>) : 'a =
        match result with
        | Ok value -> value
        | Error error -> raise (InvalidOperationException(mapError error))

    let private ensureFrameDigest (frame: BlogFrame) (text: string) : Result<unit, string> =
        if HostDigest.sha256Hex text = BlobDigest.value frame.Digest then
            Ok()
        else
            Error(sprintf "Companion blob digest mismatch: %s" (BlobDigest.value frame.Digest))

    let private readFrameBody (journal: AgentJournal) (frame: BlogFrame) : Task<Result<string, string>> =
        taskResult {
            let! text = journal.Writer.BlobWriter.Read frame.TextRef
            do! ensureFrameDigest frame text
            return text
        }

    let private readFrameBodies (journal: AgentJournal) (frames: BlogFrame list) : Task<Result<string list, string>> =
        frames |> TaskResultList.traverseM (readFrameBody journal)

    let private readFrames (journal: AgentJournal) (frames: BlogFrame list) : Task<Result<string, string>> =
        task {
            let! bodies = readFrameBodies journal frames
            return bodies |> Result.map (fun values -> String.concat "\n\n" values)
        }

    let private requestStartCutoff (physical: PhysicalUserMessageId) (rawMessages: obj list) =
        rawMessages
        |> List.tryFindIndex (fun raw ->
            ProviderWireDecode.hostMessageId raw = Some(PhysicalUserMessageId.value physical))
        |> Option.defaultWith (fun () ->
            raise (InvalidOperationException "X-wire cannot bind the physical user message to the transform snapshot"))

    /// COMPANION-009 / CTX-011: FrozenRecordPrefix = Opening + coverable Y frame
    /// prefix. RawGap never participates — it has no Y coverage proof.
    let private materializeFrozenRecordPrefix
        (journal: AgentJournal)
        (state: SessionAgentProjection)
        (frames: BlogFrame list)
        : Task<Result<string, string>> =
        taskResult {
            let! frameBodies = readFrameBodies journal frames

            let opening =
                state.XTrace
                |> Option.bind (fun trace -> trace.Opening)
                |> Option.defaultValue
                    { AssignmentText = ""
                      AuthoritativeRequirements = []
                      ConstitutiveBody = "" }

            // Opening + frames only. Gap/terminal are live X material and must not
            // enter a frozen replacement (COMPANION-009).
            // COMPANION-009: FrozenRecordPrefix is same-session X memory, not a
            // parent/child hand-off. Opening stays (includeOpening=true).
            return
                LifecycleWorkRecord.render
                    true
                    { Opening = opening
                      Frames = frameBodies
                      Gap = [] }
        }

    let private candidate
        (journal: AgentJournal)
        (sessionId: SessionId)
        (snapshot: ProjectionSnapshot)
        (state: SessionAgentProjection)
        (requestCutoff: int)
        : Task<Result<PrefixProbe, NoCandidateReason>> =
        task {
            let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty
            let blog = state.Blog |> Option.defaultValue BlogProjection.empty

            if not (BlogProjection.hasCoverage blog) then
                return Error NoCandidateReason.NoCoverage
            else
                let frames = BlogProjection.coverableFrames blog
                let! frozenResult = materializeFrozenRecordPrefix journal state frames
                let frozenRecordPrefix = requireOk frozenResult
                let! blobResult = journal.WriteBlob frozenRecordPrefix
                let blob = requireOk blobResult

                return
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
        }

    let private requireStrengthReplicaAuthority
        (binding: StrengthReplicaBinding)
        (authority: PromptAuthority.AuthorityExecutionProfile option)
        =
        match authority with
        | None -> raise (InvalidOperationException "StrengthReplica has no active Authority Root")
        | Some authority when authority.CanonicalRole <> binding.CanonicalRole ->
            raise (InvalidOperationException "StrengthReplica Authority Root role changed after binding")
        | Some authority -> authority

    let private applyStrengthReplicaPlan
        (snapshotPort: ISessionSnapshotPort)
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (binding: StrengthReplicaBinding)
        (output: obj)
        : Task<unit> =
        task {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output

            let physical =
                match ProviderWireCapture.lastUserMessageId rawMessages with
                | Some physical -> physical
                | None -> raise (InvalidOperationException "StrengthReplica request has no physical user message")

            let! snapshotResult = snapshotPort.GetMessages sessionId
            let messages = requireOk snapshotResult

            let assistant =
                requireOkMapped
                    (fun rejection -> sprintf "StrengthReplica run binding failed: %A" rejection)
                    (ProviderRunBinding.bindableRun (PhysicalUserMessageId.value physical) messages)

            let providerRun = ProviderRunIdentity.create assistant.Id
            let projections = AgentJournal.snapshot durable

            let authority =
                requireStrengthReplicaAuthority
                    binding
                    (PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections)

            let plan =
                AttemptPlanner.plan
                    authority
                    AgentPairCursor.initial
                    physical
                    providerRun
                    (PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot)
                    ProviderRequestKind.StrengthReplica
                    RecoveryOpportunity.OrdinaryAttempt
                    (fun () -> Error NoCandidateReason.NoCoverage)

            if plan.Profile.ToolCapabilitySet <> binding.ToolCapabilitySet then
                raise (
                    InvalidOperationException
                        "StrengthReplica PromptAuthority capabilities disagree with live execution gate"
                )

            scope.RecordAttemptPlan sessionId providerRun plan
        }

    /// PROJ-008 Step6: reanchor 后 CommittedPrefix=None 且声明 ReanchorAfterCompaction（wire no-op）。
    /// 若 epoch 仍有 snapshot，则走既有 Activate/Keep 路径。HostReanchor 由 Coordinator 填充。
    let private observeHostReanchor (prefix: ActivePrefixEpoch) : HostReanchorFact option =
        match prefix.Snapshot, Set.isEmpty prefix.ReanchoredRuns with
        | None, false ->
            // 最近一次 reanchor 观察：集合有元素但无 snapshot。
            // 生产路径不重放 observed run id 到 wire；仅作事实侧。
            Some
                { PreviousEpochId = string (max 0L (PrefixEpochId.value prefix.EpochId - 1L))
                  NextEpochId = string (PrefixEpochId.value prefix.EpochId)
                  ObservedCompactionRunId = "" }
        | _ -> None

    let private selectCandidateForOpportunity
        (opportunity: RecoveryOpportunity)
        (durable: AgentJournal)
        (sessionId: SessionId)
        (snapshot: ProjectionSnapshot)
        (state: SessionAgentProjection)
        (cutoff: int)
        =
        match opportunity with
        | RecoveryOpportunity.RecoveryAttempt -> candidate durable sessionId snapshot state cutoff
        | RecoveryOpportunity.OrdinaryAttempt -> Task.FromResult(Error NoCandidateReason.NoCoverage)

    let private readFrozenRecordPrefixBody
        (durable: AgentJournal)
        (choice: XProjectionChoice)
        (committed: PrefixSnapshot option)
        : Task<string> =
        match XPrefixProjection.requiredBlob choice committed with
        | None -> Task.FromResult ""
        | Some blobRef ->
            task {
                let! body = durable.Writer.BlobWriter.Read blobRef
                return requireOk body
            }

    // reanchor 后：prefix intent 已是 KeepPhysicalPrefix（Snapshot=None）；
    // 再声明 ReanchorAfterCompaction 表达 HOST-006 投影语义（wire no-op）。
    // Activate + Reanchor 同批 → ConflictingPrefixLifecycle（fail-closed）。
    // hostReanchor 仅在 Snapshot=None 时填充 → prefixIntent 必为 Keep。
    // Activate + Reanchor 冲突由 plan fail-closed 覆盖（unit 已证明）。
    let private intentsForHostReanchor (hostReanchor: HostReanchorFact option) (prefixIntent: ProjectionIntent) =
        match hostReanchor with
        | Some _ -> [ prefixIntent; ProjectionIntent.ReanchorAfterCompaction ]
        | None -> [ prefixIntent ]

    let private renderPrefixMessages (rawMessages: obj list) (intents: ProjectionIntent list) =
        match ProjectionPlanner.plan intents with
        | Error conflict -> raise (InvalidOperationException(sprintf "X-wire projection conflict: %A" conflict))
        | Ok ordered ->
            // ReanchorAfterCompaction 是 wire no-op；prefix 写回仍用
            // applyRenderedPrefix（与既有 Host id 字节合同一致）。
            let rendered = ProjectionRenderer.renderPrefix ordered
            ProjectionMessageEdit.applyRenderedPrefix rawMessages rendered

    let private commitPromotablePrefixRebase
        (durable: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (plan: AttemptPlan)
        : Task<Result<unit, string>> =
        task {
            let projections = AgentJournal.snapshot durable

            let epoch =
                AgentProjection.tryFind sessionId projections.AgentProjections
                |> Option.bind (fun state -> state.PrefixEpoch)
                |> Option.defaultValue PrefixEpochProjection.empty

            match AttemptPlanner.promotableProbe plan AttemptOutcome.Completed with
            | Some probe when epoch.EpochId = probe.BasedOnEpochId ->
                let fact =
                    ContextFact.PrefixRebaseCommitted
                        {| SessionId = sessionId
                           PreviousEpochId = probe.BasedOnEpochId
                           NextEpochId = PrefixEpochId.next probe.BasedOnEpochId
                           FrozenRecordPrefixRef = probe.Candidate.FrozenRecordPrefixRef
                           FrozenRecordPrefixDigest = probe.Candidate.FrozenRecordPrefixDigest
                           CutoffExclusive = probe.Candidate.CutoffExclusive
                           CoveredPrefixDigest = probe.Candidate.CoveredPrefixDigest
                           SealRoot = probe.Candidate.SealRoot
                           SyntheticMessageId = probe.Candidate.SyntheticMessageId
                           ProbeId = probe.ProbeId
                           SolvingProviderRun = providerRun |}

                let! appended = AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact durable
                return appended |> Result.map (fun _ -> ()) |> Result.mapError JournalAppendFailure.describe
            | _ -> return Ok()
        }

    let private recordSuccessfulAttempt
        (durable: AgentJournal)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (plan: AttemptPlan)
        : Task<Result<unit, string>> =
        if ProviderRequestKind.clearsFailureCountOnSuccess plan.Profile.RequestKind then
            FallbackLedger.recordConfirmedSuccess durable sessionId providerRun
        else
            Task.FromResult(Ok())

    let private settleAttemptPlan
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (outcome: AttemptOutcome)
        (plan: AttemptPlan)
        : Task =
        task {
            let! committed =
                match outcome with
                | AttemptOutcome.Completed -> commitPromotablePrefixRebase durable sessionId providerRun plan
                | AttemptOutcome.CompletedInvalid
                | AttemptOutcome.Failed
                | AttemptOutcome.Aborted -> Task.FromResult(Ok())

            match committed with
            | Error reason ->
                return raise (InvalidOperationException(sprintf "prefix rebase commit failed: %s" reason))
            | Ok() ->
                let! success =
                    match outcome with
                    | AttemptOutcome.Completed -> recordSuccessfulAttempt durable sessionId providerRun plan
                    | AttemptOutcome.CompletedInvalid
                    | AttemptOutcome.Failed
                    | AttemptOutcome.Aborted -> Task.FromResult(Ok())

                match success with
                | Error reason ->
                    return raise (InvalidOperationException(sprintf "provider success commit failed: %s" reason))
                | Ok() -> scope.ConsumeAttemptPlan sessionId providerRun |> ignore
        }

    let private toolContinuationRun (rawMessage: obj) : ProviderRunIdentity option =
        let info = ProviderWireDecode.infoObject rawMessage

        match
            ProviderWireDecode.firstString info [ "role" ],
            ProviderWireDecode.firstString info [ "finish" ],
            ProviderWireDecode.hostMessageId rawMessage
        with
        | Some role, Some finish, Some providerRun when
            role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
            && finish.Equals("tool-calls", StringComparison.OrdinalIgnoreCase)
            -> Some(ProviderRunIdentity.create providerRun)
        | _ -> None

    let private settleVisibleToolContinuations
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (rawMessages: obj list)
        : Task =
        task {
            for providerRun in rawMessages |> List.choose toolContinuationRun |> List.distinct do
                match scope.TryAttemptPlan sessionId providerRun with
                | None -> ()
                | Some plan -> do! settleAttemptPlan durable scope sessionId providerRun AttemptOutcome.Completed plan
        }

    let private applyCommittedPrefix
        (durable: AgentJournal)
        (sessionId: SessionId)
        (state: SessionAgentProjection)
        (rawMessages: obj list)
        (output: obj)
        : Task<unit> =
        task {
            let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty

            match prefix.Snapshot with
            | None -> return ()
            | Some committed ->
                let choice = XProjectionChoice.UseCommittedEpoch
                let! frozenRecordPrefixBody = readFrozenRecordPrefixBody durable choice (Some committed)

                let memoryPreamble =
                    ProviderProse.render (ProviderProse.languageOf sessionId) CompanionPrompt.MemoryPreamble Map.empty

                let prefixIntent =
                    XPrefixProjection.forChoice choice (Some committed) memoryPreamble frozenRecordPrefixBody

                let intents = intentsForHostReanchor (observeHostReanchor prefix) prefixIntent
                let transformed = renderPrefixMessages rawMessages intents
                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed
        }

    let private awaitProjectionSignal
        (messageVisibility: MessageVisibilityHub option)
        (sessionId: SessionId)
        : Task<unit> =
        match messageVisibility with
        | Some hub -> hub.AwaitChange sessionId ProviderRunBinding.projectionCatchupDelayMilliseconds
        | None -> Task.FromResult(())

    let private bindProviderRunAfterProjectionCatchup
        (messageVisibility: MessageVisibilityHub option)
        (snapshotPort: ISessionSnapshotPort)
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        : Task<SessionMessage> =
        let physicalId = PhysicalUserMessageId.value physical

        let rec read remainingReads =
            task {
                let! snapshotResult = snapshotPort.GetMessages sessionId
                let messages = requireOk snapshotResult

                match ProviderRunBinding.observeBindableRun physicalId messages with
                | ProviderRunBinding.Observation.Bound assistant -> return assistant
                | ProviderRunBinding.Observation.Rejected rejection ->
                    return
                        requireOkMapped (fun error -> sprintf "X-wire run binding failed: %A" error) (Error rejection)
                | ProviderRunBinding.Observation.ProjectionNotVisibleYet when remainingReads > 1 ->
                    do! awaitProjectionSignal messageVisibility sessionId
                    return! read (remainingReads - 1)
                | ProviderRunBinding.Observation.ProjectionNotVisibleYet ->
                    return
                        requireOkMapped
                            (fun error -> sprintf "X-wire run binding failed: %A" error)
                            (Error ProviderRunBinding.Rejection.NoBindableRun)
            }

        read ProviderRunBinding.projectionCatchupMaxReads

    let private planArmedWorkMainRetry
        (snapshotPort: ISessionSnapshotPort)
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (arming: SlotArming)
        (physical: PhysicalUserMessageId)
        (output: obj)
        (rawMessages: obj list)
        : Task<unit> =
        task {
            let! assistant =
                bindProviderRunAfterProjectionCatchup scope.MessageVisibility snapshotPort sessionId physical

            let providerRun = ProviderRunIdentity.create assistant.Id
            let projections = AgentJournal.snapshot durable

            match
                PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections,
                FallbackEvidence.tryCurrentState sessionId projections,
                sessionProjection durable sessionId
            with
            | Some authority, Some fallback, Some state ->
                let current =
                    ProviderWireCapture.decodeMessageView rawMessages
                    |> ProviderProjection.toSemantic

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
                let hostReanchor = observeHostReanchor prefix

                let snapshot =
                    { CurrentProjection = current
                      CommittedPrefix = prefix.Snapshot
                      BlogFrames = []
                      TransportMessages = Set.empty
                      HostReanchor = hostReanchor }

                let opportunity = RecoverySlot.opportunity arming fallback.Cursor.Offset

                let! candidateResult = selectCandidateForOpportunity opportunity durable sessionId snapshot state cutoff

                let selectProbe () = candidateResult

                let plan =
                    AttemptPlanner.plan
                        authority
                        fallback.Cursor
                        physical
                        providerRun
                        (PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ProviderRetryAttempt)
                        ProviderRequestKind.WorkMain
                        opportunity
                        selectProbe

                // `requiredBlob` is the single answer to "which blob does this choice
                // need" — the adapter reads, never guesses (CTX-010: reading the
                // COMMITTED blob for a probe attempt would inject the old prefix under
                // the candidate's id).
                let! frozenRecordPrefixBody =
                    readFrozenRecordPrefixBody durable plan.Profile.ProjectionChoice snapshot.CommittedPrefix

                let memoryPreamble =
                    ProviderProse.render (ProviderProse.languageOf sessionId) CompanionPrompt.MemoryPreamble Map.empty

                let prefixIntent =
                    XPrefixProjection.forChoice
                        plan.Profile.ProjectionChoice
                        snapshot.CommittedPrefix
                        memoryPreamble
                        frozenRecordPrefixBody

                let intents = intentsForHostReanchor hostReanchor prefixIntent
                let transformed = renderPrefixMessages rawMessages intents

                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed

                scope.RecordAttemptPlan sessionId providerRun plan

            | _ ->
                raise (
                    InvalidOperationException
                        "X-wire cannot plan a retry without authority, fallback, and session projections"
                )
        }

    let private applyNonReplicaTransform
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (snapshot: ISessionSnapshotPort option)
        (output: obj)
        : Task<unit> =
        task {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output
            let physical = ProviderWireCapture.lastUserMessageId rawMessages

            // A successful probe may have ended with tool calls. That provider
            // attempt is complete even though the Host turn continues through the
            // tool loop. Settle it before reading PrefixEpoch for this request.
            do! settleVisibleToolContinuations durable scope sessionId rawMessages

            // Owning recovery CE consumes the typed arming permit exactly once (SW-017②, PAR-011).
            // Host callback is only rendezvous/observation; presence no longer drives business branching
            // outside the owning CE.
            match scope.TryTakeRecoveryPermit sessionId, physical, snapshot with
            | None, _, _ ->
                match sessionProjection durable sessionId with
                | None ->
                    raise (InvalidOperationException "X-wire cannot apply a committed prefix without session projection")
                | Some state -> return! applyCommittedPrefix durable sessionId state rawMessages output
            | Some _, None, _ ->
                raise (InvalidOperationException "X-wire cannot plan a retry without a physical user message")
            | Some _, _, None ->
                raise (InvalidOperationException "X-wire cannot plan a retry without the public session snapshot")
            | Some arming, Some physical, Some snapshotPort ->
                do! planArmedWorkMainRetry snapshotPort durable scope sessionId arming physical output rawMessages
        }

    let private applySessionTransform
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (snapshot: ISessionSnapshotPort option)
        (output: obj)
        : Task<unit> =
        task {
            match scope.Strength.StrengthRuntime.TryFindByReplica sessionId, snapshot with
            | Some binding, Some snapshotPort ->
                do! applyStrengthReplicaPlan snapshotPort durable scope sessionId binding output
            | Some _, None ->
                raise (InvalidOperationException "StrengthReplica cannot plan without the public session snapshot")
            | None, _ -> do! applyNonReplicaTransform durable scope sessionId snapshot output
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
                do! applySessionTransform durable scope sessionId snapshot output
            | _ -> return ()
        }

    let private attemptOutcomeOfTurn (turn: ReconciledTurn) : AttemptOutcome option =
        match turn.Observation, turn.Outcome with
        | Some _, _ -> None
        | None, ReconcileProgram.TurnCompleted -> Some AttemptOutcome.Completed
        | None, ReconcileProgram.TurnInProgress -> Some AttemptOutcome.Completed
        | None, ReconcileProgram.TurnNeedsContinuation _ -> Some AttemptOutcome.CompletedInvalid
        | None, ReconcileProgram.TurnFailed _ -> Some AttemptOutcome.Failed
        | None, ReconcileProgram.TurnAborted _ -> Some AttemptOutcome.Aborted

    /// Settle the physical provider attempt, not the larger Host turn.
    /// `finish=tool-calls` therefore closes a successful attempt plan while the
    /// Host tool loop continues; only a genuinely provisional snapshot keeps it.
    let reconcileAttempt (journal: AgentJournal option) (scope: PluginRuntimeScope) (turn: ReconciledTurn) : Task =
        match journal, scope.TryAttemptPlan turn.SessionId turn.ProviderRun with
        | Some durable, Some plan ->
            match attemptOutcomeOfTurn turn with
            | Some outcome -> settleAttemptPlan durable scope turn.SessionId turn.ProviderRun outcome plan
            | None -> Task.FromResult(())
        | _ -> Task.FromResult(())
