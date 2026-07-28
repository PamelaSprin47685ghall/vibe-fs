namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// Plugin-owned same Logical-Run A/A/B/B when Host built-in retries are disabled.
/// Records durable FallbackFailure, then either sends ProviderRetryAttempt with
/// EffectiveModel or fails the run as LogicalRunDead.
module PluginFallbackRetry =

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

    /// After a provider/turn failure: record durable failure for the active
    /// Logical Run and either continue with EffectiveModel or mark Dead.
    /// Returns true when a plugin retry continuation was submitted.
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
        if autoFallbackDisabled () then
            false
        else
            match journal, activeIdentity journal sessionId with
            | None, _
            | _, None -> false
            | Some j, Some(logicalRunId, authorityRoot, fallbackBefore) ->
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
                    eventPort.NotifyTerminal sessionId (TerminalOutcome.Failed "LOGICAL_RUN_DEAD")
                    |> ignore

                    true
                | FallbackDecision.NextAttempt _ ->
                    match effectiveModel modelConfig sessionId j with
                    | None ->
                        eventPort.NotifyTerminal sessionId (TerminalOutcome.Failed "LOGICAL_RUN_DEAD")
                        |> ignore

                        true
                    | Some model ->
                        // Host may skip provider calls for zero-width-only user
                        // messages after a terminal APIError. Use a short visible
                        // continuation so the next EffectiveModel request is real.
                        HostSessionNudge.sendContinuation
                            sessionPort
                            sessionId
                            "Continue after provider failure."
                            PromptAuthority.ProviderRetryAttempt
                            { Model = Some model
                              Agent = None
                              Directory = directory
                              Metadata = None }
                            journal
                            onAccepted

                        true
