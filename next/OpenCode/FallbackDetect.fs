namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// Detects failed assistant turns when a session reaches IDLE status.
/// Does NOT rely on remote error fields or raw SSE streaming chunks.
///   1. Assistant message with zero-byte text → failed turn.
///   2. Assistant text contains XML markup but the message carries no
///      real tool-call parts → model wrote a tool call as prose.
/// Each unique message id is recorded once as FallbackFailureRecorded.
module FallbackDetect =

    let private xmlTag =
        Regex("<(?:tool_call|use_tool|call|function_call|invoke)[^>]*>", RegexOptions.Compiled)

    let private getPartsArray (msg: obj) : obj array option =
        if isNull msg then
            None
        elif not (isNull msg?parts) then
            Some(unbox<obj array> msg?parts)
        elif not (isNull msg?properties) && not (isNull msg?properties?parts) then
            Some(unbox<obj array> msg?properties?parts)
        elif
            not (isNull msg?properties)
            && not (isNull msg?properties?message)
            && not (isNull msg?properties?message?parts)
        then
            Some(unbox<obj array> msg?properties?message?parts)
        else
            None

    let private partsText (msg: obj) : string =
        match getPartsArray msg with
        | None -> ""
        | Some parts ->
            parts
            |> Array.choose (fun p ->
                if isNull p then
                    None
                else
                    let t = p?``type``

                    if isNull t then
                        None
                    else
                        let s = unbox<string> t

                        if s <> "text" then
                            None
                        else
                            let txt = p?text
                            if isNull txt then None else Some(unbox<string> txt))
            |> String.concat ""

    let private hasToolCallPart (msg: obj) : bool =
        match getPartsArray msg with
        | None -> false
        | Some parts ->
            parts
            |> Array.exists (fun p ->
                if isNull p then
                    false
                else
                    let t = p?``type``

                    not (isNull t)
                    && let s = unbox<string> t in

                       s = "tool-call"
                       || s = "tool_call"
                       || s = "tool"
                       || s = "patch"
                       || s = "step-start"
                       || s = "step-finish")

    let isTerminalAssistant (msg: obj) : bool =
        if isNull msg then
            false
        else
            let info = msg?info

            let finish =
                if not (isNull info) && not (isNull info?finish) then
                    unbox<string> info?finish
                elif not (isNull msg?finish) then
                    unbox<string> msg?finish
                elif not (isNull msg?finishReason) then
                    unbox<string> msg?finishReason
                else
                    ""

            let role =
                if not (isNull info) && not (isNull info?role) then
                    unbox<string> info?role
                elif not (isNull msg?role) then
                    unbox<string> msg?role
                else
                    ""

            finish = "stop" && role = "assistant"

    let isFailedAssistant (msg: obj) : bool =
        if not (isTerminalAssistant msg) then
            false
        else
            let text = partsText msg

            if String.IsNullOrWhiteSpace text && not (hasToolCallPart msg) then
                true
            else
                xmlTag.IsMatch text && not (hasToolCallPart msg)

    [<Import("createHash", "node:crypto")>]
    let private createHashImport: string -> obj = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringifyMessage (message: obj) : string = jsNative

    let private sha256Hex (text: string) : string =
        let hasher = createHashImport "sha256"
        hasher?update (text) |> ignore
        unbox<string> (hasher?digest ("hex"))

    let messageId (sessionId: string) (msg: obj) : string =
        let info = msg?info

        if not (isNull info) && not (isNull info?id) then
            unbox<string> info?id
        elif not (isNull msg?id) then
            unbox<string> msg?id
        else
            // Deterministic fallback when Host omits message id: hash the full
            // message shape, not just text, so distinct part metadata or finish
            // boundaries remain distinct across restarts.
            let canonical = stringifyMessage msg
            sprintf "anon-%s-%s" sessionId (sha256Hex canonical)

    /// Single attributor: the ONLY writer of FallbackFailureRecorded.
    /// The sole caller is the provider-retry detector (RetrySignalHandler): a
    /// durable failure fact requires an explicit host retry status carrying a
    /// stable message/attempt identity. Empty/xml terminals are interaction
    /// repair (zero-width continuation), NOT provider call failures, and never
    /// reach this function. The in-memory set mirrors the durable fold's
    /// identity (assistantMessageId|providerAttempt) so one process run never
    /// double-appends; the fold is the cross-restart idempotency boundary.
    let recordFallbackFailure
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
        (sessionId: string)
        (assistantMessageId: string)
        (providerAttempt: string)
        (reason: string)
        : FallbackDecision =
        let sid = SessionId.create sessionId
        // Must match AgentJournal.fallbackIdentity / fold RecentFailureIds.
        let identity = AgentJournal.fallbackIdentity assistantMessageId providerAttempt

        let currentDecision (j: AgentJournal) =
            DurableFallback.nextDecision sid (AgentJournal.snapshot j)

        let append () : FallbackDecision =
            match journal with
            | None -> FallbackDecision.NextAttempt Fallback.initial
            | Some j ->
                let fact =
                    AgentFact.FallbackFailureRecorded
                        {| SessionId = sid
                           Reason = reason
                           AssistantMessageId = assistantMessageId
                           ProviderAttempt = providerAttempt |}

                match AgentJournal.appendAgent (StreamId.Session sid) None fact j with
                | Ok _ -> currentDecision j
                | Error _ -> FallbackDecision.Dead

        if recorded.Add identity then
            // In-memory fast path passed. Check the durable projection — the
            // cross-restart boundary — and skip the append if this identity was
            // already recorded in a prior process run. Uses the BOUNDED
            // per-session RecentFailureIds (not a global HashSet) so memory is
            // O(sessions), not O(history).
            match journal with
            | None -> append ()
            | Some j ->
                let alreadyRecorded =
                    let projection = AgentJournal.snapshot j

                    projection.AgentProjections.Sessions
                    |> Map.tryFind sid
                    |> Option.bind (fun session -> session.Fallback)
                    |> Option.exists (fun fb -> List.contains identity fb.RecentFailureIds)

                if alreadyRecorded then currentDecision j else append ()
        else
            // Already seen this process run; return the current projection decision.
            match journal with
            | None -> FallbackDecision.NextAttempt Fallback.initial
            | Some j -> currentDecision j
