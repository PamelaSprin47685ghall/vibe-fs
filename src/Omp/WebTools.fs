module VibeFs.Omp.WebTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.SearchPrompts
open VibeFs.Kernel.WebFetchGuard
open VibeFs.Omp.Codec
open VibeFs.Omp.Schema
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.OllamaClient
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell.WebSearchCodec
open VibeFs.Kernel.Domain

let registerWebTools (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"
    pi?registerTool(
        createObj [
            "name", box "websearch"
            "label", box "Ollama Search"
            "description", box "Search the web using Ollama web search."
            "parameters",
                objectOf
                    [| ("query", str "Natural language search query." tb)
                       ("numResults", opt "Maximum results to return." tb num) |]
                    tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) ->
                    promise {
                        let query = Dyn.str params' "query"
                        let num = defaultArg (optInt params' "numResults") 10
                        let abort = if Dyn.isNullish signal then None else Some signal
                        let! result = ollamaPost "web_search" (createObj [ "query", box query; "max_results", box num ]) abort
                        match result with
                        | Error e -> return asErrorResult (box (formatDomainError e))
                        | Ok data ->
                            let items = parseSearchResults (Dyn.get data "results")
                            return textResult (formatSearchResults items)
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "webfetch"
            "label", box "Ollama Fetch"
            "description", box "Fetch URL content using Ollama web fetch."
            "parameters",
                objectOf
                    [| ("url", str "URL to fetch." tb)
                       ("extract_main", opt "Whether to extract main content." tb bool_)
                       ("prefer_llms_txt", opt "auto, always, or never." tb str)
                       ("prompt", opt "Optional extraction task." tb str)
                       ("timeout", opt "Timeout in seconds." tb num) |]
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
                            let! result = ollamaPost "web_fetch" (createObj (body |> Seq.toList)) abort
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
        ])