module VibeFs.Kernel.ExecutorKernel

open Fable.Core
open VibeFs.Kernel.HeadTail

let private byteLength (s: string) : int =
    if System.String.IsNullOrEmpty s then 0
    else
        let mutable count = 0
        let mutable i = 0
        while i < s.Length do
            let high = int s.[i]
            if high >= 0xD800 && high <= 0xDBFF && i + 1 < s.Length then
                let low = int s.[i + 1]
                let cp = 0x10000 + ((high - 0xD800) <<< 10) + (low - 0xDC00)
                if cp < 0x110000 then count <- count + 4
                i <- i + 2
            else
                count <-
                    count +
                    if high < 0x80 then 1
                    elif high < 0x800 then 2
                    else 3
                i <- i + 1
        count

let private truncateToBytes (s: string) (maxBytes: int) : string =
    let mutable result = ""
    let mutable bytes = 0
    let mutable i = 0
    while i < s.Length do
        let high = int s.[i]
        let charLen =
            if high >= 0xD800 && high <= 0xDBFF && i + 1 < s.Length then 2 else 1
        let codePoint = s.Substring(i, charLen)
        let cpBytes = byteLength codePoint
        if bytes + cpBytes > maxBytes then i <- s.Length
        else
            result <- result + codePoint
            bytes <- bytes + cpBytes
            i <- i + charLen
    result

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

/// Shell commands that only read — the executor is not a file browser.  When the
/// first word of a shell command is one of these, prepend a usage warning.
let readOnlyReadCommands: Set<string> =
    Set.ofList
        [ "head"; "tail"; "sed"; "cat"; "grep"; "rg"; "find"; "less"; "more"
          "diff"; "wc"; "ls"; "tree" ]

let readOnlyWarning =
    "// 绝对禁止使用 executor 工具仅仅用于查找或者读写文件，请使用专门工具例如 read/greper/editor 代替！"

/// Prepend the read-only warning when a shell command starts with a file-reading
/// verb.  Other languages pass through unchanged.
let formatSafetyWarning (output: string) (program: string) (language: ExecutorLanguage) : string =
    match language with
    | Shell ->
        match program.Trim().Split([| ' '; '\t'; '\n' |]) |> Array.tryHead with
        | None -> output
        | Some first ->
            let bare = first.Split('/') |> Array.tryLast |> Option.defaultValue ""
            if Set.contains bare readOnlyReadCommands then $"{readOnlyWarning}\n{output}" else output
    | _ -> output

/// Should the output be sent to a summariser instead of shown raw?
let shouldSummarize (output: string) : bool =
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
let buildSummaryPrompt (options: ExecuteOptions) (result: ExecuteResult) : string =
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
