namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Detects a provider-retry status and attributes the failure through the
/// single FallbackDetect attributor. This is the ONLY durable fallback writer:
/// an explicit host `session.status = retry` with a stable message/attempt
/// identity. When the host supplies no message id, `fallbackMessageId` (the
/// in-flight assistant message id from the router) is used — never a
/// session+attempt+reason hash, which would wrongly dedupe across distinct
/// user turns.
module HostEventRetry =
    let record
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
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
