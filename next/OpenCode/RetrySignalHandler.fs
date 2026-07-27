namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Sole durable fallback writer path. Identity = session + userMessage + attempt.
/// Never reads message parts and never invents empty-output failures.
module RetrySignalHandler =

    let private currentUserMessageId
        (bindings: Dictionary<string, MessageId>)
        (sessionId: SessionId)
        (signal: RetrySignal)
        =
        match signal.MessageId with
        | Some messageId -> Some messageId
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
        match currentUserMessageId userBindings signal.SessionId signal with
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
