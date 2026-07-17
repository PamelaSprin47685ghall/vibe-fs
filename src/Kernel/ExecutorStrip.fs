module Wanxiangshu.Kernel.ExecutorStrip

/// A pipe that was stripped from a command. Supports two kinds:
///   - CountPipe: head/tail with a numeric count argument
///   - FilterPipe: grep/egrep with filter arguments
type StrippedPipe =
    { raw: string
      name: string
      arguments: string
      count: int option }

type StripResult =
    { script: string
      stripped: StrippedPipe list }

let private isWhitespace c =
    c = ' ' || c = '\t' || c = '\n' || c = '\r'

let private isDigit c = c >= '0' && c <= '9'

let private isLetter c =
    (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')

let private isTerminator c =
    c = ';' || c = '&' || c = '\n' || c = '#'

let rec private skipWhile pred (s: string) i =
    if i < s.Length && pred s.[i] then
        skipWhile pred s (i + 1)
    else
        i

let private takeWhile pred (s: string) i =
    let finish = skipWhile pred s i
    finish, s.[i .. finish - 1]

let private allowedPipeCommands: Set<string> = Set.ofList [ "head"; "tail" ]

let private allowedGrepCommands: Set<string> = Set.ofList [ "grep"; "egrep" ]

let private parseCountPipe (s: string) (index: int) : (int * StrippedPipe) option =
    let afterSpace = skipWhile isWhitespace s (index + 1)
    let nameEnd, name = takeWhile isLetter s afterSpace

    if
        not (Set.contains name allowedPipeCommands)
        || not (nameEnd < s.Length && isWhitespace s.[nameEnd])
    then
        None
    else
        let afterSpace2 = skipWhile isWhitespace s nameEnd

        let afterFlag =
            if afterSpace2 + 1 < s.Length && s.[afterSpace2] = '-' && s.[afterSpace2 + 1] = 'n' then
                skipWhile isWhitespace s (afterSpace2 + 2)
            elif afterSpace2 < s.Length && s.[afterSpace2] = '-' then
                afterSpace2 + 1
            else
                afterSpace2

        if afterFlag >= s.Length || not (isDigit s.[afterFlag]) then
            None
        else
            let countEnd, countStr = takeWhile isDigit s afterFlag
            let afterCount = skipWhile (fun c -> isWhitespace c && c <> '\n') s countEnd

            if afterCount >= s.Length || isTerminator s.[afterCount] then
                Some(
                    countEnd,
                    { raw = s.[index .. countEnd - 1].Trim()
                      name = name
                      arguments = s.[nameEnd .. countEnd - 1].Trim()
                      count = Some(int countStr) }
                )
            else
                None

/// Scan grep arguments from `| grep` until a terminator or end of string.
/// Handles quoted strings so `| grep -E 'a|b'` does not confuse the `|` inside quotes.
let private parseGrepPipe (s: string) (index: int) : (int * StrippedPipe) option =
    let afterPipe = skipWhile isWhitespace s (index + 1)
    let nameEnd, name = takeWhile isLetter s afterPipe

    if
        not (Set.contains name allowedGrepCommands)
        || not (nameEnd < s.Length && (isWhitespace s.[nameEnd] || isTerminator s.[nameEnd]))
    then
        None
    else
        // Scan the rest of the pipe segment until a terminator
        let rec scanArgs i =
            if i >= s.Length then
                s.Length
            elif isTerminator s.[i] then
                i
            elif s.[i] = '\'' then
                match s.IndexOf("'", i + 1) with
                | -1 -> s.Length
                | finish -> scanArgs (finish + 1)
            elif s.[i] = '"' then
                let rec closeQuote j =
                    if j >= s.Length then s.Length
                    elif s.[j] = '"' then j + 1
                    elif s.[j] = '\\' then closeQuote (j + 2)
                    else closeQuote (j + 1)

                scanArgs (closeQuote (i + 1))
            else
                scanArgs (i + 1)

        let argsEnd = scanArgs nameEnd
        let argsStr = s.[nameEnd .. argsEnd - 1].Trim()

        Some(
            argsEnd,
            { raw = s.[index .. argsEnd - 1].Trim()
              name = name
              arguments = argsStr
              count = None }
        )

let private parsePipe (s: string) (index: int) : (int * StrippedPipe) option =
    match parseCountPipe s index with
    | Some result -> Some result
    | None -> parseGrepPipe s index

let private trimTrailingWhitespaceRev (bufferedRev: char list) = List.skipWhile isWhitespace bufferedRev

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
        KeepSlice(s.[i .. next - 1], next)
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

        if List.isEmpty found then
            { script = current; stripped = acc }
        else
            loop next (found @ acc)

    loop script []
