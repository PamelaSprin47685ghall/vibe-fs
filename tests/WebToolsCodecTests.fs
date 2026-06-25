module VibeFs.Tests.WebToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Domain
open VibeFs.Shell.WebToolsCodec

let decodeWebsearchMissingQuery () =
    let args = createObj [ "what_to_summarize", box "summarize this" ]
    match decodeWebsearchArgs args with
    | Error (InvalidIntent ("websearch", "query", _)) -> check "websearch missing query" true
    | _ -> check "websearch missing query" false

let decodeWebsearchMissingWhatToSummarize () =
    let args = createObj [ "query", box "q" ]
    match decodeWebsearchArgs args with
    | Error (InvalidIntent ("websearch", "what_to_summarize", "required")) -> check "websearch missing what_to_summarize" true
    | _ -> check "websearch missing what_to_summarize" false

let decodeWebsearchOk () =
    let args = createObj [ "query", box "q"; "what_to_summarize", box "sum"; "numResults", box 3 ]
    match decodeWebsearchArgs args with
    | Ok ws ->
        check "websearch ok query" (ws.Query = "q")
        equal "websearch ok numResults" 3 ws.NumResults
        check "websearch ok what_to_summarize" (ws.WhatToSummarize = "sum")
    | Error _ -> check "websearch ok" false

let decodeWebfetchMissingUrl () =
    let args = createObj []
    match decodeWebfetchArgs args with
    | Error (InvalidIntent ("webfetch", "url", _)) -> check "webfetch missing url" true
    | _ -> check "webfetch missing url" false

let decodeWebfetchInvalidPreferLlmsTxt () =
    let args = createObj [ "url", box "https://x"; "prefer_llms_txt", box "maybe" ]
    match decodeWebfetchArgs args with
    | Error (InvalidIntent ("webfetch", "prefer_llms_txt", _)) -> check "webfetch invalid prefer_llms_txt" true
    | _ -> check "webfetch invalid prefer_llms_txt" false

let decodeWebfetchOk () =
    let args =
        createObj [
            "url", box "https://example.com"
            "extract_main", box true
            "prefer_llms_txt", box "auto"
            "prompt", box "p"
            "timeout", box 30
        ]
    match decodeWebfetchArgs args with
    | Ok wf ->
        check "webfetch ok url" (wf.Url = "https://example.com")
        check "webfetch ok extract_main" (wf.ExtractMain = Some true)
        check "webfetch ok prefer" (wf.PreferLlmsTxt = Some "auto")
        check "webfetch ok prompt" (wf.Prompt = Some "p")
        check "webfetch ok timeout" (wf.Timeout = Some 30)
    | Error _ -> check "webfetch ok" false

let run () =
    decodeWebsearchMissingQuery ()
    decodeWebsearchMissingWhatToSummarize ()
    decodeWebsearchOk ()
    decodeWebfetchMissingUrl ()
    decodeWebfetchInvalidPreferLlmsTxt ()
    decodeWebfetchOk ()