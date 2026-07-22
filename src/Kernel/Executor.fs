module Wanxiangshu.Kernel.Executor

open System
open Wanxiangshu.Kernel.ExecutorStrip
open Wanxiangshu.Kernel.ToolOutputInfoTypes

type ExecutionDeadline = { EndsAt: float }

module ExecutionDeadline =
    let start (now: unit -> float) (budgetMs: int) : ExecutionDeadline = { EndsAt = now () + float budgetMs }

    let remainingMs (now: unit -> float) (deadline: ExecutionDeadline) : int = max 0 (int (deadline.EndsAt - now ()))

type StrippedPipe = ExecutorStrip.StrippedPipe
type StripResult = ExecutorStrip.StripResult
let strip = ExecutorStrip.strip

let headTail (s: string) (head: int) (tail: int) : string =
    if s.Length <= head + tail then
        s
    else
        s.Substring(0, head) + "..." + s.Substring(s.Length - tail)

type ExecutorLanguage =
    | Shell
    | Python
    | Javascript

type ExecutorTimeoutType =
    | Short
    | Long

let languages: ExecutorLanguage list = [ Shell; Python; Javascript ]

let timeoutMs =
    function
    | Short -> 10_000
    | Long -> 100_000

type ExecuteOptions =
    { command: string
      language: ExecutorLanguage
      dependencies: string list
      timeoutType: ExecutorTimeoutType
      cwd: string option
      whatToSummarize: string
      maxBytes: int }

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

let private isSpawnFailedMessage (output: string) = output.StartsWith "spawn failed:"

[<RequireQualifiedAccess>]
type ExecutorStatus =
    | Completed of exitCode: int
    | KilledTimeout
    | ExitError of exitCode: int option
    | Signal of string
    | SpawnFailed
    | MissingExecutable

let executorStatusText =
    function
    | ExecutorStatus.Completed _ -> "completed"
    | ExecutorStatus.KilledTimeout -> "killed_timeout (Output Truncated)"
    | ExecutorStatus.ExitError _ -> "exit_error"
    | ExecutorStatus.Signal sig' -> sig'
    | ExecutorStatus.SpawnFailed -> "spawn_failed"
    | ExecutorStatus.MissingExecutable -> "missing_executable"

let resolveExecutorStatus (result: ExecuteResult) : ExecutorStatus =
    match result with
    | Completed(_, code) -> ExecutorStatus.Completed code
    | Truncated(_, _) -> ExecutorStatus.KilledTimeout
    | Failed(_, Some c, _) -> ExecutorStatus.ExitError(Some c)
    | Failed(_, None, Some sig') when sig' <> "" -> ExecutorStatus.Signal sig'
    | Failed(output, _, _) when isSpawnFailedMessage output -> ExecutorStatus.SpawnFailed
    | Failed(_, None, _) -> ExecutorStatus.ExitError None
    | MissingExecutable _ -> ExecutorStatus.MissingExecutable

let applyExecutorStatus (result: ExecuteResult) (msg: ToolOutputMessage) : ToolOutputMessage =
    let status = resolveExecutorStatus result

    let codeOpt =
        match status with
        | ExecutorStatus.Completed code -> Some code
        | ExecutorStatus.ExitError code -> code
        | _ -> None

    { msg with
        status = Some(executorStatusText status)
        exitCode = codeOpt }

let readOnlyReadCommands: Set<string> =
    Set.ofList
        [ "head"
          "tail"
          "sed"
          "cat"
          "grep"
          "rg"
          "find"
          "less"
          "more"
          "diff"
          "wc"
          "ls"
          "tree" ]

let shouldAppendReadOnlyWarning (command: string) (language: ExecutorLanguage) : bool =
    match language with
    | Shell ->
        let stripped = (strip command).script

        let words =
            stripped.Split([| ' '; '\t'; '\n'; '|'; '&'; ';' |], StringSplitOptions.RemoveEmptyEntries)

        words
        |> Array.exists (fun word ->
            let bare = word.Split('/') |> Array.last
            Set.contains bare readOnlyReadCommands)
    | _ -> false

let shouldSummarize (byteLength: string -> int) (maxBytes: int) (output: string) : bool = byteLength output > maxBytes

let prepareShellProgram (command: string) : string = (strip command).script

let prepareProgramForExecution (options: ExecuteOptions) : string =
    match options.language with
    | Shell -> prepareShellProgram options.command
    | _ -> options.command

let timeoutToString (value: ExecutorTimeoutType) : string =
    match value with
    | Short -> "short"
    | Long -> "long"

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

let parseTimeout (value: string) : ExecutorTimeoutType =
    match value.Replace("-", "").ToLowerInvariant() with
    | "long" -> Long
    | _ -> Short
