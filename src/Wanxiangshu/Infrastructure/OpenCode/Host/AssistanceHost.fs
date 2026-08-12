namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Finality
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process
open Wanxiangshu.Session
open Wanxiangshu.Tools

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
    let claimedOwnerAttempts = HashSet<string>()

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
            |> Array.iter (fun key -> claimedOwnerAttempts.Remove key |> ignore))

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

    let tryDecodeHandleId (agentId: string) =
        if
            String.IsNullOrWhiteSpace agentId
            || not (agentId.StartsWith(HandlePrefix, StringComparison.Ordinal))
        then
            None
        else
            let rest = agentId.Substring(HandlePrefix.Length)

            let readLength (text: string) (start: int) =
                let separator = text.IndexOf('-', start)

                if separator < 0 then
                    None
                else
                    match Int32.TryParse(text.Substring(start, separator - start)) with
                    | true, value when value >= 0 -> Some(value, separator + 1)
                    | _ -> None

            match readLength rest 0 with
            | None -> None
            | Some(ownerLength, p1) ->
                match readLength rest p1 with
                | None -> None
                | Some(runLength, p2) ->
                    match readLength rest p2 with
                    | None -> None
                    | Some(agentLength, payloadStart) ->
                        let expected = ownerLength + runLength + agentLength

                        if payloadStart + expected <> rest.Length then
                            None
                        else
                            let ownerText = rest.Substring(payloadStart, ownerLength)
                            let runText = rest.Substring(payloadStart + ownerLength, runLength)
                            let requester = rest.Substring(payloadStart + ownerLength + runLength, agentLength)

                            if String.IsNullOrWhiteSpace ownerText || String.IsNullOrWhiteSpace runText then
                                None
                            else
                                Some(SessionId.create ownerText, LogicalRunId.create runText, requester)

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

    let childHasRootClaimOrProfile childId =
        match currentProjection () with
        | None -> false
        | Some snapshot ->
            let active =
                PromptAuthorityLedger.activeProfile childId snapshot.AgentProjections
                |> Option.isSome

            let pendingRoot =
                AgentProjection.tryFind childId snapshot.AgentProjections
                |> Option.bind (fun state -> state.PromptAuthority)
                |> Option.exists (fun projection ->
                    projection.PendingClaims
                    |> Map.exists (fun _ claim ->
                        claim.Origin = PromptAuthority.PromptOrigin.AuthorityRoot
                                           PromptAuthority.RootAuthorityKind.AgentOwnerRoot))

            active || pendingRoot

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
                        PromptDispatcher.AwaitMode.Detached
        }

    let consumeCompleted owner (record: HandleRecord) =
        match journal with
        | None -> ()
        | Some durable ->
            match HandleController.consume durable owner record.Handle with
            | Ok _
            | Error HandleConsumeRejection.AlreadyRetired -> ()
            | Error _ -> ()

    let recordTerminal owner agentId childId (record: HandleRecord) succeeded body =
        match record.Lifecycle with
        | HandleLifecycle.Active ->
            let evidence =
                if succeeded then
                    TerminalEvidence.completed agentId record.Handle childId body
                else
                    TerminalEvidence.failed agentId record.Handle childId body

            match JoinableCompletion.tryFromProvenTerminal evidence with
            | Error error -> Error error
            | Ok proof -> ChildRecoveryWorkflow.commitJoinable journal owner proof
        | HandleLifecycle.CompletedAwaitingJoin _ -> Ok()
        | HandleLifecycle.Retired -> Ok()
        | HandleLifecycle.Abandoned _ -> Error "consultation handle is abandoned"

    let deliverAdvice owner logicalRun requester childId (record: HandleRecord) providerPrompt directory =
        task {
            if hasAdviceClaim owner logicalRun then
                match record.Lifecycle with
                | HandleLifecycle.CompletedAwaitingJoin _ -> consumeCompleted owner record
                | _ -> ()

                return AssistanceTurnDisposition.Handled
            else
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
                    match record.Lifecycle with
                    | HandleLifecycle.CompletedAwaitingJoin _ -> consumeCompleted owner record
                    | _ -> ()

                    return AssistanceTurnDisposition.Handled
                | Error error ->
                    Diagnostic.emit
                        "needhelp"
                        [ "session_id", SessionId.value owner
                          "result", "advice-send-failed"
                          "provider_error", error ]

                    return AssistanceTurnDisposition.ClaimedButUnresolved
        }

    let sendConsultationRoot owner logicalRun requester childId directory =
        task {
            match parentRecord owner with
            | None -> return Error "canonical parent LifecycleWorkRecord unavailable"
            | Some commissionerRecord ->
                let assignment = AssistancePrompt.consultationAssignment

                let providerPrompt =
                    ForkChildPayload.relay assignment (Some commissionerRecord) [] None

                XTraceCapture.captureOpening journal childId assignment []

                let dispatcher = journal |> Option.map PromptDispatcher.forJournal

                match dispatcher with
                | None -> return Error "No journal: consultation Authority Root cannot be claimed"
                | Some runtime ->
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

    let ownerHasActiveConsultation owner =
        match currentProjection () with
        | None -> false
        | Some snapshot ->
            snapshot.AgentProjections.HandleByChildSession
            |> Map.exists (fun _ record ->
                match tryDecodeRecord record with
                | Some(_, decodedOwner, _, _) when decodedOwner = owner ->
                    match record.Lifecycle with
                    | HandleLifecycle.Active
                    | HandleLifecycle.CompletedAwaitingJoin _ -> true
                    | HandleLifecycle.Abandoned _
                    | HandleLifecycle.Retired -> false
                | _ -> false)

    let beginConsultation owner logicalRun requester directory =
        task {
            match existingConsultation owner logicalRun requester with
            | Some(_, record) ->
                // Finite per-LogicalRun guard. Reuse/recover the one consultation
                // resource; once it is spent, the owner still receives a bounded
                // continuation rather than being left aborted with no successor.
                onChildOwned record.ChildSessionId

                match record.Lifecycle with
                | HandleLifecycle.Active -> return AssistanceTurnDisposition.Handled
                | HandleLifecycle.CompletedAwaitingJoin _ ->
                    match childRecordText record.ChildSessionId with
                    | Some childWorkRecord when not (String.IsNullOrWhiteSpace childWorkRecord) ->
                        return!
                            deliverAdvice
                                owner
                                logicalRun
                                requester
                                record.ChildSessionId
                                record
                                (AssistancePrompt.advice childWorkRecord)
                                directory
                    | _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
                | HandleLifecycle.Retired
                | HandleLifecycle.Abandoned _ ->
                    let exhausted =
                        AssistancePrompt.consultationFailed
                            "another consultation is unavailable for this run; continue the original charge with the evidence you have"

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
            | None when ownerHasActiveConsultation owner -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | None ->
                match parentRecord owner with
                | None -> return AssistanceTurnDisposition.ClaimedButUnresolved
                | Some _ ->
                    let agentId = encodeHandleId owner logicalRun requester

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
                    | Ok childId ->
                        match
                            HandleController.link
                                journal
                                owner
                                agentId
                                childId
                                consultationAgent
                                Role.Inquiry
                                HandleOwnership.HostOwnedHidden
                        with
                        | Error error ->
                            sessions.AbortSession childId |> ignore
                            return AssistanceTurnDisposition.ClaimedButUnresolved
                        | Ok() ->
                            onChildOwned childId

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

    let requestingAgentFor (turn: ReconciledTurn) =
        task {
            match! snapshotPort.GetMessages turn.SessionId with
            | Error error -> return Error error
            | Ok messages ->
                let run = ProviderRunIdentity.value turn.ProviderRun

                match
                    messages
                    |> List.tryFind (fun message ->
                        String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                        && message.Id = run)
                with
                | Some message ->
                    match message.Agent with
                    | Some agent when not (String.IsNullOrWhiteSpace agent) -> return Ok(agent.Trim())
                    | _ -> return Error "requesting provider run has no managed agent binding"
                | None -> return Error "requesting provider run is absent from reconciled Host snapshot"
        }

    let handleOwnerRequest (context: ReconciledTurnContext) =
        task {
            let turn = context.Turn

            match activeProfile turn.SessionId with
            | None -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | Some profile when profile.AuthorityRootUserMessageId <> turn.AuthorityRootUserMessageId ->
                return AssistanceTurnDisposition.ClaimedButUnresolved
            | Some profile ->
                match! requestingAgentFor turn with
                | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
                | Ok requestingAgent ->
                    match PromptAuthority.parseAgentName requestingAgent with
                    | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
                    | Ok(requester, role, _, _) when role <> profile.CanonicalRole ->
                        return AssistanceTurnDisposition.ClaimedButUnresolved
                    | Ok(_, role, AgentTier.Fast, _) ->
                        markOwnerClaim turn

                        if not (sensor.TryTake(turn.SessionId, turn.ProviderRun)) then
                            return AssistanceTurnDisposition.Handled
                        else
                            let deep = ManagedAgentCatalog.nameOf AgentTier.Deep role

                            match!
                                sendContinuation
                                    turn.SessionId
                                    profile.LogicalRunId
                                    deep
                                    PromptAuthority.ContinuationKind.NeedHelpEscalation
                                    AssistancePrompt.escalation
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
                    | Ok(requester, _, AgentTier.Deep, _) ->
                        // OpenCode may still be completing the physical parent-abort
                        // descendant sweep when AbortWake reconciles. Claim ownership
                        // now, but do not parent a fresh consultation under that session
                        // until a fresh SessionIdle permit proves the abort is quiescent.
                        markOwnerClaim turn

                        match context.Quiescence with
                        | None -> return AssistanceTurnDisposition.Handled
                        | Some _ ->
                            // The permit itself remains revoked by HOST-004 after an
                            // operator abort and is intentionally not consumed here.
                            // Its presence proves this delivery came from a fresh
                            // SessionIdle wake, which is the transport fence needed
                            // before parenting a new physical child under the aborted
                            // owner session.
                            if not (sensor.TryTake(turn.SessionId, turn.ProviderRun)) then
                                return AssistanceTurnDisposition.Handled
                            else
                                return! beginConsultation turn.SessionId profile.LogicalRunId requester turn.Directory
        }

    let handleConsultationTurn
        (turn: ReconciledTurn)
        (record: HandleRecord)
        (decoded: string * SessionId * LogicalRunId * string)
        =
        task {
            let agentId, owner, logicalRun, requester = decoded

            match ownerStillOnRun owner logicalRun with
            | None ->
                // Owner was cancelled/retired or accepted a newer Authority Root.
                // A late child terminal may not resurrect it.
                return AssistanceTurnDisposition.Handled
            | Some _ ->
                match turn.Outcome with
                | ReconcileProgram.TurnCompleted ->
                    // Assistance consumes this hidden child before ordinary
                    // TurnWorkflow reaches TerminalReporter; HostTurnObserver
                    // returns immediately after Handled, so the reconciled
                    // terminal must be materialized here. Persist the exact
                    // terminal segment first so the child LWR includes this
                    // completed turn's advice, not only its opening Chronicle
                    // or older XTrace material.
                    XTraceCapture.captureTerminal journal turn

                    let body =
                        childRecordText turn.SessionId
                        |> Option.filter (String.IsNullOrWhiteSpace >> not)

                    match body with
                    | None ->
                        let failure =
                            AssistancePrompt.consultationFailed "canonical child LifecycleWorkRecord unavailable"

                        match recordTerminal owner agentId turn.SessionId record false failure with
                        | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
                        | Ok() ->
                            let refreshed =
                                childRecord turn.SessionId |> Option.map fst |> Option.defaultValue record

                            return!
                                deliverAdvice owner logicalRun requester turn.SessionId refreshed failure turn.Directory
                    | Some childWorkRecord ->
                        match recordTerminal owner agentId turn.SessionId record true childWorkRecord with
                        | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
                        | Ok() ->
                            let refreshed =
                                childRecord turn.SessionId |> Option.map fst |> Option.defaultValue record

                            return!
                                deliverAdvice
                                    owner
                                    logicalRun
                                    requester
                                    turn.SessionId
                                    refreshed
                                    (AssistancePrompt.advice childWorkRecord)
                                    turn.Directory

                | ReconcileProgram.TurnFailed error ->
                    let failure = AssistancePrompt.consultationFailed error

                    match recordTerminal owner agentId turn.SessionId record false failure with
                    | Error _ -> return AssistanceTurnDisposition.ClaimedButUnresolved
                    | Ok() ->
                        let refreshed =
                            childRecord turn.SessionId |> Option.map fst |> Option.defaultValue record

                        return! deliverAdvice owner logicalRun requester turn.SessionId refreshed failure turn.Directory

                | ReconcileProgram.TurnAborted reason ->
                    // If the consultation itself asks for help, consume that arm but
                    // never recurse into another consultation. The typed control
                    // cause proves this consultation attempt is unusable; ordinary
                    // external abort remains a bounded failure advice without being
                    // written as provider-failure evidence.
                    let recursive = sensor.TryTake(turn.SessionId, turn.ProviderRun)

                    let failureReason =
                        if recursive then
                            "recursive NEEDHELP from consultation is not allowed"
                        else
                            "consultation aborted: " + reason

                    let failure = AssistancePrompt.consultationFailed failureReason

                    if recursive then
                        match recordTerminal owner agentId turn.SessionId record false failure with
                        | Ok() -> ()
                        | Error _ -> ()

                    let refreshed =
                        childRecord turn.SessionId |> Option.map fst |> Option.defaultValue record

                    return! deliverAdvice owner logicalRun requester turn.SessionId refreshed failure turn.Directory

                | ReconcileProgram.TurnNeedsContinuation _
                | ReconcileProgram.TurnInProgress -> return AssistanceTurnDisposition.NotAssistance
        }

    /// Reconcile one stable turn before fallback/recovery ownership.
    member _.HandleTurn(context: ReconciledTurnContext) : Task<AssistanceTurnDisposition> =
        task {
            let turn = context.Turn

            match childRecord turn.SessionId with
            | Some(record, decoded) -> return! handleConsultationTurn turn record decoded
            | None ->
                match turn.Outcome with
                | ReconcileProgram.TurnAborted _ when sensor.IsArmed(turn.SessionId, turn.ProviderRun) ->
                    return! handleOwnerRequest context
                | ReconcileProgram.TurnAborted _ when isClaimedOwnerAttempt turn ->
                    return AssistanceTurnDisposition.Handled
                | _ -> return AssistanceTurnDisposition.NotAssistance
        }

    /// Bootstrap recovery: re-register hidden consultation children; never mint
    /// a replacement. Completed cells may finish advice delivery, while an active
    /// child with no Authority Root replays the same deterministic opening send.
    member _.Recover() : Task =
        task {
            match currentProjection () with
            | None -> ()
            | Some snapshot ->
                for KeyValue(childId, record) in snapshot.AgentProjections.HandleByChildSession do
                    match tryDecodeRecord record with
                    | None -> ()
                    | Some(agentId, owner, logicalRun, requester) ->
                        onChildOwned childId

                        match ownerStillOnRun owner logicalRun, record.Lifecycle with
                        | None, _ -> ()
                        | Some _, HandleLifecycle.Retired
                        | Some _, HandleLifecycle.Abandoned _ -> ()
                        | Some _, HandleLifecycle.CompletedAwaitingJoin _ ->
                            match childRecordText childId with
                            | Some childWorkRecord when not (String.IsNullOrWhiteSpace childWorkRecord) ->
                                let! _ =
                                    deliverAdvice
                                        owner
                                        logicalRun
                                        requester
                                        childId
                                        record
                                        (AssistancePrompt.advice childWorkRecord)
                                        None

                                ()
                            | _ -> ()
                        | Some _, HandleLifecycle.Active ->
                            if
                                not (hasAdviceClaim owner logicalRun)
                                && not (childHasRootClaimOrProfile childId)
                            then
                                let! _ = sendConsultationRoot owner logicalRun requester childId None
                                ()
        }
        :> Task

    /// Owner cancellation closes its active consultation resource. Child deletion
    /// alone only removes stream state; the durable handle remains the recovery truth.
    member _.DropSession(sessionId: SessionId) =
        sensor.DropSession sessionId
        dropOwnerClaims sessionId

        match currentProjection () with
        | None -> ()
        | Some snapshot ->
            snapshot.AgentProjections.HandleByChildSession
            |> Map.iter (fun childId record ->
                match tryDecodeRecord record with
                | Some(agentId, owner, _, _) when owner = sessionId ->
                    match record.Lifecycle with
                    | HandleLifecycle.Active
                    | HandleLifecycle.CompletedAwaitingJoin _ ->
                        sessions.AbortSession childId |> ignore

                        HandleController.recordAbandon
                            journal
                            owner
                            agentId
                            HandleAbandonReason.ParentCancelled
                            (clockPort.UtcNow())
                        |> ignore
                    | HandleLifecycle.Abandoned _
                    | HandleLifecycle.Retired -> ()
                | _ -> ())
