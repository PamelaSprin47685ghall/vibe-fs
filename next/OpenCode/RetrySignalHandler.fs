namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Sole durable fallback writer path.
/// Identity = session + AuthorityRootUserMessageId + providerAttempt (KISS-N12).
/// Physical continuation message IDs must never replace the authority root.
/// Never reads message parts and never invents empty-output failures.
module RetrySignalHandler =

    /// Prefer durable ActiveLogicalRun.AuthorityRootUserMessageId. Fall back to
    /// human-root bindings only when no authority projection exists yet.
    let private authorityRootUserMessageId
        (journal: AgentJournal option)
        (bindings: Dictionary<string, MessageId>)
        (sessionId: SessionId)
        =
        match journal with
        | Some j ->
            match Map.tryFind sessionId (AgentJournal.snapshot j).AgentProjections.Sessions with
            | Some session ->
                match
                    session.PromptAuthority
                    |> Option.bind (fun a -> a.ActiveLogicalRun)
                    |> Option.map (fun r -> r.AuthorityRootUserMessageId)
                with
                | Some root when not (String.IsNullOrWhiteSpace root) -> Some(MessageId.create root)
                | _ ->
                    match bindings.TryGetValue(SessionId.value sessionId) with
                    | true, messageId -> Some messageId
                    | false, _ -> None
            | None ->
                match bindings.TryGetValue(SessionId.value sessionId) with
                | true, messageId -> Some messageId
                | false, _ -> None
        | None ->
            match bindings.TryGetValue(SessionId.value sessionId) with
            | true, messageId -> Some messageId
            | false, _ -> None

    let handle
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
        (userBindings: Dictionary<string, MessageId>)
        (signal: RetrySignal)
        =
        // signal.MessageId is often the physical retry user message (or absent).
        // Fallback epoch identity is always the Authority Root, never that physical id.
        match authorityRootUserMessageId journal userBindings signal.SessionId with
        | None -> ()
        | Some messageId when String.IsNullOrWhiteSpace(MessageId.value messageId) -> ()
        | Some messageId ->
            FallbackDetect.recordFallbackFailure
                journal
                recorded
                (SessionId.value signal.SessionId)
                (MessageId.value messageId)
                signal.Attempt
                signal.Reason
            |> ignore
