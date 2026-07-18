// Based on opencode-auto-resume raw-tool-call detection patterns + Wanxiangshu protocol extensions.
module Wanxiangshu.Runtime.Fallback.FallbackMessageParser

open System.Text.RegularExpressions
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
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
           @"<[^>]*\bname\s*=\s*[""'](read|write|edit|execute|search|fuzzy_find|fuzzy_grep|fuzzy_search|list|bash|shell|terminal|browser|web|grep|find|submit_review|return_reviewer|inspector|coder|meditator|webfetch|websearch)[""']",
           RegexOptions.IgnoreCase
       )
       // Tool-name tags - standalone tool name elements
       Regex(
           @"<(read|write|edit|execute|search|fuzzy_find|fuzzy_grep|fuzzy_search|list|bash|shell|terminal|browser|web|grep|find|submit_review|return_reviewer|inspector|coder|meditator|webfetch|websearch)\b",
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
