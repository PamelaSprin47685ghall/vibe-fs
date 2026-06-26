module VibeFs.Kernel.ExecutorStrip

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