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

    type AuthorityIdentity =
        { LogicalRunId: string
          AuthorityRootUserMessageId: MessageId }

    /// Prefer durable ActiveLogicalRun. Fall back to human-root bindings only when
    /// no authority projection exists yet (stable hash of bound root).
    let authorityIdentity
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
