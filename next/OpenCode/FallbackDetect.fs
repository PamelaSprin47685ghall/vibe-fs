namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

/// Detects failed assistant turns from SSE message events.
/// Does NOT rely on remote error fields.  Two local heuristics:
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

    let isFailedAssistant (msg: obj) : bool =
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

            if finish <> "stop" then
                false
            else
                let role =
                    if not (isNull info) && not (isNull info?role) then
                        unbox<string> info?role
                    elif not (isNull msg?role) then
                        unbox<string> msg?role
                    else
                        ""

                if role <> "assistant" then
                    false
                else
                    let text = partsText msg

                    if String.IsNullOrWhiteSpace text && not (hasToolCallPart msg) then
                        true
                    else
                        xmlTag.IsMatch text && not (hasToolCallPart msg)

    let messageId (msg: obj) : string =
        let info = msg?info

        if not (isNull info) && not (isNull info?id) then
            unbox<string> info?id
        elif not (isNull msg?id) then
            unbox<string> msg?id
        else
            Guid.NewGuid().ToString("N")

    let observeEvent (journal: AgentJournal option) (recorded: HashSet<string>) (raw: obj) : unit =
        match journal with
        | None -> ()
        | Some journal ->
            let ev = if isNull raw?event then raw else raw?event

            if isNull ev then
                ()
            else
                let props = ev?properties

                if isNull props then
                    ()
                else
                    let msg = props?message
                    let target = if isNull msg then props else msg

                    if isFailedAssistant target then
                        let msgId = messageId target

                        if recorded.Add msgId then
                            let sid =
                                let info = target?info

                                if not (isNull info) && not (isNull info?sessionID) then
                                    unbox<string> info?sessionID
                                elif not (isNull props?sessionID) then
                                    unbox<string> props?sessionID
                                else
                                    ""

                            if not (String.IsNullOrWhiteSpace sid) then
                                let fact =
                                    AgentFact.FallbackFailureRecorded
                                        {| SessionId = SessionId.create sid
                                           Reason = "empty or xml-only assistant turn" |}

                                AgentJournal.appendAgent (StreamId.Session(SessionId.create sid)) None fact journal
                                |> ignore
