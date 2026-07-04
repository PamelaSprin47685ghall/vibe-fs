// Based on opencode-auto-resume raw-tool-call detection patterns + Wanxiangshu protocol extensions.
module Wanxiangshu.Shell.FallbackMessageCodec

open System.Text.RegularExpressions
open Wanxiangshu.Shell.Dyn

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
        Regex(@"<(?:edit|write|read|bash|grep|glob|search|replace|execute|run)\s*(?:\s[^>]*)?\s*(?:\/>|>)", RegexOptions.IgnoreCase)
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
        Regex(@"<[^>]*\bname\s*=\s*[""'](read|write|edit|execute|search|fuzzy_find|fuzzy_grep|fuzzy_search|list|bash|shell|terminal|browser|web|grep|find|submit_review|return_reviewer|investigator|coder|meditator|webfetch|websearch)[""']", RegexOptions.IgnoreCase)
        // Tool-name tags - standalone tool name elements
        Regex(@"<(read|write|edit|execute|search|fuzzy_find|fuzzy_grep|fuzzy_search|list|bash|shell|terminal|browser|web|grep|find|submit_review|return_reviewer|investigator|coder|meditator|webfetch|websearch)\b", RegexOptions.IgnoreCase)
    |]

/// Open/close pairs for truncated-tag detection: if open matches but close
/// doesn't, a tool call tag was truncated before completion.

/// Pair of regexes that detect truncated XML/JSON tags: if open matches but
/// close doesn't, the tag was truncated mid-stream.
type private TruncatedPattern =
    { Open: Regex
      Close: Regex }

let private truncatedOpenCloseRegexes =
    [|
        { Open = Regex(@"<function[^>]*>", RegexOptions.IgnoreCase)
          Close = Regex(@"</function>", RegexOptions.IgnoreCase) }
        { Open = Regex(@"<parameter[^>]*>", RegexOptions.IgnoreCase)
          Close = Regex(@"</parameter>", RegexOptions.IgnoreCase) }
        { Open = Regex(@"<tool_call[^>]*>", RegexOptions.IgnoreCase)
          Close = Regex(@"</tool_call>", RegexOptions.IgnoreCase) }
        { Open = Regex(@"\{""type""\s*:", RegexOptions.IgnoreCase)
          Close = Regex(@"\}", RegexOptions.IgnoreCase) }
        { Open = Regex(@"\{""name""\s*:", RegexOptions.IgnoreCase)
          Close = Regex(@"\}", RegexOptions.IgnoreCase) }
    |]

/// Test whether a raw text string contains an XML tool call pattern.
let containsToolCallAsText (text: string) : bool =
    if System.String.IsNullOrEmpty(text) then false
    elif text.Length <= 10 then false
    else
        let hasSimpleMatch =
            toolCallRegexes
            |> Array.exists (fun regex -> regex.IsMatch(text))
        if hasSimpleMatch then true
        else
            truncatedOpenCloseRegexes
            |> Array.exists (fun pair ->
                pair.Open.IsMatch(text) && not (pair.Close.IsMatch(text)))

/// Scanner prompt returned when a tool-call-as-text is detected.
let private recoveryPrompt =
    "You produced the tool call as raw text instead of properly dispatching the function calling protocol. I cannot execute it. Please invoke it again using proper structured tool calling protocol instead of putting XML tags inside your thought process or output."

/// Scan messages backwards for the most recent assistant text containing an
/// XML tool call pattern. Returns the recovery prompt when detected.
///
/// This is based on opencode-auto-resume's raw-tool-call detection pattern
/// with Wanxiangshu protocol extensions: it walks messages in reverse order,
/// checks each assistant part of type "text", and tests whether the text content
/// contains any tool call regex pattern (simple match or open/close truncation).
let scanToolCallAsText (msgs: obj array) : string option =
    if isNull msgs || msgs.Length = 0 then None
    else
        let hasToolText =
            msgs
            |> Array.rev
            |> Array.exists (fun msg ->
                let info = Dyn.get msg "info"
                if Dyn.str info "role" <> "assistant" then false
                else
                    let parts = Dyn.get msg "parts"
                    if not (Dyn.isArray parts) then false
                    else
                        (parts :?> obj array)
                        |> Array.rev
                        |> Array.exists (fun part ->
                            if Dyn.str part "type" <> "text" then false
                            else
                                let text = Dyn.str part "text"
                                containsToolCallAsText text))
        if hasToolText then Some recoveryPrompt
        else None

/// Scan messages backwards for the most recent task/todowrite tool part and
/// report whether every todo item is in a terminal status.
let allTodosCompleted (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then false
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let parts = Dyn.get msg "parts"
            if not (Dyn.isArray parts) then None
            else
                (parts :?> obj array)
                |> Array.rev
                |> Array.tryPick (fun part ->
                    let partType = Dyn.str part "type"
                    let tool = Dyn.str part "tool"
                    if (partType = "tool" || partType = "dynamic-tool")
                       && (tool = "task" || tool = "todowrite") then
                        let input = Dyn.get (Dyn.get part "state") "input"
                        let todos = Dyn.get input "todos"
                        if Dyn.isArray todos then
                            (todos :?> obj array)
                            |> Array.forall (fun todo ->
                                let s = Dyn.str todo "status"
                                s = "completed" || s = "cancelled")
                            |> Some
                        else None
                    else None))
        |> Option.defaultValue false
