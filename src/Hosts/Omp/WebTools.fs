module Wanxiangshu.Hosts.Omp.WebTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.SearchPrompts
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.WebFetchGuard
open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.Schema
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.FuzzySearch
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.WebSearchApi
open Wanxiangshu.Runtime.WebSearchCodec
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn

let private addRequired (schema: obj) (key: string) : unit =
    let existing = Dyn.get schema "required"

    if Dyn.isArray existing then
        existing?("push") (box key) |> ignore
    else
        schema?("required") <- box [| box key |]

let private websearchParameters (tb: obj) : obj =
    let schema =
        objectOf
            [| ("query", str Params.websearchQuery tb)
               ("numResults", opt Params.websearchNumResults tb num)
               ("what_to_summarize", str Params.websearchWhatToSummarize tb) |]
            tb

    addRequired schema "query"
    addRequired schema "what_to_summarize"
    schema

let private handleWebSearch
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    (params': obj)
    (signal: obj)
    (ctx: obj)
    : JS.Promise<ToolResult> =
    promise {
        let query = Dyn.str params' "query"
        let what = Dyn.str params' "what_to_summarize"

        if query = "" then
            return errorResult "Web search: query is required."
        elif what = "" then
            return errorResult "Web search: what_to_summarize is required."
        else
            let num = defaultArg (optInt params' "numResults") 10
            let abort = if Dyn.isNullish signal then None else Some signal

            let! result = ollamaPost "web_search" (createObj [ "query", box query; "max_results", box num ]) abort

            match result with
            | Error e -> return asErrorResult (box (formatDomainError e))
            | Ok data ->
                let items = parseSearchResults (Dyn.get data "results")
                let rawText = formatSearchResults items

                if items.IsEmpty then
                    return textResult rawText
                else
                    let prompt = formatPrompt omp (WebsearchSummary(what, rawText)) |> List.head

                    let! summary =
                        runSubagent
                            ExecutorTools.ompScope
                            pi
                            ctx
                            [| "read" |]
                            prompt
                            abort
                            fallbackRuntime
                            fallbackConfigOpt

                    return textResult summary
    }

let private buildWebsearch
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    : obj =
    let tb = Dyn.get pi "typebox"

    createObj
        [ "name", box "websearch"
          "label", box "Web Search"
          "description", box (description "websearch")
          "parameters", websearchParameters tb
          "execute",
          box (fun (_id: string) (params': obj) (signal: obj) (_onUpdate: obj) (ctx: obj) ->
              handleWebSearch pi fallbackRuntime fallbackConfigOpt params' signal ctx) ]

let private buildWebfetchRequest (params': obj) : (string * obj) list =
    let url = Dyn.str params' "url"
    let body = ResizeArray<(string * obj)>()
    body.Add("url", box url)

    let em = optBool params' "extract_main"

    if em.IsSome then
        body.Add("extract_main", box em.Value)

    let pl = optStr params' "prefer_llms_txt"

    if pl.IsSome then
        body.Add("prefer_llms_txt", box pl.Value)

    let pr = optStr params' "prompt"

    if pr.IsSome then
        body.Add("prompt", box pr.Value)

    let to_ = optInt params' "timeout"

    if to_.IsSome then
        body.Add("timeout", box to_.Value)

    body |> Seq.toList

let private handleWebfetchResponse (result: Result<obj, DomainError>) =
    match result with
    | Error e -> asErrorResult (box (formatDomainError e))
    | Ok data ->
        let title = optStr data "title"
        let byline = optStr data "byline"
        let length_ = optInt data "length"
        let content = optStr data "content"

        textResult (
            formatFetchResponse
                { title = title
                  byline = byline
                  length = length_
                  content = content }
        )

let private executeWebfetch (params': obj) (signal: obj) : JS.Promise<ToolResult> =
    promise {
        match validateFetchUrl (Dyn.str params' "url") with
        | Error msg -> return errorResult msg
        | Ok() ->
            let abort = if Dyn.isNullish signal then None else Some signal
            let! result = ollamaPost "web_fetch" (createObj (buildWebfetchRequest params')) abort
            return handleWebfetchResponse result
    }

let private buildWebfetch (pi: obj) : obj =
    let tb = Dyn.get pi "typebox"

    createObj
        [ "name", box "webfetch"
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
          box (fun (_id: string) (params': obj) (signal: obj) (_onUpdate: obj) (_ctx: obj) ->
              executeWebfetch params' signal) ]

let registerWebTools
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    : unit =
    pi?registerTool (buildWebsearch pi fallbackRuntime fallbackConfigOpt)
    pi?registerTool (buildWebfetch pi)
