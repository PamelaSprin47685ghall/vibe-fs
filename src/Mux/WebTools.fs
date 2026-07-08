module Wanxiangshu.Mux.WebTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell
open Wanxiangshu.Shell.PromiseStr

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SearchPrompts
open Wanxiangshu.Shell.WebSearchCodec
open Wanxiangshu.Shell.WebToolsCodec
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Mux.BuiltinTools
open Wanxiangshu.Mux.Delegate
open Wanxiangshu.Mux.SubagentTools
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Shell.WebSearchApi
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.Dyn

let websearchTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "websearch"
      description = description "websearch"
      parameters =
        mkSchema
            (createObj
                [ "query", box (strProp Params.websearchQuery)
                  "numResults", box (numProp Params.websearchNumResults)
                  "what_to_summarize", box (strProp Params.websearchWhatToSummarize) ])
            [| "query"; "what_to_summarize" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                match decodeWebsearchArgs args with
                | Error e -> resolveStr (wireDecodeFailure "websearch" e)
                | Ok ws ->
                    promise {
                        let body = createObj [ "query", box ws.Query; "max_results", box ws.NumResults ]
                        let! result = webApiPost "web_search" body runtime.AbortSignal

                        match result with
                        | Error e -> return webToolFailed "search" e
                        | Ok data ->
                            let items = parseSearchResults (Dyn.get data "results")
                            let rawResults = formatSearchResults items

                            if items.IsEmpty then
                                return rawResults
                            else
                                let prompt =
                                    formatPrompt mimocode (WebsearchSummary(ws.WhatToSummarize, rawResults))
                                    |> List.head

                                let opts = toolOptions toolNames summarizationRole summarizationAiSettingsAgentId
                                return! runMuxSubagent deps config summarizationAgentId prompt "Web search summary" opts
                    }
      condition = None }

let webfetchTool: ToolDefinition =
    { name = "webfetch"
      description = description "webfetch"
      parameters =
        mkSchema
            (createObj
                [ "url", box (strProp Params.webfetchUrl)
                  "extract_main", box (boolProp Params.webfetchExtractMain)
                  "prefer_llms_txt", box (strEnumProp Params.webfetchPreferLlmsTxt [| "auto"; "always"; "never" |])
                  "prompt", box (strProp Params.webfetchPrompt)
                  "timeout", box (numProp Params.webfetchTimeout) ])
            [| "url" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                match decodeWebfetchArgs args with
                | Error e -> resolveStr (wireDecodeFailure "webfetch" e)
                | Ok wf ->
                    promise {
                        let bodyEntries = ResizeArray<(string * obj)>()
                        bodyEntries.Add("url", box wf.Url)
                        addIfSome bodyEntries "extract_main" wf.ExtractMain
                        addIfSome bodyEntries "prefer_llms_txt" wf.PreferLlmsTxt
                        addIfSome bodyEntries "prompt" wf.Prompt
                        addIfSome bodyEntries "timeout" wf.Timeout
                        let body = createObj (Seq.toList bodyEntries)
                        let! result = webApiPost "web_fetch" body runtime.AbortSignal

                        match result with
                        | Error e -> return webToolFailed "fetch" e
                        | Ok data ->
                            let title =
                                if Dyn.isNullish (Dyn.get data "title") then
                                    None
                                else
                                    Some(Dyn.str data "title")

                            let byline =
                                if Dyn.isNullish (Dyn.get data "byline") then
                                    None
                                else
                                    Some(Dyn.str data "byline")

                            let length_ =
                                if Dyn.isNullish (Dyn.get data "length") then
                                    None
                                else
                                    Some(unbox<int> (Dyn.get data "length"))

                            let content =
                                if Dyn.isNullish (Dyn.get data "content") then
                                    None
                                else
                                    Some(Dyn.str data "content")

                            return
                                formatFetchResponse
                                    { title = title
                                      byline = byline
                                      length = length_
                                      content = content }
                    }
      condition = None }
