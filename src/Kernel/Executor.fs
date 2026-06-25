module VibeFs.Kernel.Executor

open System
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.ToolOutputInfo

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

let private allowedPipeCommands: Set<string> =
    Set.ofList [ "head"; "tail" ]

let private parsePipe (s: string) (index: int) : (int * StrippedPipe) option =
    let afterSpace = skipWhile isWhitespace s (index + 1)
    let nameEnd, name = takeWhile isLetter s afterSpace
    if not (Set.contains name allowedPipeCommands) || not (nameEnd < s.Length && isWhitespace s.[nameEnd]) then
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
            if afterCount >= s.Length || isTerminator s.[afterCount] then
                Some(countEnd, { pipe = s.[index..countEnd - 1].Trim(); name = name; count = int countStr })
            else None

let private trimTrailingWhitespaceRev (bufferedRev: char list) =
    List.skipWhile isWhitespace bufferedRev

let private appendSliceRev (bufferedRev: char list) (slice: string) =
    (slice.ToCharArray() |> Array.toList |> List.rev) @ bufferedRev

type private Cursor =
    | KeepSlice of slice: string * next: int
    | KeepRest of slice: string
    | DropPipe of next: int * pipe: StrippedPipe
    | LiteralChar

let private classifyToken (s: string) (i: int) : Cursor =
    match s.[i] with
    | '\'' ->
        match s.IndexOf("'", i + 1) with
        | -1 -> KeepRest s.[i..]
        | finish -> KeepSlice(s.[i..finish], finish + 1)
    | '"' ->
        let rec closeQuote j =
            if j >= s.Length then s.Length
            elif s.[j] = '"' then j + 1
            elif s.[j] = '\\' then closeQuote (j + 2)
            else closeQuote (j + 1)
        let next = closeQuote (i + 1)
        KeepSlice(s.[i..next - 1], next)
    | '#' ->
        match s.IndexOf("\n", i) with
        | -1 -> KeepRest s.[i..]
        | finish -> KeepSlice(s.[i..finish], finish + 1)
    | '|' ->
        match parsePipe s i with
        | Some(next, pipe) -> DropPipe(next, pipe)
        | None -> LiteralChar
    | _ -> LiteralChar

let private scan (script: string) : string * StrippedPipe list =
    let rec loop index bufferedRev stripped =
        if index >= script.Length then
            System.String(List.toArray (List.rev bufferedRev)), List.rev stripped
        else
            match classifyToken script index with
            | KeepSlice(slice, next) -> loop next (appendSliceRev bufferedRev slice) stripped
            | KeepRest slice -> loop script.Length (appendSliceRev bufferedRev slice) stripped
            | DropPipe(next, pipe) -> loop next (trimTrailingWhitespaceRev bufferedRev) (pipe :: stripped)
            | LiteralChar -> loop (index + 1) (script.[index] :: bufferedRev) stripped
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

type ExecuteOptions =
    { program: string
      language: ExecutorLanguage
      dependencies: string list
      timeoutType: ExecutorTimeoutType
      mode: string
      cwd: string option }

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

let executorKilledAfterTimeoutHint (timeoutMs: int) =
    $"Killed after {timeoutMs}ms. Partial output below."

let executorKilledBySignalHint (signal: string) =
    $"Killed by signal {signal}. Partial output below."

let private isSpawnFailedMessage (output: string) =
    output.StartsWith "spawn failed:"

let private executorInfoItems (result: ExecuteResult) : InfoItem list =
    match result with
    | Completed(_, code) ->
        [ InfoItem.Status "completed"; InfoItem.ExitCode code ]
    | Truncated(_, timeoutType) ->
        let ms = timeoutMs timeoutType
        [ InfoItem.Status "killed_timeout"
          InfoItem.TimeoutMs ms
          InfoItem.Hint (executorKilledAfterTimeoutHint ms) ]
    | Failed(output, code, sig') ->
        match sig', code with
        | Some s, _ when s <> "" ->
            [ InfoItem.Status "killed_signal"
              InfoItem.Signal s
              InfoItem.Hint (executorKilledBySignalHint s) ]
        | _, Some c ->
            [ InfoItem.Status "exit_error"; InfoItem.ExitCode c ]
        | _, None when isSpawnFailedMessage output ->
            [ InfoItem.Status "spawn_failed" ]
        | _, None ->
            [ InfoItem.Status "exit_error" ]
    | MissingExecutable _ ->
        [ InfoItem.Status "missing_executable" ]

let formatToolResponse (result: ExecuteResult) (summaryOption: string option) : string =
    let body = Option.defaultValue (outputFromResult result) summaryOption
    let truncated = match result with Truncated _ -> true | _ -> false
    let ref' =
        if truncated || summaryOption.IsSome then ToolOutputBodyRef.SeeBelowTruncated
        else ToolOutputBodyRef.SeeBelow
    render
        { empty with
            info = executorInfoItems result @ [ InfoItem.BodyRef ref' ]
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
        | None ->
            render
                { empty with
                    info =
                        [ InfoItem.Hint hintExecutorMisuse
                          InfoItem.BodyRef ToolOutputBodyRef.SeeBelow ]
                    body = output }

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
    executorSummarizerPrompt capped langStr options.program options.dependencies timeoutStr options.mode

let parseTimeout (value: string) : ExecutorTimeoutType =
    match value.Replace("-", "").ToLowerInvariant() with
    | "long" -> Long
    | "lastresort" -> LastResort
    | _ -> Short
