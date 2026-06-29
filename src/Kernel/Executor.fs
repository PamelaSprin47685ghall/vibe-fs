module Wanxiangshu.Kernel.Executor

open System
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ExecutorStrip

type StrippedPipe = ExecutorStrip.StrippedPipe
type StripResult = ExecutorStrip.StripResult
let strip = ExecutorStrip.strip

let headTail (s: string) (head: int) (tail: int) : string =
    if s.Length <= head + tail then s
    else s.Substring(0, head) + "..." + s.Substring(s.Length - tail)

type ExecutorLanguage = Shell | Python | Javascript
type ExecutorTimeoutType = Short | Long | LastResort

let languages: ExecutorLanguage list = [ Shell; Python; Javascript ]
let timeoutMs = function Short -> 1000 | Long -> 10000 | LastResort -> 100_000
let summaryThresholdBytes = 8192

type ExecuteOptions =
    { program: string
      language: ExecutorLanguage
      dependencies: string list
      timeoutType: ExecutorTimeoutType
      mode: string
      cwd: string option
      whatToSummarize: string }

type ExecuteResult =
    | Completed of output: string * exitCode: int
    | Truncated of output: string * timeoutType: ExecutorTimeoutType
    | Failed of output: string * exitCode: int option * signal: string option
    | MissingExecutable of executable: string * output: string

let outputFromResult (result: ExecuteResult) : string =
    match result with
    | Completed(o, _)
    | Truncated(o, _)
    | Failed(o, _, _)
    | MissingExecutable(_, o) -> o

let private isSpawnFailedMessage (output: string) =
    output.StartsWith "spawn failed:"

let executorInfoItems (result: ExecuteResult) : InfoItem list =
    match result with
    | Completed(_, code) ->
        [ InfoItem.Status "completed"; InfoItem.ExitCode code ]
    | Truncated(_, timeoutType) ->
        [ InfoItem.Status "killed_timeout (Output Truncated)" ]
    | Failed(_, Some c, _) ->
        [ InfoItem.Status "exit_error"; InfoItem.ExitCode c ]
    | Failed(_, None, Some sig') when sig' <> "" ->
        [ InfoItem.Status sig' ]
    | Failed(output, _, _) when isSpawnFailedMessage output ->
        [ InfoItem.Status "spawn_failed" ]
    | Failed(_, None, _) ->
        [ InfoItem.Status "exit_error" ]
    | MissingExecutable _ ->
        [ InfoItem.Status "missing_executable" ]

let formatToolResponse (result: ExecuteResult) (summaryOption: string option) : string =
    let body = Option.defaultValue (outputFromResult result) summaryOption
    render
        { empty with
            info = executorInfoItems result
            body = body }

let readOnlyReadCommands: Set<string> =
    Set.ofList
        [ "head"; "tail"; "sed"; "cat"; "grep"; "rg"; "find"; "less"; "more"
          "diff"; "wc"; "ls"; "tree" ]

let shouldAppendReadOnlyWarning (program: string) (language: ExecutorLanguage) : bool =
    match language with
    | Shell ->
        let stripped = (strip program).script
        let words = stripped.Split([| ' '; '\t'; '\n'; '|'; '&'; ';' |], StringSplitOptions.RemoveEmptyEntries)
        words
        |> Array.exists (fun word ->
            let bare = word.Split('/') |> Array.last
            Set.contains bare readOnlyReadCommands)
    | _ -> false

let prependSafetyWarning (output: string) (program: string) (language: ExecutorLanguage) : string =
    if not (shouldAppendReadOnlyWarning program language) then output
    else
        match tryParse output with
        | Some msg -> render (appendInfo (InfoItem.Hint hintExecutorMisuse) msg)
        | None -> render { empty with info = [ InfoItem.Hint hintExecutorMisuse ]; body = output }

let shouldSummarize (byteLength: string -> int) (output: string) : bool =
    byteLength output > summaryThresholdBytes

let prepareShellProgram (program: string) : string =
    (strip program).script

let prepareProgramForExecution (options: ExecuteOptions) : string =
    match options.language with
    | Shell -> prepareShellProgram options.program
    | _ -> options.program

let prependSafetyWarningForExecution (output: string) (options: ExecuteOptions) : string =
    prependSafetyWarning output (prepareProgramForExecution options) options.language

let timeoutToString (value: ExecutorTimeoutType) : string =
    match value with
    | Short -> "short"
    | Long -> "long"
    | LastResort -> "last-resort"

let parseLanguage (value: string) : ExecutorLanguage =
    match value.ToLowerInvariant() with
    | "python" -> Python
    | "javascript" -> Javascript
    | _ -> Shell

let languageToString (value: ExecutorLanguage) : string =
    match value with
    | Python -> "python"
    | Javascript -> "javascript"
    | Shell -> "shell"

let private summaryInputMaxBytes = 1_048_576

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
            + "\n\n[Output truncated to 1MB for summarization]"
        else
            raw
    let langStr = languageToString options.language
    let timeoutStr = timeoutToString options.timeoutType
    executorSummarizerPrompt options.whatToSummarize capped langStr options.program options.dependencies timeoutStr options.mode

let parseTimeout (value: string) : ExecutorTimeoutType =
    match value.Replace("-", "").ToLowerInvariant() with
    | "long" -> Long
    | "lastresort" -> LastResort
    | _ -> Short
