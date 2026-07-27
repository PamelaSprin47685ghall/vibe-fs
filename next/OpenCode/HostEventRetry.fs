namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Detects a provider-retry status and attributes the failure to the single
/// FallbackDetect attributor. `retryAttemptBySession` records the attempt per
/// session so the failed-assistant detector can recognize the same physical
/// failure instead of appending a second fact. When the host supplies no message
/// id, `fallbackMessageId` (the in-flight assistant message id from the router)
/// is used — never a session+attempt+reason hash, which would wrongly dedupe
/// across distinct user turns.
module HostEventRetry =
    let record
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
        (retryAttemptBySession: Dictionary<string, string>)
        (hostShutdownSessions: HashSet<string>)
        (fallbackMessageId: string)
        (sessionId: string)
        (raw: obj)
        =
        let event = if isNull raw || isNull raw?event then raw else raw?event
        let properties = if isNull event then null else event?properties
        let status = if isNull properties then null else properties?status

        if not (isNull status) && not (isNull status?``type``) && status?``type`` = "retry" then
            let attempt =
                if isNull status?attempt then
                    "unknown"
                else
                    string status?attempt

            let reason =
                if isNull status?message then
                    "provider retry"
                else
                    unbox<string> status?message

            // Host-shutdown artifact: when the provider is deliberately stopped
            // (the test harness stopMocking, or a production graceful shutdown
            // returning 503 "Service Unavailable" / connection reset), in-flight
            // requests fail. These are NOT model failures and must not poison the
            // durable fallback budget (which would otherwise mark the session
            // Dead after 4 and break restart recovery). Real model errors (e.g.
            // provider 500 server_error, see fallback-canary) are recorded
            // normally. The production abort path is additionally gated in
            // HostEventRouter.
            let isHostShutdown =
                reason.Contains("mocking stopped")
                || reason.Contains("Service Unavailable")
                || reason.Contains("Connection reset")
                || reason.Contains("ECONNRESET")

            retryAttemptBySession.[sessionId] <- attempt

            if isHostShutdown then
                hostShutdownSessions.Add sessionId |> ignore
            else
                let assistantMessageId =
                    if not (isNull properties) && not (isNull properties?messageID) then
                        unbox<string> (properties?messageID)
                    elif not (isNull properties) && not (isNull properties?messageId) then
                        unbox<string> (properties?messageId)
                    elif
                        not (isNull properties)
                        && not (isNull properties?info)
                        && not (isNull properties?info?id)
                    then
                        unbox<string> (properties?info?id)
                    else
                        fallbackMessageId

                if not (String.IsNullOrWhiteSpace assistantMessageId) then
                    FallbackDetect.recordFallbackFailure journal recorded sessionId assistantMessageId attempt reason
                    |> ignore
