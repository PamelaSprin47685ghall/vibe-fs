module Wanxiangshu.Runtime.ExecutorFormat

open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.ToolOutputInfo

let formatToolResponse (result: ExecuteResult) (summaryOption: string option) : string =
    let bodyText = Option.defaultValue (outputFromResult result) summaryOption
    let msg = applyExecutorStatus result (withBody bodyText)
    render msg

let prependSafetyWarning (output: string) (command: string) (language: ExecutorLanguage) : string =
    if not (shouldAppendReadOnlyWarning command language) then
        output
    else
        let msg = { empty with body = Some output; hint = Some hintExecutorMisuse }
        render msg

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

    executorSummarizerPrompt options.whatToSummarize capped langStr options.command options.dependencies timeoutStr
