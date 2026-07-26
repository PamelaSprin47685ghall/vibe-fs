namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Durable provider-retry failure identity for HostEventRouter.
module HostEventRetry =
    let record
        (journal: AgentJournal option)
        (retryAttempts: Dictionary<string, HashSet<string>>)
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

            // Prefer host message id; otherwise bind identity to session+attempt+reason
            // so distinct failure rounds never collapse, while the same retry event
            // remains restart-stable (no random GUID).
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
                    sprintf "retry-%s-%s-%d" sessionId attempt (hash reason)

            let identity = sprintf "%s|%s|%s" assistantMessageId attempt reason

            let seenAttempts =
                match retryAttempts.TryGetValue sessionId with
                | true, values -> values
                | false, _ ->
                    let values = HashSet<string>()
                    retryAttempts.[sessionId] <- values
                    values

            // Process-local set is only a hot-path filter; fold is the durable
            // idempotency boundary across restarts.
            if seenAttempts.Add identity then
                match journal with
                | None -> ()
                | Some journal ->
                    let fact =
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = SessionId.create sessionId
                               Reason = reason
                               AssistantMessageId = assistantMessageId
                               ProviderAttempt = attempt |}

                    AgentJournal.appendAgent (StreamId.Session(SessionId.create sessionId)) None fact journal
                    |> ignore
