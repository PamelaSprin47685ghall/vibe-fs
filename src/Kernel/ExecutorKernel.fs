module VibeFs.Kernel.ExecutorKernel

open Fable.Core
open System
open VibeFs.Kernel.HeadTail

/// Languages the executor can spawn.  A closed set: adding one is a compile
/// error at every match site.
type ExecutorLanguage = Shell | Python | Javascript
type ExecutorTimeoutType = Short | Long | LastResort

let languages: ExecutorLanguage list = [ Shell; Python; Javascript ]

let timeoutMs = function Short -> 1000 | Long -> 10000 | LastResort -> 100_000
let summaryThresholdBytes = 8192
let rawOutputCapBytes = 1_048_576

type ExecuteOptions =
    { program: string
      language: ExecutorLanguage
      dependencies: string list
      timeoutType: ExecutorTimeoutType
      cwd: string option }

/// The four ways an execution can end.  Each carries only the data that matters
/// for that outcome — Truncated knows its budget, MissingExecutable knows its name.
type ExecuteResult =
    | Completed of output: string
    | Truncated of output: string * timeoutType: ExecutorTimeoutType
    | Failed of output: string
    | MissingExecutable of executable: string * output: string

/// Shell commands that only read — the executor is not a file browser.  When any
/// word of a shell command is one of these, prepend a usage warning.
let readOnlyReadCommands: Set<string> =
    Set.ofList
        [ "head"; "tail"; "sed"; "cat"; "grep"; "rg"; "find"; "less"; "more"
          "diff"; "wc"; "ls"; "tree" ]

let readOnlyWarning =
    "// 绝对禁止使用 executor 工具仅仅用于查找或者读写文件，请使用 read/reader/coder 代替！"

/// Prepend the read-only warning when a shell command contains a file-reading
/// verb anywhere, not just as the leading word.  Splits on shell separators
/// (space, tab, newline, |, &, ;) so verbs chained after &&, ||, ;, or a pipe
/// are caught, while a bare substring inside another word (e.g. "cat" in
/// "concat") is not.
let shouldAppendReadOnlyWarning (program: string) (language: ExecutorLanguage) : bool =
    match language with
    | Shell ->
        let stripped = (strip program).script
        let words = stripped.Split([| ' '; '\t'; '\n'; '|'; '&'; ';' |], StringSplitOptions.RemoveEmptyEntries)
        words
        |> Array.exists (fun word ->
            let bare = word.Split('/') |> Array.tryLast |> Option.defaultValue ""
            Set.contains bare readOnlyReadCommands)
    | _ -> false

let prependSafetyWarning (output: string) (program: string) (language: ExecutorLanguage) : string =
    if shouldAppendReadOnlyWarning program language then $"{readOnlyWarning}\n{output}" else output

/// Should the output be sent to a summariser instead of shown raw?
/// Accepts a byte-length function injected by the Shell (e.g. Buffer.byteLength).
let shouldSummarize (byteLength: string -> int) (output: string) : bool =
    byteLength output > summaryThresholdBytes

/// Strip head/tail pipes before executing a shell script.
let prepareShellProgram (program: string) : string =
    (strip program).script

/// Human-readable header for the summary prompt, depending on the outcome.
let describeResultTag (result: ExecuteResult) (timeoutType: ExecutorTimeoutType) : string =
    match result with
    | Completed _ -> "The following program has been executed (synchronous)."
    | Truncated _ -> $"The following program exceeded the {timeoutType} timeout and was killed. Partial output is below."
    | Failed _ -> "The following program exited with a non-zero status."
    | MissingExecutable(executable, _) -> $"The following program could not start because '{executable}' was not found."

/// Build the prompt handed to the summariser agent.
/// Accepts byte-length and truncation functions injected by the Shell.
let buildSummaryPrompt (byteLength: string -> int) (truncateToBytes: string -> int -> string) (options: ExecuteOptions) (result: ExecuteResult) : string =
    let rawOutput =
        match result with
        | Completed o | Truncated(o, _) | Failed o -> o
        | MissingExecutable(_, o) -> o
    let outputForSummary =
        if byteLength rawOutput > rawOutputCapBytes then
            truncateToBytes rawOutput rawOutputCapBytes
            + "\n\n[Output truncated to 1MB for summarization]"
        else
            rawOutput
    let depList = String.concat ", " options.dependencies
    let depInfo = if options.dependencies.IsEmpty then "" else $"Dependencies: {depList}\n\n"
    [ describeResultTag result options.timeoutType; ""; "Program:"; options.program
      ""; depInfo.TrimEnd()
      "Summarize the output. Highlight successes, failures, and key values. Do not invent details."
      ""; "Raw output:"; outputForSummary ]
    |> List.choose (fun s -> if System.String.IsNullOrEmpty(s) then None else Some s)
    |> String.concat "\n"
