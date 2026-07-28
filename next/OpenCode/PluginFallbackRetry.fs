namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// Plugin-owned same Logical-Run A/A/B/B.
/// Host prompt_async after session.error only starts a real provider loop once
/// the runner is fully idle. Record durable failure immediately; debounce
/// ProviderRetryAttempt until SessionIdle has been quiet for a short settle.
module PluginFallbackRetry =

    type PendingRetry =
        { AssistantMessageId: MessageId
          Reason: string
          Directory: string option }

    let private pendingGate = obj ()
    let private pending = Dictionary<string, PendingRetry>()
    /// SessionId string → timer handle for debounced flush.
    let private flushTimers = Dictionary<string, obj>()

    let private autoFallbackDisabled () =
        let value = Environment.GetEnvironmentVariable("WANXIANGSHU_DISABLE_AUTO_FALLBACK")
        value = "1"

    let private activeIdentity (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | None -> None
        | Some j ->
            match Map.tryFind sessionId (AgentJournal.snapshot j).AgentProjections.Sessions with
            | Some session ->
                session.PromptAuthority
                |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                |> Option.map (fun run -> run.LogicalRunId, run.AuthorityRootUserMessageId, session.Fallback)
            | None -> None

    let private nextProviderAttempt (fallback: FallbackProjection option) =
        match fallback with
        | Some fb -> string (fb.TotalFailures + 1)
        | None -> "1"

    let private effectiveModel
        (modelConfig: ModelResolver.ModelConfig option)
        (sessionId: SessionId)
        (journal: AgentJournal)
        =
        match modelConfig with
        | None ->
            ModelResolver.fromEnv ()
            |> Option.bind (fun cfg -> ModelResolver.resolveForSession cfg sessionId (AgentJournal.snapshot journal))
        | Some cfg -> ModelResolver.resolveForSession cfg sessionId (AgentJournal.snapshot journal)

    let private tryRemovePending (key: string) =
        lock pendingGate (fun () ->
            match pending.TryGetValue key with
            | true, value ->
                pending.Remove key |> ignore
                Some value
            | false, _ -> None)

    let private setPending (key: string) (value: PendingRetry) =
        lock pendingGate (fun () -> pending.[key] <- value)

    let private hasPending (key: string) =
        lock pendingGate (fun () -> pending.ContainsKey key)

    /// Record durable failure. Queue retry for debounced SessionIdle when NextAttempt.
    let handleTurnFailure
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
        (modelConfig: ModelResolver.ModelConfig option)
        (sessionId: SessionId)
        (assistantMessageId: MessageId)
        (error: string)
        (directory: string option)
        (onAccepted: (MessageId -> unit) option)
        : bool =
        let _ = sessionPort
        let _ = modelConfig
        let _ = onAccepted

        if autoFallbackDisabled () then
            false
        else
            match journal, activeIdentity journal sessionId with
            | None, _
            | _, None -> false
            | Some _, Some(logicalRunId, authorityRoot, fallbackBefore) ->
                let attempt = nextProviderAttempt fallbackBefore

                let decision =
                    FallbackDetect.recordFallbackFailure
                        journal
                        recorded
                        (SessionId.value sessionId)
                        logicalRunId
                        authorityRoot
                        (MessageId.value assistantMessageId)
                        attempt
                        error

                match decision with
                | FallbackDecision.Dead ->
                    tryRemovePending (SessionId.value sessionId) |> ignore

                    eventPort.NotifyTerminal sessionId (TerminalOutcome.Failed "LOGICAL_RUN_DEAD")
                    |> ignore

                    true
                | FallbackDecision.NextAttempt _ ->
                    setPending
                        (SessionId.value sessionId)
                        { AssistantMessageId = assistantMessageId
                          Reason = error
                          Directory = directory }

                    true

    let private sendPending
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (modelConfig: ModelResolver.ModelConfig option)
        (sessionId: SessionId)
        (onAccepted: (MessageId -> unit) option)
        (pendingRetry: PendingRetry)
        : bool =
        if autoFallbackDisabled () then
            false
        else
            match journal with
            | None -> false
            | Some j ->
                match effectiveModel modelConfig sessionId j with
                | None ->
                    eventPort.NotifyTerminal sessionId (TerminalOutcome.Failed "LOGICAL_RUN_DEAD")
                    |> ignore

                    true
                | Some model ->
                    HostSessionNudge.sendContinuation
                        sessionPort
                        sessionId
                        "Continue after provider failure."
                        PromptAuthority.ProviderRetryAttempt
                        { Model = Some model
                          Agent = None
                          Directory = pendingRetry.Directory
                          Metadata = None }
                        journal
                        onAccepted

                    true

    /// Debounced flush: each SessionIdle reschedules. Only the last quiet period
    /// after host teardown removes pending and sends ProviderRetryAttempt.
    let scheduleFlushOnIdle
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (modelConfig: ModelResolver.ModelConfig option)
        (sessionId: SessionId)
        (onAccepted: (MessageId -> unit) option)
        (settleMs: int)
        : unit =
        let key = SessionId.value sessionId

        if not (hasPending key) then
            ()
        else
            lock pendingGate (fun () ->
                match flushTimers.TryGetValue key with
                | true, oldTimer ->
                    HostSignalBootstrapTimers.clearTimeout oldTimer
                    flushTimers.Remove key |> ignore
                | false, _ -> ()

                let timer =
                    HostSignalBootstrapTimers.deferMs settleMs (fun () ->
                        lock pendingGate (fun () -> flushTimers.Remove key |> ignore)

                        match tryRemovePending key with
                        | None -> ()
                        | Some pendingRetry ->
                            sendPending sessionPort eventPort journal modelConfig sessionId onAccepted pendingRetry
                            |> ignore)

                flushTimers.[key] <- timer)

    /// Back-compat alias used by older call sites.
    let flushPendingRetry
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (modelConfig: ModelResolver.ModelConfig option)
        (sessionId: SessionId)
        (onAccepted: (MessageId -> unit) option)
        : bool =
        scheduleFlushOnIdle sessionPort eventPort journal modelConfig sessionId onAccepted 250
        hasPending (SessionId.value sessionId)
