// Based on opencode-auto-resume raw-tool-call detection patterns + Wanxiangshu protocol extensions.
module Wanxiangshu.Shell.FallbackMessageCodec

open System.Text.RegularExpressions
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types

/// Regex patterns that detect XML-style tool calls emitted as raw text.
/// Covers wrappers (tool_call/function/invoke), closing tags, name attributes,
/// self-closing tool tags, JSON-style tool calls, and string-end truncations.
let private toolCallRegexes =
    [|
       // Generic tool call wrappers: <tool_call, <function, <invoke, etc.
       Regex(@"<(tool_call|function|invoke|use_tool|call|parameter)\b", RegexOptions.IgnoreCase)
       // Opening function tag with attribute: <function name=...
       Regex(@"<function\s*=", RegexOptions.IgnoreCase)
       // Closing XML tags
       Regex(@"</function>", RegexOptions.IgnoreCase)
       Regex(@"</parameter>", RegexOptions.IgnoreCase)
       Regex(@"</tool_call>", RegexOptions.IgnoreCase)
       // Tool-name attribute variants: <tool name= or <tool_name=
       Regex(@"<tool[\s_]name\s*=", RegexOptions.IgnoreCase)
       // Invoke tag with trailing space: <invoke [args]
       Regex(@"<invoke\s+", RegexOptions.IgnoreCase)
       // Self-closing tool tags: <edit ...>, <edit/>, <edit attr="x"/>
       Regex(
           @"<(?:edit|write|read|bash|grep|glob|search|replace|execute|run)\s*(?:\s[^>]*)?\s*(?:\/>|>)",
           RegexOptions.IgnoreCase
       )
       // JSON-style tool call: {"type":"function", ...}
       Regex(@"\{""type"":\s*""function""", RegexOptions.IgnoreCase)
       // JSON-style tool name: {"name":"read", ...}
       Regex(@"\{""name"":\s*""[a-zA-Z_]", RegexOptions.IgnoreCase)
       // Truncated tag at string end: <func, <funct, <functi, <functio, <function
       Regex(@"<func(?:t|ti|tio|tion)?$", RegexOptions.IgnoreCase ||| RegexOptions.Multiline)
       // Truncated parameter at end: <par, <para, <param, <parame, <paramet, <parameter
       Regex(@"<par(?:a|am|ame|amet|amete|ameter)?$", RegexOptions.IgnoreCase ||| RegexOptions.Multiline)
       // Truncated JSON at end: {"type": (optional colon)
       Regex(@"\{""type""\s*:?$", RegexOptions.IgnoreCase ||| RegexOptions.Multiline)
       Regex(@"\{""name""\s*:?$", RegexOptions.IgnoreCase ||| RegexOptions.Multiline)
       // name="tool_name" attribute pattern
       Regex(
           @"<[^>]*\bname\s*=\s*[""'](read|write|edit|execute|search|fuzzy_find|fuzzy_grep|fuzzy_search|list|bash|shell|terminal|browser|web|grep|find|submit_review|return_reviewer|investigator|coder|meditator|webfetch|websearch)[""']",
           RegexOptions.IgnoreCase
       )
       // Tool-name tags - standalone tool name elements
       Regex(
           @"<(read|write|edit|execute|search|fuzzy_find|fuzzy_grep|fuzzy_search|list|bash|shell|terminal|browser|web|grep|find|submit_review|return_reviewer|investigator|coder|meditator|webfetch|websearch)\b",
           RegexOptions.IgnoreCase
       ) |]

/// Open/close pairs for truncated-tag detection: if open matches but close
/// doesn't, a tool call tag was truncated before completion.
/// Pair of regexes that detect truncated XML/JSON tags: if open matches but
/// close doesn't, the tag was truncated mid-stream.
type private TruncatedPattern = { Open: Regex; Close: Regex }

let private truncatedOpenCloseRegexes =
    [| { Open = Regex(@"<function[^>]*>", RegexOptions.IgnoreCase)
         Close = Regex(@"</function>", RegexOptions.IgnoreCase) }
       { Open = Regex(@"<parameter[^>]*>", RegexOptions.IgnoreCase)
         Close = Regex(@"</parameter>", RegexOptions.IgnoreCase) }
       { Open = Regex(@"<tool_call[^>]*>", RegexOptions.IgnoreCase)
         Close = Regex(@"</tool_call>", RegexOptions.IgnoreCase) }
       { Open = Regex(@"\{""type""\s*:", RegexOptions.IgnoreCase)
         Close = Regex(@"\}", RegexOptions.IgnoreCase) }
       { Open = Regex(@"\{""name""\s*:", RegexOptions.IgnoreCase)
         Close = Regex(@"\}", RegexOptions.IgnoreCase) } |]

let stripMarkdownCode (text: string) : string =
    if System.String.IsNullOrEmpty(text) then
        ""
    else
        let len = text.Length
        let sb = System.Text.StringBuilder(len)
        let mutable i = 0

        while i < len do
            if i + 2 < len && text.[i] = '`' && text.[i + 1] = '`' && text.[i + 2] = '`' then
                i <- i + 3
                let mutable found = false

                while i + 2 < len && not found do
                    if text.[i] = '`' && text.[i + 1] = '`' && text.[i + 2] = '`' then
                        found <- true
                        i <- i + 3
                    else
                        i <- i + 1

                if not found then
                    i <- len
            elif text.[i] = '`' then
                i <- i + 1
                let mutable found = false

                while i < len && not found do
                    if text.[i] = '`' then
                        found <- true
                        i <- i + 1
                    else
                        i <- i + 1

                if not found then
                    i <- len
            else
                sb.Append(text.[i]) |> ignore
                i <- i + 1

        sb.ToString()

/// Test whether a raw text string contains an XML tool call pattern.
let containsToolCallAsText (text: string) : bool =
    if System.String.IsNullOrEmpty(text) || text.Length <= 10 then
        false
    else
        let cleaned = stripMarkdownCode text

        if cleaned.Length <= 10 then
            false
        else
            let hasSimpleMatch =
                toolCallRegexes |> Array.exists (fun regex -> regex.IsMatch(cleaned))

            let isRealCall =
                hasSimpleMatch
                || (truncatedOpenCloseRegexes
                    |> Array.exists (fun pair -> pair.Open.IsMatch(cleaned) && not (pair.Close.IsMatch(cleaned))))

            isRealCall
            && (cleaned.Contains("=")
                || cleaned.Contains("/")
                || cleaned.Contains("<tool_call")
                || cleaned.Contains("<function")
                || cleaned.Contains("<invoke")
                || cleaned.Contains("{"))

/// Scanner prompt returned when a tool-call-as-text is detected.
let private recoveryPrompt =
    "You produced the tool call as raw text instead of properly dispatching the function calling protocol. I cannot execute it. Please invoke it again using proper structured tool calling protocol instead of putting XML tags inside your thought process or output."

/// Scan messages backwards for the most recent assistant text containing an
/// XML tool call pattern. Returns the recovery prompt when detected.
let scanToolCallAsText (msgs: obj array) : string option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        let lastAssistant =
            msgs
            |> Array.rev
            |> Array.tryFind (fun msg -> Dyn.str (Dyn.get msg "info") "role" = "assistant")

        match lastAssistant with
        | None -> None
        | Some msg ->
            let parts = Dyn.get msg "parts"

            if not (Dyn.isArray parts) then
                None
            else
                let hasToolText =
                    (parts :?> obj array)
                    |> Array.rev
                    |> Array.exists (fun part ->
                        Dyn.str part "type" = "text" && containsToolCallAsText (Dyn.str part "text"))

                if hasToolText then Some recoveryPrompt else None

/// Scan messages backwards for the most recent task/todowrite tool part and
/// report whether every todo item is in a terminal status.
let allTodosCompleted (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then
        false
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let parts = Dyn.get msg "parts"

            if not (Dyn.isArray parts) then
                None
            else
                (parts :?> obj array)
                |> Array.rev
                |> Array.tryPick (fun part ->
                    let pt, tl = Dyn.str part "type", Dyn.str part "tool"

                    if (pt = "tool" || pt = "dynamic-tool") && (tl = "task" || tl = "todowrite") then
                        let todos = Dyn.get (Dyn.get (Dyn.get part "state") "input") "todos"

                        if Dyn.isArray todos then
                            (todos :?> obj array)
                            |> Array.forall (fun t ->
                                let s = Dyn.str t "status" in s = "completed" || s = "cancelled")
                            |> Some
                        else
                            None
                    else
                        None))
        |> Option.defaultValue false

/// Detect whether the last assistant message carries no tool calls and no
/// visible text content — i.e. the LLM returned an empty output.
let isIdleNoContentAndNoTools (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then
        false
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            if Dyn.str (Dyn.get msg "info") "role" <> "assistant" then
                None
            else
                let parts = Dyn.get msg "parts"

                if not (Dyn.isArray parts) then
                    None
                else
                    let arr = parts :?> obj array

                    let hasTool =
                        arr
                        |> Array.exists (fun p -> let pt = Dyn.str p "type" in pt = "tool" || pt = "dynamic-tool")

                    if hasTool then
                        None
                    else
                        Some(
                            not (
                                arr
                                |> Array.exists (fun p ->
                                    Dyn.str p "type" = "text"
                                    && not (System.String.IsNullOrWhiteSpace(Dyn.str p "text")))
                            )
                        ))
        |> Option.defaultValue false

/// Check if the last assistant message shows it was aborted by the user.
/// Returns Some ErrorInput if aborted, otherwise None.
let tryGetLastAssistantAbortInfo (msgs: obj array) : ErrorInput option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let info = Dyn.get msg "info"

            if isNull info || Dyn.str info "role" <> "assistant" then
                None
            else
                let f, err = Dyn.str info "finish", Dyn.get info "error"

                let isAbort =
                    f = "abort"
                    || f = "interrupted"
                    || f = "cancelled"
                    || (not (isNull err)
                        && (Dyn.str err "name" = "MessageAbortedError" || Dyn.str err "name" = "AbortError"))

                if isAbort then
                    Some
                        { ErrorName = "MessageAbortedError"
                          DomainError = Some MessageAborted
                          Message = "Generation aborted before producing content"
                          StatusCode = None
                          IsRetryable = Some false }
                else
                    None)

/// Decode a FallbackModel from a raw host object (which can be a string like "openai/gpt-5" or an object).
let decodeModelFromObj (modelObj: obj) : FallbackModel option =
    if isNull modelObj || Dyn.isNullish modelObj then
        None
    elif Dyn.typeIs modelObj "string" then
        let s = string modelObj
        let slash = s.IndexOf('/')

        if slash <= 0 || slash >= s.Length - 1 then
            None
        else
            Some
                { ProviderID = s.[0 .. slash - 1].Trim()
                  ModelID = s.[slash + 1 ..].Trim()
                  Variant = None
                  Temperature = None
                  TopP = None
                  MaxTokens = None
                  ReasoningEffort = None
                  Thinking = false }
    else
        let getStr k k2 =
            let v = Dyn.str modelObj k in if v <> "" then v else Dyn.str modelObj k2

        let provider = getStr "providerID" "provider"

        let modelId =
            let v = Dyn.str modelObj "modelID"
            let v = if v <> "" then v else Dyn.str modelObj "id"
            if v <> "" then v else Dyn.str modelObj "model"

        if provider <> "" && modelId <> "" then
            Some
                { ProviderID = provider
                  ModelID = modelId
                  Variant = None
                  Temperature = None
                  TopP = None
                  MaxTokens = None
                  ReasoningEffort = None
                  Thinking = false }
        else
            None
