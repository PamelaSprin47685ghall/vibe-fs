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
                    false
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

    let private selectCandidateWhenRecoverable
        (mayRecover: bool)
        (durable: AgentJournal)
        (sessionId: SessionId)
        (snapshot: ProjectionSnapshot)
        (state: SessionAgentProjection)
        (cutoff: int)
        =
        if mayRecover then
            candidate durable sessionId snapshot state cutoff
        else
            Task.FromResult(Error NoCandidateReason.NoCoverage)

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

    // FALLBACK-012 / CTX-011: arming is a one-shot recovery opportunity. Consume it
    // only when a probe was actually selected, OR when recovery was not possible for
    // a durable reason. Temporary NoCoverage (blog frames still catching up) must keep
    // arming so a later main can still probe — otherwise a retry that races the first
    // BlogEntry burns the armed slot and PrefixRebase never lands.
    let private consumeRecoveryArming (scope: PluginRuntimeScope) (sessionId: SessionId) (plan: AttemptPlan) =
        match AttemptPlanner.probeOf plan, plan.NoProbeReason with
        | Some _, _ -> scope.ClearRecovery sessionId
        | None, Some NoCandidateReason.NoCoverage -> ()
        | None, _ -> scope.ClearRecovery sessionId

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
            let! snapshotResult = snapshotPort.GetMessages sessionId
            let messages = requireOk snapshotResult

            let assistant =
                requireOkMapped
                    (fun rejection -> sprintf "X-wire run binding failed: %A" rejection)
                    (ProviderRunBinding.bindableRun (PhysicalUserMessageId.value physical) messages)

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

                let mayRecover =
                    RecoverySlot.mayRecover arming fallback.Cursor.Offset (BlogProjection.hasCoverage blog)

                let! candidateResult = selectCandidateWhenRecoverable mayRecover durable sessionId snapshot state cutoff

                let selectProbe () = candidateResult

                let plan =
                    AttemptPlanner.plan
                        authority
                        fallback.Cursor
                        physical
                        providerRun
                        (PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ProviderRetryAttempt)
                        ProviderRequestKind.WorkMain
                        mayRecover
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
                consumeRecoveryArming scope sessionId plan

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

            match scope.TryRecoveryArming sessionId, physical, snapshot with
            | None, _, _ -> return ()
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

    let private commitPromotablePrefixRebase
        (durable: AgentJournal)
        (turn: ReconciledTurn)
        (plan: AttemptPlan)
        : Task<unit> =
        task {
            let projections = AgentJournal.snapshot durable

            let epoch =
                AgentProjection.tryFind turn.SessionId projections.AgentProjections
                |> Option.bind (fun state -> state.PrefixEpoch)
                |> Option.defaultValue PrefixEpochProjection.empty

            match AttemptPlanner.promotableProbe plan AttemptOutcome.Completed with
            | Some probe when epoch.EpochId = probe.BasedOnEpochId ->
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

                let! _ = AgentJournal.appendAgent (StreamId.Session turn.SessionId) (Some turn.ProviderRun) fact durable

                ()
            | _ -> ()
        }

    let private reconcileArmedAttempt
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (turn: ReconciledTurn)
        (plan: AttemptPlan)
        : Task =
        // Keep the plan across provisional / unknown rereads of the SAME
        // provider run. A first idle wake often sees finish=None (Unknown) or
        // a needs-continuation race before the terminal finish lands; clearing
        // here made the subsequent TurnCompleted promote with TryAttemptPlan=None
        // (measured: FallbackCursorAdvanced without PrefixRebaseCommitted).
        match turn.Outcome with
        | ReconcileProgram.TurnCompleted ->
            task {
                do! commitPromotablePrefixRebase durable turn plan
                scope.ClearAttemptPlan turn.SessionId turn.ProviderRun
            }
            :> Task
        | ReconcileProgram.TurnFailed _
        | ReconcileProgram.TurnAborted _ ->
            scope.ClearAttemptPlan turn.SessionId turn.ProviderRun
            Task.FromResult(())
        | ReconcileProgram.TurnNeedsContinuation _
        | ReconcileProgram.TurnInProgress -> Task.FromResult(())

    let reconcileAttempt (journal: AgentJournal option) (scope: PluginRuntimeScope) (turn: ReconciledTurn) : Task =
        match journal, scope.TryAttemptPlan turn.SessionId turn.ProviderRun with
        | Some durable, Some plan -> reconcileArmedAttempt durable scope turn plan
        | _ -> Task.FromResult(())
