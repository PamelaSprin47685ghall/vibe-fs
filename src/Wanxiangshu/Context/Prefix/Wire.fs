namespace Wanxiangshu.Context.Prefix

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Participant.Provider.Projection

[<RequireQualifiedAccess>]
type PrefixPresentationHorizon =
    | Current
    | TentativeCold

type XWireReconciliationDecision =
    { Promoted: bool
      Cleared: bool
      KeptPlan: bool }

type AttemptPlanCapability =
    { TryAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option
      TryBindAttemptPlan: SessionId -> PhysicalUserMessageId -> ProviderRunIdentity -> AttemptPlan option
      ConsumeAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option
      FreezePendingAttemptPlan: SessionId -> PhysicalUserMessageId -> PendingAttemptPlan -> PendingAttemptPlanAdmission
      TryPendingAttemptPlan: SessionId -> PhysicalUserMessageId -> PendingAttemptPlan option }

module XWire =

    let mayProbe (budget: ProviderFailureBudget.FailureBudget) : bool = budget.ConsecutiveFailureCount > 0

    let presentationHorizonForProbe (hasProbe: bool) : PrefixPresentationHorizon =
        if hasProbe then
            PrefixPresentationHorizon.TentativeCold
        else
            PrefixPresentationHorizon.Current

    let reconciliationDecision
        (hasPlan: bool)
        (outcome: AttemptOutcome option)
        (hasPromotableProbe: bool)
        (probeEpochMatches: bool)
        : XWireReconciliationDecision =
        match hasPlan, outcome with
        | false, _ ->
            { Promoted = false
              Cleared = false
              KeptPlan = false }
        | true, Some AttemptOutcome.Completed ->
            { Promoted = hasPromotableProbe && probeEpochMatches
              Cleared = true
              KeptPlan = false }
        | true, Some AttemptOutcome.CompletedInvalid
        | true, Some AttemptOutcome.Failed
        | true, Some AttemptOutcome.Aborted ->
            { Promoted = false
              Cleared = true
              KeptPlan = false }
        | true, None ->
            { Promoted = false
              Cleared = false
              KeptPlan = true }

    let private sessionIdOfOutput (output: obj) : SessionId option =
        ProviderWireDecode.projectionSessionIdFromMessages output
        |> Option.map SessionId.create

    let private requireOk (result: Result<'a, string>) : 'a =
        match result with
        | Ok value -> value
        | Error reason -> raise (InvalidOperationException reason)

    let private ensureFrameDigest (frame: BlogFrame) (text: string) : Result<unit, string> =
        if HostDigest.sha256Hex text = BlobDigest.value frame.Digest then
            Ok()
        else
            Error(sprintf "Companion blob digest mismatch: %s" (BlobDigest.value frame.Digest))

    let private readFrameBody (port: WireJournalPort) (frame: BlogFrame) : Task<Result<string, string>> =
        taskResult {
            let! text = port.ReadBlob frame.TextRef
            do! ensureFrameDigest frame text
            return text
        }

    let private readFrameBodies (port: WireJournalPort) (frames: BlogFrame list) : Task<Result<string list, string>> =
        frames |> TaskResultList.traverseM (readFrameBody port)

    let private readFrames (port: WireJournalPort) (frames: BlogFrame list) : Task<Result<string, string>> =
        task {
            let! bodies = readFrameBodies port frames
            return bodies |> Result.map (fun values -> String.concat "\n\n" values)
        }

    /// Prefix coverage is expressed only in canonical XTrace semantic-turn
    /// coordinates. A positional legacy trace is insufficient proof and fails
    /// closed instead of silently interpreting a provider-array index as history.
    let private providerRetryOrigin =
        PromptAuthority.originLabel (
            PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ProviderRetryAttempt
        )

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

    let retryTransportRetirement (horizon: PrefixPresentationHorizon) (rawMessages: obj list) =
        match horizon with
        | PrefixPresentationHorizon.Current -> Set.empty
        | PrefixPresentationHorizon.TentativeCold -> staleProviderRetryMessageIds rawMessages

    let replacePrefixByHostIds
        (rawMessages: obj list)
        (coveredHostMessageIds: string list)
        (openingHostMessageId: string option)
        (syntheticMessageId: string)
        (memory: string)
        =
        ProjectionMessageEdit.replacePrefixByHostIds
            rawMessages
            coveredHostMessageIds
            openingHostMessageId
            syntheticMessageId
            memory

    let suppressHostMessagesByIds (rawMessages: obj list) (hostMessageIds: Set<string>) =
        ProjectionMessageEdit.suppressHostMessagesByIds rawMessages hostMessageIds

    /// COMPANION-009 / CTX-011: FrozenRecordPrefix = Opening + coverable Y frame
    /// prefix. RawGap never participates — it has no Y coverage proof.
    let private materializeFrozenRecordPrefix
        (port: WireJournalPort)
        (state: WireSessionState)
        (frames: BlogFrame list)
        : Task<Result<string, string>> =
        taskResult {
            let! frameBodies = readFrameBodies port frames

            let opening: XTraceOpeningEvidence =
                state.XTrace
                |> Option.bind XTraceProjection.openingEvidence
                |> Option.defaultValue
                    { AssignmentText = ""
                      AuthoritativeRequirements = []
                      ConstitutiveBody = "" }

            // Same-session FrozenRecordPrefix omits Opening (WORK-RECORD-007):
            // the true raw Opening remains physically present outside the Y
            // replacement. Gap/terminal are live X material and also stay out.
            // Under WORK-RECORD-007, LifecycleWorkRecord.materialize with
            // includeOpening=false produces the headless Chronicle Y prefix.
            return LifecycleWorkRecord.materialize opening frameBodies "" false
        }

    let private candidate
        (port: WireJournalPort)
        (sessionId: SessionId)
        (snapshot: ProjectionSnapshot)
        (committed: PrefixSnapshot option)
        (state: WireSessionState)
        (requestCutoff: int)
        : Task<Result<PrefixProbe, NoCandidateReason>> =
        task {
            let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty
            let blog = state.Blog |> Option.defaultValue BlogProjection.empty

            if not (BlogProjection.hasCoverage blog) then
                return Error NoCandidateReason.NoCoverage
            else
                let frames = BlogProjection.coverableFrames blog
                let! frozenResult = materializeFrozenRecordPrefix port state frames
                let frozenRecordPrefix = requireOk frozenResult
                let! blobResult = port.WriteBlob frozenRecordPrefix
                let blob = requireOk blobResult

                return
                    PrefixProbeSelection.select
                        HostDigest.sha256Hex
                        sessionId
                        prefix.EpochId
                        committed
                        blog.Coverage.CoverableTurnCutoffExclusive
                        blog.Coverage.CoveredPrefixDigest
                        requestCutoff
                        blob.BlobRef
                        blob.BlobDigest
                        (ProjectionRenderer.cutoffDigest HostDigest.sha256Hex snapshot)
        }

    let private readFrozenRecordPrefixBody
        (port: WireJournalPort)
        (choice: XProjectionChoice)
        (committed: PrefixSnapshot option)
        : Task<string> =
        match XPrefixProjection.requiredBlob choice committed with
        | None -> Task.FromResult ""
        | Some blobRef ->
            task {
                let! body = port.ReadBlob blobRef
                return requireOk body
            }

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

    let private applyPrefix (state: WireSessionState) (rawMessages: obj list) (intent: PrefixProjectionIntent) =
        match XPrefixProjection.render intent with
        | PrefixRendered.Physical -> rawMessages
        | PrefixRendered.Synthetic activation ->
            let xTrace = state.XTrace |> Option.defaultValue XTraceProjection.empty

            let replaceableHostMessageIds, openingHostMessageId =
                requireStableReplacement activation xTrace

            ProjectionMessageEdit.replacePrefixByHostIds
                rawMessages
                replaceableHostMessageIds
                openingHostMessageId
                activation.SyntheticMessageId
                activation.Memory

    let private renderPrefixMessages
        (state: WireSessionState)
        (rawMessages: obj list)
        (intent: PrefixProjectionIntent)
        (horizon: PrefixPresentationHorizon)
        =
        // A retry row that was visible in a tentative-cold provider request is
        // now part of that new horizon's physical prefix even though it is not X
        // semantics. Retiring it on the very next ordinary request would shrink
        // the provider wire. Historical retry rows may therefore retire only as
        // part of a later real cold presentation; Current must be byte-preserving.
        let staleTransport = retryTransportRetirement horizon rawMessages

        applyPrefix state rawMessages intent
        |> fun prefixed -> ProjectionMessageEdit.suppressHostMessagesByIds prefixed staleTransport

    let private commitPromotablePrefixRebase
        (port: WireJournalPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (plan: AttemptPlan)
        : Task<Result<unit, string>> =
        task {
            let view = port.ReadView sessionId

            let epoch =
                view.State
                |> Option.bind (fun state -> state.PrefixEpoch)
                |> Option.defaultValue PrefixEpochProjection.empty

            let promotableProbe = AttemptPlanner.promotableProbe plan AttemptOutcome.Completed

            let decision =
                reconciliationDecision
                    true
                    (Some AttemptOutcome.Completed)
                    (Option.isSome promotableProbe)
                    (promotableProbe
                     |> Option.exists (fun probe -> epoch.EpochId = probe.BasedOnEpochId))

            match promotableProbe, decision.Promoted with
            | Some probe, true ->
                let fact =
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

                return! port.CommitPrefixRebase sessionId providerRun fact
            | _ -> return Ok()
        }

    let private recordSuccessfulAttempt
        (port: WireJournalPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (plan: AttemptPlan)
        : Task<Result<unit, string>> =
        if ProviderRequestKind.clearsFailureCountOnSuccess plan.Profile.RequestKind then
            port.RecordConfirmedSuccess sessionId providerRun
        else
            Task.FromResult(Ok())

    let private settleAttemptPlan
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (outcome: AttemptOutcome)
        (plan: AttemptPlan)
        : Task =
        task {
            let! committed =
                match outcome with
                | AttemptOutcome.Completed -> commitPromotablePrefixRebase port sessionId providerRun plan
                | AttemptOutcome.CompletedInvalid
                | AttemptOutcome.Failed
                | AttemptOutcome.Aborted -> Task.FromResult(Ok())

            committed
            |> Result.mapError (fun reason -> sprintf "prefix rebase commit failed: %s" reason)
            |> requireOk
            |> ignore

            let! success =
                match outcome with
                | AttemptOutcome.Completed -> recordSuccessfulAttempt port sessionId providerRun plan
                | AttemptOutcome.CompletedInvalid
                | AttemptOutcome.Failed
                | AttemptOutcome.Aborted -> Task.FromResult(Ok())

            success
            |> Result.mapError (fun reason -> sprintf "provider success commit failed: %s" reason)
            |> requireOk
            |> ignore

            attempts.ConsumeAttemptPlan sessionId providerRun |> ignore
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
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (physical: PhysicalUserMessageId option)
        : Task =
        let plan =
            match attempts.TryAttemptPlan sessionId providerRun with
            | Some existing -> Some existing
            | None ->
                physical
                |> Option.bind (fun parent -> attempts.TryBindAttemptPlan sessionId parent providerRun)

        match plan with
        | None -> Task.FromResult(())
        | Some plan -> settleAttemptPlan port attempts sessionId providerRun AttemptOutcome.Completed plan

    let private settleVisibleToolContinuations
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (rawMessages: obj list)
        : Task =
        task {
            for providerRun, physical in rawMessages |> List.choose toolContinuationBinding |> List.distinct do
                do! settleVisibleToolContinuation port attempts sessionId providerRun physical
        }

    let private applyCommittedPrefix
        (port: WireJournalPort)
        (sessionId: SessionId)
        (state: WireSessionState)
        (rawMessages: obj list)
        (output: obj)
        : Task<unit> =
        task {
            let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty

            match prefix.Snapshot with
            | None ->
                // Even before the first Y epoch exists, XWire still owns the Host
                // transport membrane. Otherwise every ordinary request between
                // failed probes would accumulate all prior ProviderRetryAttempt
                // rows and undo the recovery cleanup.
                let transformed =
                    renderPrefixMessages state rawMessages PrefixProjectionIntent.Keep PrefixPresentationHorizon.Current

                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed
            | Some committed ->
                let choice = XProjectionChoice.UseCommittedEpoch
                let! frozenRecordPrefixBody = readFrozenRecordPrefixBody port choice (Some committed)

                let memoryPreamble =
                    ProviderProse.render (ProviderProse.languageOf sessionId) CompanionPrompt.MemoryPreamble Map.empty

                let intent =
                    XPrefixProjection.forChoice choice (Some committed) memoryPreamble frozenRecordPrefixBody

                let transformed =
                    renderPrefixMessages state rawMessages intent PrefixPresentationHorizon.Current

                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed
        }

    let private applyOrdinaryCommittedPrefix
        (port: WireJournalPort)
        (sessionId: SessionId)
        (state: WireSessionState option)
        (rawMessages: obj list)
        (output: obj)
        : Task =
        match state with
        | None -> raise (InvalidOperationException "X-wire cannot apply a committed prefix without session projection")
        | Some s -> applyCommittedPrefix port sessionId s rawMessages output

    let private authoritySummary (authority: PromptAuthority.AuthorityExecutionProfile) =
        sprintf
            "session=%s logical=%s root=%s kind=%A participant=%s role=%s"
            (SessionId.value authority.SessionId)
            (LogicalRunId.value authority.LogicalRunId)
            (AuthorityRootUserMessageId.value authority.AuthorityRootUserMessageId)
            authority.AuthorityKind
            authority.SelectedAgent
            (Roles.roleLabel authority.CanonicalRole)

    let private requireAdmittedPendingPlan
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        (pendingPlan: PendingAttemptPlan)
        : PendingAttemptPlan =
        match attempts.FreezePendingAttemptPlan sessionId physical pendingPlan with
        | PendingAttemptPlanAdmission.Admitted plan -> plan
        | PendingAttemptPlanAdmission.ReplayedExisting plan -> plan
        | PendingAttemptPlanAdmission.PlanConflict(existing, attempted) when
            PendingAttemptPlanAdmission.sameRequestIdentity existing attempted
            ->
            existing
        | PendingAttemptPlanAdmission.PlanConflict(existing, attempted) ->
            raise (
                InvalidOperationException(
                    sprintf
                        "HOST-BOUNDARY-008: pending attempt plan conflict: existing=(%s) attempted=(%s)"
                        (authoritySummary existing.Authority)
                        (authoritySummary attempted.Authority)
                )
            )
        | PendingAttemptPlanAdmission.IdentityMismatch(expectedSession, expectedPhysical, attempted) ->
            raise (
                InvalidOperationException(
                    sprintf
                        "HOST-BOUNDARY-008: pending attempt plan identity mismatch: expectedSession=%A expectedPhysical=%A attempted=%A"
                        expectedSession
                        expectedPhysical
                        attempted
                )
            )

    let private prepareRetryCandidate
        allowProbe
        (port: WireJournalPort)
        sessionId
        physical
        rawMessages
        (state: WireSessionState)
        =
        if not allowProbe then
            Task.FromResult(Error NoCandidateReason.NoCoverage)
        else
            task {
                let xTrace = state.XTrace |> Option.defaultValue XTraceProjection.empty
                let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty
                let! currentResult = port.CurrentProjection xTrace
                let current = requireOk currentResult
                let cutoff = requestStartCutoff physical rawMessages xTrace
                let snapshot = { CurrentProjection = current }
                return! candidate port sessionId snapshot prefix.Snapshot state cutoff
            }

    let private pendingPlanForRetry
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        : PendingAttemptPlan option =
        match attempts.TryPendingAttemptPlan sessionId physical with
        | Some existing when existing.Authority = authority -> Some existing
        | Some existing ->
            raise (
                InvalidOperationException(
                    sprintf
                        "HOST-BOUNDARY-008: pending attempt plan authority conflict: existing=(%s) current=(%s)"
                        (authoritySummary existing.Authority)
                        (authoritySummary authority)
                )
            )
        | None -> None

    let private planOrBuildRetry
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (failure: ProviderFailureProjection)
        (state: WireSessionState)
        (prefix: ActivePrefixEpoch)
        (existingPlan: PendingAttemptPlan option)
        (rawMessages: obj list)
        : Task<PendingAttemptPlan> =
        match existingPlan with
        | Some existing -> Task.FromResult existing
        | None ->
            task {
                // The retry transport row survives a successful tool step.
                // Its presence does not authorize another cold prefix.
                let allowProbe = mayProbe failure.Budget

                let! candidateResult = prepareRetryCandidate allowProbe port sessionId physical rawMessages state

                let selectProbeForPlan () = candidateResult

                let pendingPlan =
                    AttemptPlanner.freezePreInference
                        authority
                        physical
                        (PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ProviderRetryAttempt)
                        ProviderRequestKind.WorkMain
                        prefix.Snapshot
                        allowProbe
                        selectProbeForPlan

                // Freeze pending plan BEFORE rendering or modifying wire output.
                return requireAdmittedPendingPlan attempts sessionId physical pendingPlan
            }

    let private planProviderRetry
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        (output: obj)
        (rawMessages: obj list)
        : Task<PrefixPresentationHorizon> =
        task {
            let view = port.ReadView sessionId

            match view.ActiveAuthorityProfile, view.ProviderFailureState, view.State with
            | Some authority, Some failure, Some state ->
                let prefix = state.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty

                let existingPlan = pendingPlanForRetry attempts sessionId physical authority

                let! admittedPlan =
                    planOrBuildRetry
                        port
                        attempts
                        sessionId
                        physical
                        authority
                        failure
                        state
                        prefix
                        existingPlan
                        rawMessages

                let presentationHorizon =
                    admittedPlan
                    |> AttemptPlanner.pendingProbeOf
                    |> Option.isSome
                    |> presentationHorizonForProbe

                // `requiredBlob` is the single answer to "which blob does this choice
                // need" — the adapter reads, never guesses (CTX-010: reading the
                // COMMITTED blob for a probe attempt would inject the old prefix under
                // the candidate's id).
                let! frozenRecordPrefixBody =
                    readFrozenRecordPrefixBody port admittedPlan.ProjectionChoice admittedPlan.CommittedPrefixSnapshot

                let memoryPreamble =
                    ProviderProse.render (ProviderProse.languageOf sessionId) CompanionPrompt.MemoryPreamble Map.empty

                let prefixIntent =
                    XPrefixProjection.forChoice
                        admittedPlan.ProjectionChoice
                        admittedPlan.CommittedPrefixSnapshot
                        memoryPreamble
                        frozenRecordPrefixBody

                let transformed =
                    renderPrefixMessages state rawMessages prefixIntent presentationHorizon

                Wanxiangshu.OpenCode.HostMessageProjection.replaceMessagesInPlace output transformed
                return presentationHorizon

            | _ ->
                return
                    raise (
                        InvalidOperationException
                            "X-wire cannot plan a retry without authority, failure, and session projections"
                    )
        }

    let private applyNonReplicaTransform
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
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
            do! settleVisibleToolContinuations port attempts sessionId rawMessages

            let recoveryAttempt =
                physical
                |> Option.filter (fun p ->
                    let physicalId = PhysicalUserMessageId.value p
                    isProviderRetryMessageId physicalId rawMessages)

            match recoveryAttempt with
            | None ->
                let view = port.ReadView sessionId
                do! applyOrdinaryCommittedPrefix port sessionId view.State rawMessages output
                return PrefixPresentationHorizon.Current
            | Some physical -> return! planProviderRetry port attempts sessionId physical output rawMessages
        }

    let private applySessionTransform
        (isReplicaSession: SessionId -> bool)
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
        (sessionId: SessionId)
        (snapshot: ISessionSnapshotPort option)
        (output: obj)
        : Task<PrefixPresentationHorizon> =
        task {
            if isReplicaSession sessionId then
                return PrefixPresentationHorizon.Current
            else
                return! applyNonReplicaTransform port attempts sessionId snapshot output
        }

    let applyTransform
        (isReplicaSession: SessionId -> bool)
        (snapshot: ISessionSnapshotPort option)
        (port: WireJournalPort option)
        (attempts: AttemptPlanCapability)
        (output: obj)
        : Task<PrefixPresentationHorizon> =
        task {
            match port, sessionIdOfOutput output with
            | Some p, Some sessionId when (p.ReadView sessionId).IsCompanion |> not ->
                return! applySessionTransform isReplicaSession p attempts sessionId snapshot output
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
        (port: WireJournalPort)
        (attempts: AttemptPlanCapability)
        (turn: ReconciledTurn)
        (plan: AttemptPlan)
        : Task =
        let outcome = attemptOutcomeOfTurn turn
        let decision = reconciliationDecision true outcome false false

        match outcome, decision.Cleared with
        | Some settled, true -> settleAttemptPlan port attempts turn.SessionId turn.ProviderRun settled plan
        | _ -> Task.FromResult(())

    let private attemptPlanForTurn (attempts: AttemptPlanCapability) (turn: ReconciledTurn) =
        attempts.TryAttemptPlan turn.SessionId turn.ProviderRun
        |> Option.orElseWith (fun () ->
            attempts.TryBindAttemptPlan turn.SessionId turn.PhysicalUserMessageId turn.ProviderRun)

    let private plannedReconciliation
        (port: WireJournalPort option)
        (attempts: AttemptPlanCapability)
        (turn: ReconciledTurn)
        =
        port
        |> Option.bind (fun p -> attemptPlanForTurn attempts turn |> Option.map (fun plan -> p, plan))

    /// Settle the physical provider attempt, not the larger Host turn.
    /// `finish=tool-calls` therefore closes a successful attempt plan while the
    /// Host tool loop continues; only a genuinely provisional snapshot keeps it.
    let reconcileAttempt
        (port: WireJournalPort option)
        (attempts: AttemptPlanCapability)
        (turn: ReconciledTurn)
        : Task =
        match plannedReconciliation port attempts turn with
        | Some(p, plan) -> reconcilePlannedAttempt p attempts turn plan
        | None -> Task.FromResult(())
