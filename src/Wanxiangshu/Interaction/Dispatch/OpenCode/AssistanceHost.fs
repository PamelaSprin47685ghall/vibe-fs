namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Repository.Investigation.Semble

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
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
open Wanxiangshu.Mission.Review
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
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Mission.Finality
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
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

/// AGENT-031 / HOST-027: reconciled-turn assistance workflow.
///
/// The stream sensor only arms/aborts. This owner consumes that exact provider
/// attempt after reconciliation, before recovery/fallback classification. Deep
/// consultation durability reuses EXEC-009 HostOwnedHidden handles rather than a
/// feature-owned persistence tree.
type AssistanceHost
    (
        sessions: ISessionHostPort,
        journal: AgentJournal option,
        sensor: NeedHelpSensor,
        snapshotPort: ISessionSnapshotPort,
        onChildOwned: SessionId -> unit,
        ?clock: IClockPort
    ) =

    [<Literal>]
    let HandlePrefix = "needhelp-"

    let clockPort = defaultArg clock (PtyTiming.nodeClockPort ())
    let consultationAgent = ManagedAgentCatalog.nameOf AgentTier.Deep Role.Inquiry
    let claimGate = obj ()
    // DSL-MUTABLE: single-flight — claimed owner attempt latch
    let claimedOwnerAttempts = HashSet<string>()
    // DSL-MUTABLE: resource — dropped owner set
    let droppedOwners = HashSet<string>()
    // DSL-MUTABLE: subscription — terminal subscription registry per child
    let terminalSubscriptions = Dictionary<string, IDisposable>()

    let assistanceLines (sessionId: SessionId) (path: string) =
        ProviderProse.instructionLines (ProviderProse.languageOf sessionId) path Map.empty

    let consultationAssignment (sessionId: SessionId) =
        ProviderProse.render (ProviderProse.languageOf sessionId) AssistancePrompt.ConsultationPath Map.empty

    let escalationPrompt (sessionId: SessionId) =
        AssistancePrompt.escalation (assistanceLines sessionId AssistancePrompt.EscalationPath)

    let advicePrompt (sessionId: SessionId) (childWorkRecord: string) =
        AssistancePrompt.advice (assistanceLines sessionId AssistancePrompt.ReturnPath) childWorkRecord

    let consultationFailedPrompt (sessionId: SessionId) (reason: string) =
        AssistancePrompt.consultationFailed (assistanceLines sessionId AssistancePrompt.ConsultationFailedPath) reason

    let ownerAttemptKey (turn: ReconciledTurn) =
        SessionId.value turn.SessionId
        + "\u001f"
        + ProviderRunIdentity.value turn.ProviderRun

    let sessionClaimPrefix (sessionId: SessionId) = SessionId.value sessionId + "\u001f"

    let markOwnerClaim (turn: ReconciledTurn) =
        lock claimGate (fun () ->
            let prefix = sessionClaimPrefix turn.SessionId

            claimedOwnerAttempts
            |> Seq.filter (fun key -> key.StartsWith(prefix, StringComparison.Ordinal))
            |> Seq.toArray
            |> Array.iter (fun key -> claimedOwnerAttempts.Remove key |> ignore)

            claimedOwnerAttempts.Add(ownerAttemptKey turn) |> ignore)

    let isClaimedOwnerAttempt (turn: ReconciledTurn) =
        lock claimGate (fun () -> claimedOwnerAttempts.Contains(ownerAttemptKey turn))

    let dropOwnerClaims (sessionId: SessionId) =
        lock claimGate (fun () ->
            let prefix = sessionClaimPrefix sessionId

            claimedOwnerAttempts
            |> Seq.filter (fun key -> key.StartsWith(prefix, StringComparison.Ordinal))
            |> Seq.toArray
            |> Array.iter (fun key -> claimedOwnerAttempts.Remove key |> ignore)

            droppedOwners.Add(SessionId.value sessionId) |> ignore)

    let isOwnerDropped (sessionId: SessionId) =
        lock claimGate (fun () -> droppedOwners.Contains(SessionId.value sessionId))

    let currentProjection () =
        journal |> Option.map AgentJournal.snapshot

    let activeProfile sessionId =
        HostSessionNudge.tryActiveProfile journal sessionId

    let encodeHandleId (owner: SessionId) (logicalRun: LogicalRunId) (requestingAgent: string) =
        let ownerText = SessionId.value owner
        let runText = LogicalRunId.value logicalRun

        sprintf
            "%s%d-%d-%d-%s%s%s"
            HandlePrefix
            ownerText.Length
            runText.Length
            requestingAgent.Length
            ownerText
            runText
            requestingAgent

    let requireFlag (cond: bool) = if cond then Some() else None

    let tryParseNonNeg (text: string) =
        match Int32.TryParse text with
        | true, value when value >= 0 -> Some value
        | _ -> None

    let readLength (text: string) (start: int) =
        option {
            let separator = text.IndexOf('-', start)
            do! requireFlag (separator >= 0)
            let! value = tryParseNonNeg (text.Substring(start, separator - start))
            return value, separator + 1
        }

    let tryDecodeHandleId (agentId: string) =
        option {
            do!
                requireFlag (
                    not (String.IsNullOrWhiteSpace agentId)
                    && agentId.StartsWith(HandlePrefix, StringComparison.Ordinal)
                )

            let rest = agentId.Substring(HandlePrefix.Length)
            let! ownerLength, p1 = readLength rest 0
            let! runLength, p2 = readLength rest p1
            let! agentLength, payloadStart = readLength rest p2
            let expected = ownerLength + runLength + agentLength
            do! requireFlag (payloadStart + expected = rest.Length)
            let ownerText = rest.Substring(payloadStart, ownerLength)
            let runText = rest.Substring(payloadStart + ownerLength, runLength)
            let requester = rest.Substring(payloadStart + ownerLength + runLength, agentLength)
            do! requireFlag (not (String.IsNullOrWhiteSpace ownerText || String.IsNullOrWhiteSpace runText))
            return SessionId.create ownerText, LogicalRunId.create runText, requester
        }

    let tryDecodeRecord (record: HandleRecord) =
        match record.Ownership, record.Handle with
        | HandleOwnership.HostOwnedHidden, HandleId.Agent agentHandle when record.TargetAgent = consultationAgent ->
            let agentId = AgentHandleId.value agentHandle

            tryDecodeHandleId agentId
            |> Option.map (fun (owner, logicalRun, requester) -> agentId, owner, logicalRun, requester)
        | _ -> None

    let childRecord childId =
        currentProjection ()
        |> Option.bind (fun snapshot -> Map.tryFind childId snapshot.AgentProjections.HandleByChildSession)
        |> Option.bind (fun record -> tryDecodeRecord record |> Option.map (fun decoded -> record, decoded))

    let ownerStillOnRun owner logicalRun =
        if isOwnerDropped owner then
            None
        else
            activeProfile owner
            |> Option.filter (fun profile -> profile.LogicalRunId = logicalRun)

    let hasAdviceClaim owner logicalRun =
        match currentProjection (), ownerStillOnRun owner logicalRun with
        | Some snapshot, Some _ ->
            AgentProjection.tryFind owner snapshot.AgentProjections
            |> Option.bind (fun state -> state.PromptAuthority)
            |> Option.exists (fun projection ->
                let pending =
                    projection.PendingClaims
                    |> Map.exists (fun _ claim ->
                        claim.LogicalRunId = Some logicalRun
                        && claim.Origin = PromptAuthority.PromptOrigin.Continuation
                                              PromptAuthority.ContinuationKind.NeedHelpAdvice)

                let accepted =
                    projection.AcceptedContinuationIds
                    |> Map.exists (fun _ kind -> kind = PromptAuthority.ContinuationKind.NeedHelpAdvice)

                pending || accepted)
        | _ -> false

    let toolMap role =
        PromptAuthority.toolCapabilitiesFor role ProviderRequestKind.WorkMain
        |> StaticTools.requestToolMap

    let parentRecord owner =
        LifecycleWorkRecordProjection.lifecycleWorkRecord journal owner true

    let childRecordText child =
        LifecycleWorkRecordProjection.lifecycleWorkRecord journal child false

    let sendContinuation owner logicalRun requester kind prompt directory =
        task {
            match ownerStillOnRun owner logicalRun with
            | None -> return Error "requesting LogicalRun is no longer active"
            | Some _ ->
                return!
                    HostSessionNudge.sendContinuationToAgentResult
                        sessions
                        owner
                        prompt
                        kind
                        requester
                        directory
                        journal
                        PromptDispatcher.AwaitMode.Await
        }

    let consumeCompleted owner (record: HandleRecord) : Task =
        match journal with
        | None -> AsyncSupport.completedTask ()
        | Some durable ->
            task {
                let! _ = HandleController.consume durable owner record.Handle
                ()
            }
            :> Task

    let terminalEvidence succeeded agentId handle childId body =
        if succeeded then
            TerminalEvidence.completed agentId handle childId body
        else
            TerminalEvidence.failed agentId handle childId body

    let commitActiveTerminal owner evidence =
        match JoinableCompletion.tryFromProvenTerminal evidence with
        | Error error -> Task.FromResult(Error error)
        | Ok proof -> ChildRecoveryWorkflow.commitJoinable journal owner proof

    let recordTerminal owner agentId childId (record: HandleRecord) succeeded body : Task<Result<unit, string>> =
        match record.Lifecycle with
        | HandleLifecycle.Active ->
            commitActiveTerminal owner (terminalEvidence succeeded agentId record.Handle childId body)
        | HandleLifecycle.CompletedAwaitingJoin _ -> Task.FromResult(Ok())
        | HandleLifecycle.Retired -> Task.FromResult(Ok())
        | HandleLifecycle.Abandoned _ -> Task.FromResult(Error "consultation handle is abandoned")

    let consumeIfAwaitingJoin owner (record: HandleRecord) =
        match record.Lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin _ -> consumeCompleted owner record
        | _ -> AsyncSupport.completedTask ()

    let deliverAdviceAfterSend owner logicalRun requester childId (record: HandleRecord) providerPrompt directory =
        task {
            match!
                sendContinuation
                    owner
                    logicalRun
                    requester
                    PromptAuthority.ContinuationKind.NeedHelpAdvice
                    providerPrompt
                    directory
            with
            | Ok _ ->
                do! consumeIfAwaitingJoin owner record
                return AssistanceTurnDisposition.Handled
            | Error error ->
                Diagnostic.emit
                    "needhelp"
                    [ "session_id", SessionId.value owner
                      "result", "advice-send-failed"
                      "provider_error", error ]

                return AssistanceTurnDisposition.ClaimedButUnresolved
        }

    let deliverAdvice owner logicalRun requester childId (record: HandleRecord) providerPrompt directory =
        task {
            if hasAdviceClaim owner logicalRun then
                do! consumeIfAwaitingJoin owner record
                return AssistanceTurnDisposition.Handled
            else
                return! deliverAdviceAfterSend owner logicalRun requester childId record providerPrompt directory
        }

    let settleFailedConsultation owner logicalRun agentId childId record requester error =
        let failure = consultationFailedPrompt owner error

        task {
            match! recordTerminal owner agentId childId record false failure with
            | Ok() ->
                let refreshed = childRecord childId |> Option.map fst |> Option.defaultValue record
                let! _ = deliverAdvice owner logicalRun requester childId refreshed failure None
                ()
            | Error _ -> ()
        }
        |> ignore

    let onFailedConsultationTerminal childId error =
        match
            childRecord childId
            |> Option.bind (fun (record, (agentId, owner, logicalRun, requester)) ->
                ownerStillOnRun owner logicalRun
                |> Option.map (fun _ -> record, agentId, owner, logicalRun, requester))
        with
        | None -> ()
        | Some(record, agentId, owner, logicalRun, requester) ->
            settleFailedConsultation owner logicalRun agentId childId record requester error

    let observeChildTerminal childId _session outcome =
        match outcome with
        | TerminalOutcome.Failed stop -> onFailedConsultationTerminal childId stop.Reason
        | _ -> ()

    let registerChildSubscription (childId: SessionId) =
        let key = SessionId.value childId

        lock claimGate (fun () ->
            if not (terminalSubscriptions.ContainsKey key) then
                terminalSubscriptions.[key] <- sessions.SubscribeTerminal(childId, observeChildTerminal childId))

    let trackChildOwned (childId: SessionId) =
        registerChildSubscription childId
        onChildOwned childId

    let sendConsultationRoot owner logicalRun requester childId directory =
        taskResult {
            let! parentOpt = parentRecord owner |> TaskResultCE.ofTask

            let! commissionerRecord =
                parentOpt
                |> Result.requireSome "canonical parent LifecycleWorkRecord unavailable"

            let assignment = consultationAssignment owner
            let lang = ProviderProse.languageOf owner

            let forkProse: ForkChildInstructions =
                { Base = ProviderProse.instructionLines lang ForkChildPayload.BasePath Map.empty
                  CommissionerRecord = ProviderProse.render lang ForkChildPayload.CommissionerRecordPath Map.empty
                  Attachment = ProviderProse.render lang ForkChildPayload.AttachmentPath Map.empty
                  Requirements = ProviderProse.render lang ForkChildPayload.RequirementsPath Map.empty }

            let providerPrompt =
                ForkChildPayload.relay forkProse assignment (Some commissionerRecord) None [] None

            do!
                XTraceCapture.captureOpening journal childId assignment []
                |> TaskResultCE.ofTask

            let! runtime =
                journal
                |> Option.map PromptDispatcher.forJournal
                |> Result.requireSome "No journal: consultation Authority Root cannot be claimed"

            return!
                runtime.SendAgentOwnerRootWithTools
                    sessions
                    childId
                    providerPrompt
                    consultationAgent
                    directory
                    PromptDispatcher.AwaitMode.Detached
                    None
                    (toolMap Role.Inquiry)
                    None
        }

    let existingConsultation owner logicalRun requester =
        match journal with
        | None -> None
        | Some durable ->
            let agentId = encodeHandleId owner logicalRun requester
            let handle = HandleController.agentHandle agentId

            HandleProjection.tryFind handle (AgentJournal.handleProjection durable owner)
            |> Option.map (fun record -> agentId, record)

    let isActiveConsultationFor owner record =
        match tryDecodeRecord record, record.Lifecycle with
        | Some(_, decodedOwner, _, _), (HandleLifecycle.Active | HandleLifecycle.CompletedAwaitingJoin _) when
            decodedOwner = owner
            ->
            true
        | _ -> false

    let ownerHasActiveConsultation owner =
        match currentProjection () with
        | None -> false
        | Some snapshot ->
            snapshot.AgentProjections.HandleByChildSession
            |> Map.exists (fun _ record -> isActiveConsultationFor owner record)

    let adviceFromCompletedChild owner logicalRun requester directory (record: HandleRecord) =
        task {
            match! childRecordText record.ChildSessionId with
            | Some childWorkRecord when not (String.IsNullOrWhiteSpace childWorkRecord) ->
                return!
                    deliverAdvice
                        owner
                        logicalRun
                        requester
                        record.ChildSessionId
                        record
                        (advicePrompt owner childWorkRecord)
                        directory
            | _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
        }

    let sendExhaustedAdvice owner logicalRun requester directory =
        let exhausted =
            consultationFailedPrompt
                owner
                "another consultation is unavailable for this run; continue the original charge with the evidence you have"

        task {
            match!
                sendContinuation
                    owner
                    logicalRun
                    requester
                    PromptAuthority.ContinuationKind.NeedHelpAdvice
                    exhausted
                    directory
            with
            | Ok _ -> return AssistanceTurnDisposition.Handled
            | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
        }

    let recoverExistingConsultation owner logicalRun requester directory (_agentId: string) (record: HandleRecord) =
        // Finite per-LogicalRun guard. Reuse/recover the one consultation
        // resource; once it is spent, the owner still receives a bounded
        // continuation rather than being left aborted with no successor.
        trackChildOwned record.ChildSessionId

        match record.Lifecycle with
        | HandleLifecycle.Active -> Task.FromResult AssistanceTurnDisposition.Handled
        | HandleLifecycle.CompletedAwaitingJoin _ ->
            adviceFromCompletedChild owner logicalRun requester directory record
        | HandleLifecycle.Retired
        | HandleLifecycle.Abandoned _ -> sendExhaustedAdvice owner logicalRun requester directory

    let afterConsultationLinked owner logicalRun requester directory childId =
        trackChildOwned childId

        task {
            match! sendConsultationRoot owner logicalRun requester childId directory with
            | Ok _ -> return AssistanceTurnDisposition.Handled
            | Error error ->
                Diagnostic.emit
                    "needhelp"
                    [ "session_id", SessionId.value owner
                      "result", "consultation-send-failed"
                      "provider_error", error ]

                return AssistanceTurnDisposition.ClaimedButUnresolved
        }

    let linkConsultationHandle owner agentId childId =
        task {
            match!
                HandleController.link
                    journal
                    owner
                    agentId
                    childId
                    consultationAgent
                    Role.Inquiry
                    HandleOwnership.HostOwnedHidden
            with
            | Ok() -> return Ok()
            | Error error ->
                sessions.AbortSession childId |> ignore
                return Error error
        }

    let afterConsultationChildCreated owner logicalRun requester directory agentId childId =
        task {
            match! linkConsultationHandle owner agentId childId with
            | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | Ok() -> return! afterConsultationLinked owner logicalRun requester directory childId
        }

    let startFreshConsultationWithParent owner logicalRun requester directory =
        let agentId = encodeHandleId owner logicalRun requester

        task {
            match!
                sessions.CreateChildSession(
                    owner,
                    { Title = Some "needhelp-consultation"
                      Agent = Some consultationAgent
                      Directory = directory }
                )
            with
            | Error error ->
                Diagnostic.emit
                    "needhelp"
                    [ "session_id", SessionId.value owner
                      "result", "consultation-create-failed"
                      "provider_error", error ]

                return AssistanceTurnDisposition.ClaimedButUnresolved
            | Ok childId -> return! afterConsultationChildCreated owner logicalRun requester directory agentId childId
        }

    let startFreshConsultation owner logicalRun requester directory =
        task {
            match! parentRecord owner with
            | None -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | Some _ -> return! startFreshConsultationWithParent owner logicalRun requester directory
        }

    let beginConsultation owner logicalRun requester directory =
        task {
            match existingConsultation owner logicalRun requester with
            | Some(agentId, record) ->
                return! recoverExistingConsultation owner logicalRun requester directory agentId record
            | None when ownerHasActiveConsultation owner -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | None -> return! startFreshConsultation owner logicalRun requester directory
        }

    let agentFromMessage (message: SessionMessage) =
        match message.Agent with
        | Some agent when not (String.IsNullOrWhiteSpace agent) -> Ok(agent.Trim())
        | _ -> Error "requesting provider run has no managed agent binding"

    let requestingAgentFor (turn: ReconciledTurn) =
        taskResult {
            let! messages = snapshotPort.GetMessages turn.SessionId
            let run = ProviderRunIdentity.value turn.ProviderRun

            let! message =
                messages
                |> List.tryFind (fun message ->
                    String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    && message.Id = run)
                |> Result.requireSome "requesting provider run is absent from reconciled Host snapshot"

            return! agentFromMessage message
        }

    let sendEscalationContinuation (turn: ReconciledTurn) (profile: PromptAuthority.AuthorityExecutionProfile) role =
        let deep = ManagedAgentCatalog.nameOf AgentTier.Deep role

        task {
            match!
                sendContinuation
                    turn.SessionId
                    profile.LogicalRunId
                    deep
                    PromptAuthority.ContinuationKind.NeedHelpEscalation
                    (escalationPrompt turn.SessionId)
                    turn.Directory
            with
            | Ok _ -> return AssistanceTurnDisposition.Handled
            | Error error ->
                Diagnostic.emit
                    "needhelp"
                    [ "session_id", SessionId.value turn.SessionId
                      "result", "escalation-send-failed"
                      "provider_error", error ]

                return AssistanceTurnDisposition.ClaimedButUnresolved
        }

    /// HOST-027 / AGENT-031: every assistance successor shares one physical
    /// admission law. AbortWake only claims the ProviderRun; it never sends a
    /// continuation or creates a child while OpenCode may still be sweeping the
    /// aborted attempt. A fresh SessionIdle revisit is the transport fence. The
    /// NEEDHELP arm is consumed only behind that fence, so ordinary fallback can
    /// never race a half-landed assistance continuation.
    /// HOST-027: owning CE holds the one-shot AssistanceAbortClaim.
    /// The typed claim is consumed exactly once behind the idle fence (SW-017 ②).
    /// No separate IsArmed presence probe; the claim itself is the capability.
    let withFreshAssistanceQuiescence
        (context: ReconciledTurnContext)
        (assistanceClaim: AssistanceAbortClaim)
        (continueAfterIdle: unit -> Task<AssistanceTurnDisposition>)
        =
        markOwnerClaim context.Turn

        match context.Quiescence with
        | None -> Task.FromResult AssistanceTurnDisposition.Handled
        | Some _ ->
            task {
                match
                    sensor.TryConsumeAssistanceClaim(
                        AssistanceAbortClaim.sessionId assistanceClaim,
                        AssistanceAbortClaim.providerRun assistanceClaim
                    )
                with
                | None -> return AssistanceTurnDisposition.Handled
                | Some _ -> return! continueAfterIdle ()
            }

    let escalateFastOwnerRequest
        (context: ReconciledTurnContext)
        (assistanceClaim: AssistanceAbortClaim)
        (turn: ReconciledTurn)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        role
        =
        withFreshAssistanceQuiescence context assistanceClaim (fun () -> sendEscalationContinuation turn profile role)

    let beginDeepOwnerConsultation
        (context: ReconciledTurnContext)
        (assistanceClaim: AssistanceAbortClaim)
        (turn: ReconciledTurn)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        requester
        =
        withFreshAssistanceQuiescence context assistanceClaim (fun () ->
            beginConsultation turn.SessionId profile.LogicalRunId requester turn.Directory)

    let handleParsedOwnerRequest
        (context: ReconciledTurnContext)
        (assistanceClaim: AssistanceAbortClaim)
        (turn: ReconciledTurn)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        requestingAgent
        =
        match PromptAuthority.parseAgentName requestingAgent with
        | Error _ -> Task.FromResult AssistanceTurnDisposition.ClaimedButUnresolved
        | Ok(_, role, _, _) when role <> profile.CanonicalRole ->
            Task.FromResult AssistanceTurnDisposition.ClaimedButUnresolved
        | Ok(_, role, AgentTier.Fast, _) -> escalateFastOwnerRequest context assistanceClaim turn profile role
        | Ok(requester, _, AgentTier.Deep, _) ->
            beginDeepOwnerConsultation context assistanceClaim turn profile requester

    let handleOwnerRequestForProfile
        (context: ReconciledTurnContext)
        (assistanceClaim: AssistanceAbortClaim)
        (turn: ReconciledTurn)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        =
        task {
            match! requestingAgentFor turn with
            | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | Ok requestingAgent ->
                return! handleParsedOwnerRequest context assistanceClaim turn profile requestingAgent
        }

    let handleOwnerRequest (context: ReconciledTurnContext) (assistanceClaim: AssistanceAbortClaim) =
        let turn = context.Turn

        task {
            match activeProfile turn.SessionId with
            | None -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | Some profile when profile.AuthorityRootUserMessageId <> turn.AuthorityRootUserMessageId ->
                return AssistanceTurnDisposition.ClaimedButUnresolved
            | Some profile -> return! handleOwnerRequestForProfile context assistanceClaim turn profile
        }

    let finalizeConsultationAdvice owner logicalRun requester childId (record: HandleRecord) providerPrompt directory =
        let refreshed = childRecord childId |> Option.map fst |> Option.defaultValue record
        deliverAdvice owner logicalRun requester childId refreshed providerPrompt directory

    let finalizeConsultationFailure
        owner
        agentId
        childId
        (record: HandleRecord)
        logicalRun
        requester
        directory
        failure
        =
        task {
            match! recordTerminal owner agentId childId record false failure with
            | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | Ok() -> return! finalizeConsultationAdvice owner logicalRun requester childId record failure directory
        }

    let finalizeConsultationSuccess
        owner
        agentId
        childId
        (record: HandleRecord)
        logicalRun
        requester
        directory
        childWorkRecord
        =
        task {
            match! recordTerminal owner agentId childId record true childWorkRecord with
            | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | Ok() ->
                return!
                    finalizeConsultationAdvice
                        owner
                        logicalRun
                        requester
                        childId
                        record
                        (advicePrompt owner childWorkRecord)
                        directory
        }

    let completeConsultationTurn (turn: ReconciledTurn) (record: HandleRecord) agentId owner logicalRun requester =
        task {
            // Assistance consumes this hidden child before ordinary
            // TurnWorkflow reaches TerminalReporter; HostTurnObserver
            // returns immediately after Handled, so the reconciled
            // terminal must be materialized here. Persist the exact
            // terminal segment first so the child LWR includes this
            // completed turn's advice, not only its opening Chronicle
            // or older XTrace material.
            do! XTraceCapture.captureTerminal journal turn
            let! body = childRecordText turn.SessionId
            let body = body |> Option.filter (String.IsNullOrWhiteSpace >> not)

            match body with
            | None ->
                let failure =
                    consultationFailedPrompt owner "canonical child LifecycleWorkRecord unavailable"

                return!
                    finalizeConsultationFailure
                        owner
                        agentId
                        turn.SessionId
                        record
                        logicalRun
                        requester
                        turn.Directory
                        failure
            | Some childWorkRecord ->
                return!
                    finalizeConsultationSuccess
                        owner
                        agentId
                        turn.SessionId
                        record
                        logicalRun
                        requester
                        turn.Directory
                        childWorkRecord
        }

    let recordRecursiveAbortFailure owner agentId childId record failure =
        task {
            let! _ = recordTerminal owner agentId childId record false failure
            ()
        }

    let abortConsultationTurn (turn: ReconciledTurn) (record: HandleRecord) agentId owner logicalRun requester reason =
        // If the consultation itself asks for help, consume that arm but
        // never recurse into another consultation. The typed control
        // cause proves this consultation attempt is unusable; ordinary
        // external abort remains a bounded failure advice without being
        // written as provider-failure evidence.
        let recursiveClaim =
            sensor.TryConsumeAssistanceClaim(turn.SessionId, turn.ProviderRun)

        let recursive = recursiveClaim.IsSome

        let failureReason =
            if recursive then
                "recursive NEEDHELP from consultation is not allowed"
            else
                "consultation aborted: " + reason

        let failure = consultationFailedPrompt owner failureReason

        task {
            if recursive then
                do! recordRecursiveAbortFailure owner agentId turn.SessionId record failure

            return! finalizeConsultationAdvice owner logicalRun requester turn.SessionId record failure turn.Directory
        }

    let handleConsultationWhileOwnerActive
        (turn: ReconciledTurn)
        (record: HandleRecord)
        agentId
        owner
        logicalRun
        requester
        =
        match turn.Outcome with
        | ReconcileProgram.TurnCompleted -> completeConsultationTurn turn record agentId owner logicalRun requester
        | ReconcileProgram.TurnFailed _ ->
            // Provider-attempt failure remains child-local and follows the
            // ordinary A/A/B/B recovery path. Assistance does not turn one
            // failed attempt into consultation failure while retries can continue.
            Task.FromResult AssistanceTurnDisposition.NotAssistance
        | ReconcileProgram.TurnAborted reason ->
            abortConsultationTurn turn record agentId owner logicalRun requester reason
        | ReconcileProgram.TurnNeedsContinuation _
        | ReconcileProgram.TurnInProgress -> Task.FromResult AssistanceTurnDisposition.NotAssistance

    let handleConsultationTurn
        (turn: ReconciledTurn)
        (record: HandleRecord)
        (decoded: string * SessionId * LogicalRunId * string)
        =
        let agentId, owner, logicalRun, requester = decoded

        task {
            match ownerStillOnRun owner logicalRun with
            | None ->
                // Owner was cancelled/retired or accepted a newer Authority Root.
                // A late child terminal may not resurrect it.
                return AssistanceTurnDisposition.Handled
            | Some _ -> return! handleConsultationWhileOwnerActive turn record agentId owner logicalRun requester
        }

    let handleOwnerSideTurn (context: ReconciledTurnContext) (turn: ReconciledTurn) =
        let isClaimed = isClaimedOwnerAttempt turn

        let assistanceClaim =
            sensor.TryObserveAssistanceClaim(turn.SessionId, turn.ProviderRun)

        match turn.Outcome, assistanceClaim, context.Quiescence with
        | ReconcileProgram.TurnAborted _, Some claim, _
        | _, Some claim, Some _ -> handleOwnerRequest context claim
        | (ReconcileProgram.TurnAborted _, None, _ | ReconcileProgram.TurnFailed _, None, _) when isClaimed ->
            // HOST-027: once exact NEEDHELP has claimed this physical owner
            // ProviderRun, later Host terminal views of that SAME run cannot
            // reclassify it as provider failure. OpenCode may surface Abort and
            // Failure wakes for one cancelled transport; ownership is by typed
            // (SessionId, ProviderRun), not by whichever terminal label arrives
            // last. Consultation-child TurnFailed remains child-local above.
            Task.FromResult AssistanceTurnDisposition.Handled
        | _ -> Task.FromResult AssistanceTurnDisposition.NotAssistance

    let activeConsultationAbort (sessionId: SessionId) (childId: SessionId) (record: HandleRecord) =
        match tryDecodeRecord record, record.Lifecycle with
        | Some(agentId, owner, _, _), (HandleLifecycle.Active | HandleLifecycle.CompletedAwaitingJoin _) when
            owner = sessionId
            ->
            sessions.AbortSession childId |> ignore
            Some(agentId, owner)
        | _ -> None

    let dropSignalSubscriptions (sessionId: SessionId) =
        lock claimGate (fun () ->
            let key = SessionId.value sessionId

            match terminalSubscriptions.TryGetValue key with
            | true, sub ->
                sub.Dispose()
                terminalSubscriptions.Remove key |> ignore
            | false, _ -> ())

    /// Reconcile one stable turn before fallback/recovery ownership.
    member _.HandleTurn(context: ReconciledTurnContext) : Task<AssistanceTurnDisposition> =
        let turn = context.Turn

        task {
            match childRecord turn.SessionId with
            | Some(record, decoded) -> return! handleConsultationTurn turn record decoded
            | None -> return! handleOwnerSideTurn context turn
        }

    /// Synchronous signal-plane teardown. Parent cancellation uses this before its
    /// own durable handle cancellation CE; no durable work is started here.
    member _.DropSignals(sessionId: SessionId) =
        sensor.DropSession sessionId
        dropOwnerClaims sessionId
        dropSignalSubscriptions sessionId

    /// Owner deletion closes its active consultation resource. Child deletion
    /// alone only removes stream state; the durable handle remains the recovery truth.
    /// Physical abort is issued immediately; the returned Task owns durable abandon
    /// completion and must be awaited before Journal/store teardown.
    member this.DropSession(sessionId: SessionId) : Task =
        this.DropSignals sessionId

        let active =
            match currentProjection () with
            | None -> []
            | Some snapshot ->
                snapshot.AgentProjections.HandleByChildSession
                |> Map.toList
                |> List.choose (fun (childId, record) -> activeConsultationAbort sessionId childId record)

        task {
            for agentId, owner in active do
                let! _ =
                    HandleController.recordAbandon
                        journal
                        owner
                        agentId
                        HandleAbandonReason.ParentCancelled
                        (clockPort.UtcNow())

                ()
        }
        :> Task

[<RequireQualifiedAccess>]
module AssistanceHostWiring =

    /// HOST-027 owner composition. The Host root supplies runtime resources and
    /// receives the stream sensor it must route raw events into; eligibility,
    /// interruption and assistance lifecycle wiring remain owned here.
    let install
        (needHelpSensor: NeedHelpSensor)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort)
        (scope: PluginRuntimeScope)
        =
        scope.AttachNeedHelpSensor needHelpSensor

        let assistance =
            AssistanceHost(
                sessionPort,
                journal,
                needHelpSensor,
                snapshot,
                (fun childId -> scope.Sessions.OwnedSessions.Add(SessionId.value childId) |> ignore)
            )

        scope.AttachAssistance(assistance.HandleTurn, assistance.DropSignals, assistance.DropSession)
        needHelpSensor
