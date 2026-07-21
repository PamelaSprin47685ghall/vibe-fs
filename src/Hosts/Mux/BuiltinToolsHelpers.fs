module Wanxiangshu.Hosts.Mux.BuiltinToolsHelpers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Runtime.ExecutorToolsCodec
open Wanxiangshu.Hosts.Mux.SubagentTools
open Wanxiangshu.Hosts.Mux.Delegate

module Dyn = Wanxiangshu.Runtime.Dyn

let summarizationAgentId = "explore"
let summarizationRole = "executor"
let summarizationAiSettingsAgentId = "explore"

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let private byteLength (s: string) : int = nodeBuffer?byteLength (s, "utf-8")

let summarizeWhenNeeded
    (deps: obj)
    (config: obj)
    (toolNames: string array)
    (options: ExecuteOptions)
    (result: ExecuteResult)
    : JS.Promise<string> =
    promise {
        let output = outputFromResult result

        if not (shouldSummarize byteLength options.maxBytes output) then
            let formatted = formatToolResponse result None
            return prependSafetyWarningForExecution formatted options
        else
            let langStr = languageToString options.language
            let timeoutStr = timeoutToString options.timeoutType

            let prompt =
                formatPrompt
                    mimocode
                    (ExecutorSummary(
                        output,
                        langStr,
                        options.command,
                        options.dependencies,
                        timeoutStr,
                        options.whatToSummarize
                    ))
                |> List.head

            let opts = toolOptions toolNames summarizationRole summarizationAiSettingsAgentId
            let! report = runMuxSubagent deps config summarizationAgentId prompt "Executor summary" opts
            let formatted = formatToolResponse result (Some report)
            return prependSafetyWarningForExecution formatted options
    }

let formatHostReadResult (result: obj) : string =
    if Dyn.isNullish result then
        ""
    elif Dyn.typeIs result "string" then
        string result
    else
        let content = Dyn.str result "content"
        let warning = Dyn.str result "warning"
        let success = Dyn.get result "success"
        let error = Dyn.str result "error"

        if not (Dyn.isNullish success) then
            if Dyn.truthy success then
                match content, warning with
                | "", "" -> ""
                | "", warning -> warning
                | content, "" -> content
                | content, warning -> $"{content}\n\n{warning}"
            elif error <> "" then
                error
            else
                string result
        elif content <> "" then
            if warning = "" then content else $"{content}\n\n{warning}"
        elif error <> "" then
            error
        else
            string result

let hostReadResultIsDirectoryError (result: obj) : bool =
    if Dyn.isNullish result || not (Dyn.typeIs result "object") then
        false
    else
        let success = Dyn.get result "success"
        let error = Dyn.str result "error"

        not (Dyn.isNullish success)
        && not (Dyn.truthy success)
        && error.StartsWith "Path is a directory, not a file:"
