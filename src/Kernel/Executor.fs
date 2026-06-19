module VibeFs.Kernel.Executor

open System

type StrippedPipe =
    { pipe: string
      name: string
      count: int }

type StripResult =
    { script: string
      stripped: StrippedPipe list }

let private isWhitespace c = c = ' ' || c = '\t' || c = '\n' || c = '\r'
let private isDigit c = c >= '0' && c <= '9'
let private isLetter c = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
let private isTerminator c = c = ';' || c = '&' || c = '\n' || c = '#'

let rec private skipWhile pred (s: string) i =
    if i < s.Length && pred s.[i] then skipWhile pred s (i + 1) else i

let private takeWhile pred (s: string) i =
    let finish = skipWhile pred s i
    finish, s.[i..finish - 1]

let private parsePipe (s: string) (index: int) : (int * StrippedPipe) option =
    let afterSpace = skipWhile isWhitespace s (index + 1)
    let nameEnd, name = takeWhile isLetter s afterSpace
    if not (name = "head" || name = "tail") || not (nameEnd < s.Length && isWhitespace s.[nameEnd]) then
        None
    else
        let afterSpace2 = skipWhile isWhitespace s nameEnd
        let afterFlag =
            if afterSpace2 + 1 < s.Length && s.[afterSpace2] = '-' && s.[afterSpace2 + 1] = 'n' then
                skipWhile isWhitespace s (afterSpace2 + 2)
            elif afterSpace2 < s.Length && s.[afterSpace2] = '-' then afterSpace2 + 1
            else afterSpace2
        if afterFlag >= s.Length || not (isDigit s.[afterFlag]) then None
        else
            let countEnd, countStr = takeWhile isDigit s afterFlag
            let afterCount = skipWhile (fun c -> isWhitespace c && c <> '\n') s countEnd
            if afterCount >= s.Length || not (isTerminator s.[afterCount]) then None
            else Some(countEnd, { pipe = s.[index..countEnd - 1].Trim(); name = name; count = int countStr })

let private readSingleQuoted (s: string) (i: int) =
    match s.IndexOf("'", i + 1) with
    | -1 -> None
    | finish -> Some(s.[i..finish], finish + 1)

let private readDoubleQuoted (s: string) (i: int) =
    let rec loop j =
        if j >= s.Length then s.Length
        elif s.[j] = '"' then j + 1
        elif s.[j] = '\\' then loop (j + 2)
        else loop (j + 1)

    let next = loop (i + 1)
    s.[i..next - 1], next

let private readHashComment (s: string) (i: int) =
    match s.IndexOf("\n", i) with
    | -1 -> None
    | finish -> Some(s.[i..finish], finish + 1)

let private trimTrailingWhitespace (chars: char list) =
    chars |> List.rev |> List.skipWhile isWhitespace |> List.rev

let private appendSlice (chars: char list) (slice: string) =
    slice.ToCharArray() |> Array.fold (fun acc ch -> acc @ [ ch ]) chars

let private scan (script: string) : string * StrippedPipe list =
    let rec loop index buffered stripped =
        if index >= script.Length then
            System.String(List.toArray buffered), List.rev stripped
        else
            let ch = script.[index]
            if ch = '\'' then
                match readSingleQuoted script index with
                | Some(slice, next) -> loop next (appendSlice buffered slice) stripped
                | None -> loop script.Length (appendSlice buffered script.[index..]) stripped
            elif ch = '"' then
                let slice, next = readDoubleQuoted script index
                loop next (appendSlice buffered slice) stripped
            elif ch = '#' then
                match readHashComment script index with
                | Some(slice, next) -> loop next (appendSlice buffered slice) stripped
                | None -> loop script.Length (appendSlice buffered script.[index..]) stripped
            elif ch = '|' then
                match parsePipe script index with
                | Some(finish, pipe) -> loop finish (trimTrailingWhitespace buffered) (pipe :: stripped)
                | None -> loop (index + 1) (buffered @ [ ch ]) stripped
            else
                loop (index + 1) (buffered @ [ ch ]) stripped

    loop 0 [] []

let strip (script: string) : StripResult =
    let rec loop current acc =
        let next, found = scan current
        if List.isEmpty found then { script = current; stripped = acc }
        else loop next (found @ acc)
    loop script []

let headTail (s: string) (head: int) (tail: int) : string =
    if s.Length <= head + tail then s
    else s.Substring(0, head) + "..." + s.Substring(s.Length - tail)

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

type ExecuteResult =
    | Completed of output: string
    | Truncated of output: string * timeoutType: ExecutorTimeoutType
    | Failed of output: string
    | MissingExecutable of executable: string * output: string

let readOnlyReadCommands: Set<string> =
    Set.ofList
        [ "head"; "tail"; "sed"; "cat"; "grep"; "rg"; "find"; "less"; "more"
          "diff"; "wc"; "ls"; "tree" ]

let readOnlyWarning =
    "// 绝对禁止使用 executor 工具仅仅用于查找或者读写文件，请使用 read/investigator/coder 代替！"

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

let shouldSummarize (byteLength: string -> int) (output: string) : bool =
    byteLength output > summaryThresholdBytes

let prepareShellProgram (program: string) : string =
    (strip program).script

let describeResultTag (result: ExecuteResult) (timeoutType: ExecutorTimeoutType) : string =
    match result with
    | Completed _ -> "The following program has been executed (synchronous)."
    | Truncated _ -> $"The following program exceeded the {timeoutType} timeout and was killed. Partial output is below."
    | Failed _ -> "The following program exited with a non-zero status."
    | MissingExecutable(executable, _) -> $"The following program could not start because '{executable}' was not found."

let buildSummaryPrompt (byteLength: string -> int) (truncateToBytes: string -> int -> string) (options: ExecuteOptions) (result: ExecuteResult) : string =
    let rawOutput =
        match result with
        | Completed o
        | Truncated(o, _)
        | Failed o -> o
        | MissingExecutable(_, o) -> o

    let outputForSummary =
        if byteLength rawOutput > rawOutputCapBytes then
            truncateToBytes rawOutput rawOutputCapBytes + "\n\n[Output truncated to 1MB for summarization]"
        else
            rawOutput

    let depList = String.concat ", " options.dependencies
    let depInfo = if options.dependencies.IsEmpty then "" else $"Dependencies: {depList}\n\n"

    [ describeResultTag result options.timeoutType
      ""
      "Program:"
      options.program
      ""
      depInfo.TrimEnd()
      "Summarize the output. Highlight successes, failures, and key values. Do not invent details."
      ""
      "Raw output:"
      outputForSummary ]
    |> List.choose (fun s -> if System.String.IsNullOrEmpty(s) then None else Some s)
    |> String.concat "\n"
