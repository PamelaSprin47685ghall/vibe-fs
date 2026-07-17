module Wanxiangshu.Runtime.ExecutorFormat

open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.ToolOutputInfo

let formatToolResponse (result: ExecuteResult) (summaryOption: string option) : string =
    let body = Option.defaultValue (outputFromResult result) summaryOption

    render
        { empty with
            info = executorInfoItems result
            body = body }

let prependSafetyWarning (output: string) (command: string) (language: ExecutorLanguage) : string =
    if not (shouldAppendReadOnlyWarning command language) then
        output
    else
        match tryParse output with
        | Some msg -> render (appendInfo (InfoItem.Hint hintExecutorMisuse) msg)
        | None ->
            render
                { empty with
                    info = [ InfoItem.Hint hintExecutorMisuse ]
                    body = output }

let prependSafetyWarningForExecution (output: string) (options: ExecuteOptions) : string =
    prependSafetyWarning output (prepareProgramForExecution options) options.language

let private summaryInputMaxBytes = 200_000

let buildSummaryPrompt
    (byteLength: string -> int)
    (truncateToBytes: string -> int -> string)
    (options: ExecuteOptions)
    (result: ExecuteResult)
    : string =
    let raw = outputFromResult result

    let capped =
        if byteLength raw > summaryInputMaxBytes then
            truncateToBytes raw summaryInputMaxBytes
            + "\n\n[Output truncated to 200000 bytes for summarization]"
        else
            raw

    let langStr = languageToString options.language
    let timeoutStr = timeoutToString options.timeoutType

    executorSummarizerPrompt
        options.whatToSummarize
        capped
        langStr
        options.command
        options.dependencies
        timeoutStr
        options.mode
