module VibeFs.Omp.WebTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.SearchPrompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.WebFetchGuard
open VibeFs.Omp.Codec
open VibeFs.Omp.ChildSession
open VibeFs.Omp.Schema
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.OllamaClient
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell.WebSearchCodec
open VibeFs.Kernel.Domain
open VibeFs.Kernel.HostTools

let private buildWebsearch (pi: obj) : obj =
    let tb = Dyn.get pi "typebox"
    createObj [
        "name", box "websearch"
        "label", box "Web Search"
        "description", box (description "websearch")
        "parameters",
            objectOf
                [| ("query", str Params.websearchQuery tb)
                   ("numResults", opt Params.websearchNumResults tb num)
                   ("what_to_summarize", str Params.websearchWhatToSummarize tb) |]
                tb
        "execute",
            box(fun (_id: string) (params': obj) (signal: obj) ->
                promise {
                    let query = Dyn.str params' "query"
                    let what = Dyn.str params' "what_to_summarize"
                    if query = "" then return errorResult "Web search: query is required."
                    elif what = "" then return errorResult "Web search: what_to_summarize is required."
                    else
                        let num = defaultArg (optInt params' "numResults") 10
                        let abort = if Dyn.isNullish signal then None else Some signal
                        let! result =
                            ollamaPost "web_search"
                                (createObj [ "query", box query; "max_results", box num ])
                                abort
                        match result with
                        | Error e -> return asErrorResult (box (formatDomainError e))
                        | Ok data ->
                            let items = parseSearchResults (Dyn.get data "results")
                            let rawText = formatSearchResults items
                            if items.IsEmpty then
                                return textResult rawText
                            else
                                let prompt =
                                    formatPrompt omp (WebsearchSummary(what, rawText)) |> List.head
                                let! summary =
                                    runSubagent
                                        pi
                                        (createObj [ "cwd", box "" ])
                                        [| "read" |]
                                        prompt
                                        abort
                                return textResult summary
                })
    ]

let private buildWebfetch (pi: obj) : obj =
    let tb = Dyn.get pi "typebox"
    createObj [
        "name", box "webfetch"
        "label", box "Web Fetch"
        "description", box (description "webfetch")
        "parameters",
            objectOf
                [| ("url", str Params.webfetchUrl tb)
                   ("extract_main", opt Params.webfetchExtractMain tb bool_)
                   ("prefer_llms_txt", opt Params.webfetchPreferLlmsTxt tb str)
                   ("prompt", opt Params.webfetchPrompt tb str)
                   ("timeout", opt Params.webfetchTimeout tb num) |]
                tb
        "execute",
            box(fun (_id: string) (params': obj) (signal: obj) ->
                promise {
                    let url = Dyn.str params' "url"
                    match validateFetchUrl url with
                    | Error msg -> return errorResult msg
                    | Ok () ->
                        let body = ResizeArray<(string * obj)>()
                        body.Add("url", box url)
                        let em = optBool params' "extract_main"
                        if em.IsSome then body.Add("extract_main", box em.Value)
                        let pl = optStr params' "prefer_llms_txt"
                        if pl.IsSome then body.Add("prefer_llms_txt", box pl.Value)
                        let pr = optStr params' "prompt"
                        if pr.IsSome then body.Add("prompt", box pr.Value)
                        let to_ = optInt params' "timeout"
                        if to_.IsSome then body.Add("timeout", box to_.Value)
                        let abort = if Dyn.isNullish signal then None else Some signal
                        let! result =
                            ollamaPost "web_fetch" (createObj (body |> Seq.toList)) abort
                        match result with
                        | Error e -> return asErrorResult (box (formatDomainError e))
                        | Ok data ->
                            let title = optStr data "title"
                            let byline = optStr data "byline"
                            let length_ = optInt data "length"
                            let content = optStr data "content"
                            return
                                textResult(
                                    formatFetchResponse
                                        { title = title
                                          byline = byline
                                          length = length_
                                          content = content })
                })
    ]

let registerWebTools (pi: obj) : unit =
    pi?registerTool(buildWebsearch pi)
    pi?registerTool(buildWebfetch pi)
