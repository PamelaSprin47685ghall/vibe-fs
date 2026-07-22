module Wanxiangshu.Runtime.ExecutorFormat

open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.ToolOutputInfo

let private stdoutText (summaryOption: string option) (result: ExecuteResult) : string =
    Option.defaultValue (outputFromResult result) summaryOption

let private executorStatusValues (result: ExecuteResult) : string * int option * string option * bool =
    let status = resolveExecutorStatus result

    let exitCode =
        match status with
        | ExecutorStatus.Completed code -> Some code
        | ExecutorStatus.ExitError code -> code
        | _ -> None

    let signal =
        match result with
        | Failed(_, _, Some sig') when sig' <> "" -> Some sig'
        | _ -> None

    let truncated =
        match result with
        | Truncated _ -> true
        | _ -> false

    executorStatusText status, exitCode, signal, truncated

let formatToolResponse (result: ExecuteResult) (summaryOption: string option) : ToolOutputMessage =
    let stdout = stdoutText summaryOption result
    let status, exitCode, signal, truncated = executorStatusValues result

    { empty with
        content =
            Executor
                { stdout = stdout
                  stderr = None
                  exitCode = exitCode
                  signal = signal
                  status = status
                  truncated = truncated
                  summary = summaryOption } }

let prependSafetyWarning (msg: ToolOutputMessage) (command: string) (language: ExecutorLanguage) : ToolOutputMessage =
    if not (shouldAppendReadOnlyWarning command language) then
        msg
    else
        { msg with hint = Some hintExecutorMisuse }

let prependSafetyWarningForExecution (msg: ToolOutputMessage) (options: ExecuteOptions) : ToolOutputMessage =
    prependSafetyWarning msg (prepareProgramForExecution options) options.language

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
