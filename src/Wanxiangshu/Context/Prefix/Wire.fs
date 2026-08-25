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

[<RequireQualifiedAccess>]
type PrefixPresentationHorizon =
    | Current
    | TentativeCold

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

    /// Prefix coverage is expressed only in canonical XTrace semantic-turn
    /// coordinates. A positional legacy trace is insufficient proof and fails
    /// closed instead of silently interpreting a provider-array index as history.
    let private providerRetryOrigin =
        PromptAuthority.originLabel (PromptAuthority.PromptOrigin.Continuation PromptAuthority.ProviderRetryAttempt)

    let private isProviderRetryAttempt (rawMessage: obj) =
        ProviderWireDecode.promptOriginOfMessage rawMessage = Some providerRetryOrigin

    let private isProviderRetryMessageId (messageId: string) (rawMessages: obj list) =
        rawMessages
        |> List.exists (fun message ->
            ProviderWireDecode.hostMessageId message = Some messageId
            && isProviderRetryAttempt message)

    let private requestStartCutoff
        (physical: PhysicalUserMessageId)
        (rawMessages: obj list)
        (xTrace: XTraceProjectionState)
        =
        let physicalId = PhysicalUserMessageId.value physical

        match XTraceProjection.tryTurnOfHostMessageId physicalId xTrace with
        | Some cutoff -> cutoff
        | None when isProviderRetryMessageId physicalId rawMessages ->
            ProviderWireCapture.trySemanticTurnOfHostMessageId physicalId rawMessages
            |> Option.defaultWith (fun () ->
                raise (
                    InvalidOperationException
                        "X-wire cannot bind the retry user message to the Host semantic-turn coordinate"
                ))
        | None ->
            raise (
                InvalidOperationException
                    "X-wire cannot bind the physical user message to stable canonical XTrace provenance"
            )

    let private staleProviderRetryMessageIds (rawMessages: obj list) =
        let currentPhysical =
            ProviderWireCapture.lastUserMessageId rawMessages
            |> Option.map PhysicalUserMessageId.value

        rawMessages
        |> List.choose (fun message ->
            match ProviderWireDecode.hostMessageId message with
            | Some messageId when isProviderRetryAttempt message && Some messageId <> currentPhysical -> Some messageId
            | _ -> None)
        |> Set.ofList

    let private retryTransportRetirement (horizon: PrefixPresentationHorizon) (rawMessages: obj list) =
        match horizon with
        | PrefixPresentationHorizon.Current -> Set.empty
        | PrefixPresentationHorizon.TentativeCold -> staleProviderRetryMessageIds rawMessages

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

            // Same-session FrozenRecordPrefix omits Opening (WORK-RECORD-007):
            // the true raw Opening remains physically present outside the Y
            // replacement. Gap/terminal are live X material and also stay out.
            return
                LifecycleWorkRecord.render
                    false
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

    /// HOST-BOUNDARY-008: `experimental.chat.messages.transform` runs before the
    /// Host creates the assistant run, so ProviderRunIdentity cannot be an input
    /// here and no bounded wait may disguise a future run as projection lag.
    /// The replica recovery decision is frozen as an UNBOUND attempt plan keyed
    /// by the exact PhysicalUserMessageId; the exact ProviderRunIdentity binds
    /// exactly once later from a complete Host observation
    /// (`TryBindAttemptPlan` on the turn path). Capability agreement between the
    /// frozen authority role and the live execution gate is checked now — it
    /// depends only on session-scoped evidence, never on a future run.
    let private applyStrengthReplicaPlan
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

            let projections = AgentJournal.snapshot durable

            let authority =
                requireStrengthReplicaAuthority
                    binding
                    (PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections)

            if
                PromptAuthority.toolCapabilitiesFor authority.CanonicalRole ProviderRequestKind.StrengthReplica
                <> binding.ToolCapabilitySet
            then
                raise (
                    InvalidOperationException
                        "StrengthReplica PromptAuthority capabilities disagree with live execution gate"
                )

            let pendingPlan =
                AttemptPlanner.freezePreInference
                    authority
                    AgentPairCursor.initial
                    physical
                    (PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot)
                    ProviderRequestKind.StrengthReplica
                    RecoveryOpportunity.OrdinaryAttempt
                    (fun () -> Error NoCandidateReason.NoCoverage)

            scope.RecordPendingAttemptPlan sessionId physical pendingPlan
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

    let private requireStableReplacement
        (activation: PrefixActivation)
        (xTrace: XTraceProjectionState)
        : string list * string option =
        let coveredHostMessageIds =
            XTraceProjection.hostMessageIdsBeforeTurn activation.CutoffExclusive xTrace

        if activation.CutoffExclusive > 0 && List.isEmpty coveredHostMessageIds then
            raise (
                InvalidOperationException
                    "X-wire cannot replace a covered prefix without stable canonical XTrace message identities"
            )

        let openingHostMessageId = XTraceProjection.tryOpeningHostMessageId xTrace

        if activation.CutoffExclusive > 0 && Option.isNone openingHostMessageId then
            raise (
                InvalidOperationException
                    "X-wire cannot place same-session memory without the stable raw Opening identity"
            )

        coveredHostMessageIds
        |> List.filter (fun messageId -> Some messageId <> openingHostMessageId),
        openingHostMessageId

    let private applyPlannedPrefix
        (state: SessionAgentProjection)
        (rawMessages: obj list)
        (ordered: ProjectionIntent list)
        =
        let rendered = ProjectionRenderer.renderPrefix ordered

        match rendered with
        | RenderedPrefix.PhysicalPrefix -> rawMessages
        | RenderedPrefix.SyntheticPrefix activation ->
            let xTrace = state.XTrace |> Option.defaultValue XTraceProjection.empty

            let replaceableHostMessageIds, openingHostMessageId =
                requireStableReplacement activation xTrace

            ProjectionMessageEdit.applyRenderedPrefixByHostIds
                rawMessages
                replaceableHostMessageIds
                openingHostMessageId
                rendered

    let private renderPrefixMessages
        (state: SessionAgentProjection)
        (rawMessages: obj list)
        (intents: ProjectionIntent list)
        (horizon: PrefixPresentationHorizon)
        =
        // A retry row that was visible in a tentative-cold provider request is
        // now part of that new horizon's physical prefix even though it is not X
        // semantics. Retiring it on the very next ordinary request would shrink
        // the provider wire. Historical retry rows may therefore retire only as
        // part of a later real cold presentation; Current must be byte-preserving.
        let staleTransport = retryTransportRetirement horizon rawMessages

        let intents =
            if Set.isEmpty staleTransport then
                intents
            else
                ProjectionIntent.SuppressTransportOnly :: intents

        match ProjectionPlanner.plan intents with
        | Error conflict -> raise (InvalidOperationException(sprintf "X-wire projection conflict: %A" conflict))
        | Ok ordered ->
            applyPlannedPrefix state rawMessages ordered
            |> fun prefixed -> ProjectionMessageEdit.suppressHostMessagesByIds prefixed staleTransport

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

                return
                    appended
                    |> Result.map (fun _ -> ())
                    |> Result.mapError JournalAppendFailure.describe
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

            committed
            |> Result.mapError (fun reason -> sprintf "prefix rebase commit failed: %s" reason)
            |> requireOk
            |> ignore

            let! success =
                match outcome with
                | AttemptOutcome.Completed -> recordSuccessfulAttempt durable sessionId providerRun plan
                | AttemptOutcome.CompletedInvalid
                | AttemptOutcome.Failed
                | AttemptOutcome.Aborted -> Task.FromResult(Ok())

            success
            |> Result.mapError (fun reason -> sprintf "provider success commit failed: %s" reason)
            |> requireOk
            |> ignore

            scope.ConsumeAttemptPlan sessionId providerRun |> ignore
        }

    let private toolContinuationBinding
        (rawMessage: obj)
        : (ProviderRunIdentity * PhysicalUserMessageId option) option =
        let info = ProviderWireDecode.infoObject rawMessage

        match
            ProviderWireDecode.firstString info [ "role" ],
            ProviderWireDecode.firstString info [ "finish" ],
            ProviderWireDecode.hostMessageId rawMessage
        with
        | Some role, Some finish, Some providerRun when
            role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
            && finish.Equals("tool-calls", StringComparison.OrdinalIgnoreCase)
            ->
            let physical =
                ProviderWireDecode.firstString info [ "parentID" ]
                |> Option.map PhysicalUserMessageId.create

            Some(ProviderRunIdentity.create providerRun, physical)
        | _ -> None

    let private settleVisibleToolContinuation
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (physical: PhysicalUserMessageId option)
        : Task =
        let plan =
            match scope.TryAttemptPlan sessionId providerRun with
            | Some existing -> Some existing
            | None ->
                physical
                |> Option.bind (fun parent -> scope.TryBindAttemptPlan sessionId parent providerRun)

        match plan with
        | None -> Task.FromResult(())
        | Some plan -> settleAttemptPlan durable scope sessionId providerRun AttemptOutcome.Completed plan

    let private settleVisibleToolContinuations
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (rawMessages: obj list)
        : Task =
        task {
            for providerRun, physical in rawMessages |> List.choose toolContinuationBinding |> List.distinct do
                do! settleVisibleToolContinuation durable scope sessionId providerRun physical
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
            | None ->
                // Even before the first Y epoch exists, XWire still owns the Host
                // transport membrane. Otherwise every ordinary A/B slot between
                // failed probes would accumulate all prior ProviderRetryAttempt
                // rows and undo the recovery slot's cleanup.
                let intents =
                    intentsForHostReanchor (observeHostReanchor prefix) ProjectionIntent.KeepPhysicalPrefix

                let transformed =
                    renderPrefixMessages state rawMessages intents PrefixPresentationHorizon.Current

                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed
            | Some committed ->
                let choice = XProjectionChoice.UseCommittedEpoch
                let! frozenRecordPrefixBody = readFrozenRecordPrefixBody durable choice (Some committed)

                let memoryPreamble =
                    ProviderProse.render (ProviderProse.languageOf sessionId) CompanionPrompt.MemoryPreamble Map.empty

                let prefixIntent =
                    XPrefixProjection.forChoice choice (Some committed) memoryPreamble frozenRecordPrefixBody

                let intents = intentsForHostReanchor (observeHostReanchor prefix) prefixIntent

                let transformed =
                    renderPrefixMessages state rawMessages intents PrefixPresentationHorizon.Current

                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed
        }

    let private applyOrdinaryCommittedPrefix
        (durable: AgentJournal)
        (sessionId: SessionId)
        (rawMessages: obj list)
        (output: obj)
        : Task =
        match sessionProjection durable sessionId with
        | None -> raise (InvalidOperationException "X-wire cannot apply a committed prefix without session projection")
        | Some state -> applyCommittedPrefix durable sessionId state rawMessages output

    let private planArmedWorkMainRetry
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (arming: SlotArming)
        (physical: PhysicalUserMessageId)
        (output: obj)
        (rawMessages: obj list)
        : Task<PrefixPresentationHorizon> =
        task {
            let projections = AgentJournal.snapshot durable

            match
                PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections,
                FallbackEvidence.tryCurrentState sessionId projections,
                sessionProjection durable sessionId
            with
            | Some authority, Some fallback, Some state ->
                let blog = state.Blog |> Option.defaultValue BlogProjection.empty
                let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty
                let xTrace = state.XTrace |> Option.defaultValue XTraceProjection.empty

                let! currentResult = XTraceMaterialization.currentProjection durable xTrace
                let current = requireOk currentResult

                let cutoff = requestStartCutoff physical rawMessages xTrace
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

                let pendingPlan =
                    AttemptPlanner.freezePreInference
                        authority
                        fallback.Cursor
                        physical
                        (PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ProviderRetryAttempt)
                        ProviderRequestKind.WorkMain
                        opportunity
                        selectProbe

                let presentationHorizon =
                    match AttemptPlanner.pendingProbeOf pendingPlan with
                    | Some _ -> PrefixPresentationHorizon.TentativeCold
                    | None -> PrefixPresentationHorizon.Current

                // `requiredBlob` is the single answer to "which blob does this choice
                // need" — the adapter reads, never guesses (CTX-010: reading the
                // COMMITTED blob for a probe attempt would inject the old prefix under
                // the candidate's id).
                let! frozenRecordPrefixBody =
                    readFrozenRecordPrefixBody durable pendingPlan.ProjectionChoice snapshot.CommittedPrefix

                let memoryPreamble =
                    ProviderProse.render (ProviderProse.languageOf sessionId) CompanionPrompt.MemoryPreamble Map.empty

                let prefixIntent =
                    XPrefixProjection.forChoice
                        pendingPlan.ProjectionChoice
                        snapshot.CommittedPrefix
                        memoryPreamble
                        frozenRecordPrefixBody

                let intents = intentsForHostReanchor hostReanchor prefixIntent
                let transformed = renderPrefixMessages state rawMessages intents presentationHorizon

                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed

                scope.RecordPendingAttemptPlan sessionId physical pendingPlan
                return presentationHorizon

            | _ ->
                return
                    raise (
                        InvalidOperationException
                            "X-wire cannot plan a retry without authority, fallback, and session projections"
                    )
        }

    let private applyNonReplicaTransform
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (_snapshot: ISessionSnapshotPort option)
        (output: obj)
        : Task<PrefixPresentationHorizon> =
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
            let recoveryAttempt =
                physical
                |> Option.bind (fun physical ->
                    scope.TryTakeRecoveryPermit(sessionId, physical)
                    |> Option.map (fun arming -> physical, arming))

            match recoveryAttempt with
            | None ->
                do! applyOrdinaryCommittedPrefix durable sessionId rawMessages output
                return PrefixPresentationHorizon.Current
            | Some(physical, arming) ->
                return! planArmedWorkMainRetry durable scope sessionId arming physical output rawMessages
        }

    let private applySessionTransform
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (snapshot: ISessionSnapshotPort option)
        (output: obj)
        : Task<PrefixPresentationHorizon> =
        task {
            match scope.Strength.StrengthRuntime.TryFindByReplica sessionId with
            | Some binding ->
                do! applyStrengthReplicaPlan durable scope sessionId binding output
                return PrefixPresentationHorizon.Current
            | None -> return! applyNonReplicaTransform durable scope sessionId snapshot output
        }

    let applyTransform
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (output: obj)
        : Task<PrefixPresentationHorizon> =
        task {
            match journal, sessionIdOfOutput output with
            | Some durable, Some sessionId when not (isCompanionSession durable sessionId) ->
                return! applySessionTransform durable scope sessionId snapshot output
            | _ -> return PrefixPresentationHorizon.Current
        }

    let private attemptOutcomeOfTurn (turn: ReconciledTurn) : AttemptOutcome option =
        match turn.Observation, turn.Outcome with
        | Some _, _ -> None
        | None, ReconcileProgram.TurnCompleted -> Some AttemptOutcome.Completed
        | None, ReconcileProgram.TurnInProgress -> Some AttemptOutcome.Completed
        | None, ReconcileProgram.TurnNeedsContinuation _ -> Some AttemptOutcome.CompletedInvalid
        | None, ReconcileProgram.TurnFailed _ -> Some AttemptOutcome.Failed
        | None, ReconcileProgram.TurnAborted _ -> Some AttemptOutcome.Aborted

    let private reconcilePlannedAttempt
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (turn: ReconciledTurn)
        (plan: AttemptPlan)
        : Task =
        match attemptOutcomeOfTurn turn with
        | Some outcome -> settleAttemptPlan durable scope turn.SessionId turn.ProviderRun outcome plan
        | None -> Task.FromResult(())

    let private attemptPlanForTurn (scope: PluginRuntimeScope) (turn: ReconciledTurn) =
        scope.TryAttemptPlan turn.SessionId turn.ProviderRun
        |> Option.orElseWith (fun () ->
            scope.TryBindAttemptPlan turn.SessionId turn.PhysicalUserMessageId turn.ProviderRun)

    let private plannedReconciliation
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (turn: ReconciledTurn)
        =
        journal
        |> Option.bind (fun durable -> attemptPlanForTurn scope turn |> Option.map (fun plan -> durable, plan))

    /// Settle the physical provider attempt, not the larger Host turn.
    /// `finish=tool-calls` therefore closes a successful attempt plan while the
    /// Host tool loop continues; only a genuinely provisional snapshot keeps it.
    let reconcileAttempt (journal: AgentJournal option) (scope: PluginRuntimeScope) (turn: ReconciledTurn) : Task =
        match plannedReconciliation journal scope turn with
        | Some(durable, plan) -> reconcilePlannedAttempt durable scope turn plan
        | None -> Task.FromResult(())
