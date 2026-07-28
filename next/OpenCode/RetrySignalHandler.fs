namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Sole durable fallback writer path.
/// Identity = logicalRunId + AuthorityRootUserMessageId + providerAttempt.
/// Physical continuation message IDs must never replace the authority root.
/// Never reads message parts and never invents empty-output failures.
module RetrySignalHandler =

    type private AuthorityIdentity =
        { LogicalRunId: string
          AuthorityRootUserMessageId: MessageId }

    /// Prefer durable ActiveLogicalRun. Fall back to human-root bindings only when
    /// no authority projection exists yet (stable hash of bound root).
    let private authorityIdentity
        (journal: AgentJournal option)
        (bindings: Dictionary<string, MessageId>)
        (sessionId: SessionId)
        : AuthorityIdentity option =
        match journal with
        | Some j ->
            match Map.tryFind sessionId (AgentJournal.snapshot j).AgentProjections.Sessions with
            | Some session ->
                match session.PromptAuthority |> Option.bind (fun a -> a.ActiveLogicalRun) with
                | Some run when not (String.IsNullOrWhiteSpace(MessageId.value run.AuthorityRootUserMessageId)) ->
                    Some
                        { LogicalRunId = run.LogicalRunId
                          AuthorityRootUserMessageId = run.AuthorityRootUserMessageId }
                | _ ->
                    match bindings.TryGetValue(SessionId.value sessionId) with
                    | true, messageId ->
                        Some
                            { LogicalRunId =
                                PromptAuthority.stableLogicalRunId
                                    PromptAuthority.sha256Hex
                                    (RuntimeId.value (AgentJournal.runtimeId j))
                                    sessionId
                                    messageId
                              AuthorityRootUserMessageId = messageId }
                    | false, _ -> None
            | None ->
                match bindings.TryGetValue(SessionId.value sessionId) with
                | true, messageId ->
                    Some
                        { LogicalRunId =
                            PromptAuthority.stableLogicalRunId
                                PromptAuthority.sha256Hex
                                (RuntimeId.value (AgentJournal.runtimeId j))
                                sessionId
                                messageId
                          AuthorityRootUserMessageId = messageId }
                | false, _ -> None
        | None ->
            match bindings.TryGetValue(SessionId.value sessionId) with
            | true, messageId ->
                Some
                    { LogicalRunId = "no-journal"
                      AuthorityRootUserMessageId = messageId }
            | false, _ -> None

    let private currentAttempt (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | None -> None
        | Some j ->
            (AgentJournal.snapshot j).AgentProjections.Sessions
            |> Map.tryFind sessionId
            |> Option.bind (fun session -> session.Fallback)
            |> Option.bind (fun fallback -> fallback.LastProviderAttempt)

    let handle
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
        (userBindings: Dictionary<string, MessageId>)
        (signal: RetrySignal)
        =
        // signal.MessageId is often the physical retry user message (or absent).
        // Fallback epoch identity is always the Authority Root, never that physical id.
        match authorityIdentity journal userBindings signal.SessionId with
        | None -> ()
        | Some identity when String.IsNullOrWhiteSpace(MessageId.value identity.AuthorityRootUserMessageId) -> ()
        | Some identity ->
            // AssistantMessageId is retained for diagnostics only; identity is run-scoped.
            let assistantMessageId =
                match signal.MessageId with
                | Some mid -> MessageId.value mid
                | None -> MessageId.value identity.AuthorityRootUserMessageId

            FallbackDetect.recordFallbackFailure
                journal
                recorded
                (SessionId.value signal.SessionId)
                identity.LogicalRunId
                (MessageId.value identity.AuthorityRootUserMessageId)
                assistantMessageId
                signal.Attempt
                signal.Reason
            |> ignore

    /// Host-declared non-retryable errors still need a physical continuation.
    /// Route their synthetic retry identity through this sole durable writer;
    /// callers send the continuation only after the following idle signal.
    let handleProviderError
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
        (userBindings: Dictionary<string, MessageId>)
        (error: ProviderErrorSignal)
        =
        let dedupeKey =
            sprintf
                "provider-error|%s|%s"
                (SessionId.value error.SessionId)
                (error.MessageId
                 |> Option.map MessageId.value
                 |> Option.defaultValue error.Reason)

        if recorded.Add dedupeKey then
            let current = currentAttempt journal error.SessionId

            let marker attempt =
                sprintf "provider-error-attempt|%s|%d" (SessionId.value error.SessionId) attempt

            match current with
            | Some attempt when not (recorded.Contains(marker attempt)) ->
                // Host emitted its retry signal before the terminal error.
                // Reuse that durable advance; do not advance twice.
                recorded.Add(marker attempt) |> ignore
            | _ ->
                let attempt = current |> Option.defaultValue 0L |> (fun value -> value + 1L)

                handle
                    journal
                    recorded
                    userBindings
                    { SessionId = error.SessionId
                      Attempt = string attempt
                      Reason = error.Reason
                      MessageId = error.MessageId }

                recorded.Add(marker attempt) |> ignore

            true
        else
            false
