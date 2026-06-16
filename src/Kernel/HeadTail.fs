module VibeFs.Kernel.HeadTail

/// Strip trailing `| head -N` / `| tail -N` pipes from a shell script, while
/// respecting quotes and comments.  Returns the cleaned script and the removed
/// pipes.  Pure string scanning — no shell is ever invoked.
type StrippedPipe = { pipe: string; name: string; count: int }
type StripResult = { script: string; stripped: StrippedPipe list }

let private isWhitespace c = c = ' ' || c = '\t' || c = '\n' || c = '\r'
let private isDigit c = c >= '0' && c <= '9'
let private isLetter c = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
let private isTerminator c = c = ';' || c = '&' || c = '\n' || c = '#'

/// Skip while `pred` holds starting at `i`; return the new index.
let rec private skipWhile pred (s: string) i =
    if i < s.Length && pred s.[i] then skipWhile pred s (i + 1) else i

/// Read while `pred` holds; return (segmentEnd, substring).
let private takeWhile pred (s: string) i =
    let finish = skipWhile pred s i
    finish, s.[i..finish - 1]

/// Skip backwards while `pred` holds on the preceding char; return new index.
let rec private rskipWhile pred (s: string) i =
    if i > 0 && pred s.[i - 1] then rskipWhile pred s (i - 1) else i

/// Try to parse a `| head/tail -N` pipe at `index`.  Returns the index just past
/// the pipe and the pipe record, or None if this `|` is not such a pipe.
let private parsePipe (s: string) (index: int) : (int * StrippedPipe) option =
    let afterSpace = skipWhile isWhitespace s (index + 1)
    let nameEnd, name = takeWhile isLetter s afterSpace
    let isHeadOrTail = name = "head" || name = "tail"
    let spaceFollowsName = nameEnd < s.Length && isWhitespace s.[nameEnd]
    if not isHeadOrTail || not spaceFollowsName then None
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
            let terminatorFollows = afterCount >= s.Length || isTerminator s.[afterCount]
            if not terminatorFollows then None
            else
                Some(countEnd, { pipe = s.[index..countEnd - 1].Trim(); name = name; count = int countStr })

let private readSingleQuoted (s: string) (i: int) =
    match s.IndexOf("'", i + 1) with -1 -> None | finish -> Some(s.[i..finish], finish + 1)

let private readDoubleQuoted (s: string) (i: int) =
    let rec loop j =
        if j >= s.Length then s.Length
        elif s.[j] = '"' then j + 1
        elif s.[j] = '\\' then loop (j + 2)
        else loop (j + 1)
    let next = loop (i + 1)
    s.[i..next - 1], next

let private readHashComment (s: string) (i: int) =
    match s.IndexOf("\n", i) with -1 -> None | finish -> Some(s.[i..finish], finish + 1)

/// One scan pass over the script: copy through quotes/comments, cut head/tail pipes.
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

/// Repeatedly scan until no more head/tail pipes remain (they may be nested).
/// Pipes are prepended so the first-stripped (outermost) comes last, matching
/// left-to-right reading order of the original script.
let strip (script: string) : StripResult =
    let rec loop current acc =
        let next, found = scan current
        if List.isEmpty found then { script = current; stripped = acc }
        else loop next (found @ acc)
    loop script []

/// Keep `head` characters at the start and `tail` characters at the end,
/// inserting an ellipsis when the string is longer than head + tail.
let headTail (s: string) (head: int) (tail: int) : string =
    if s.Length <= head + tail then s
    else s.Substring(0, head) + "..." + s.Substring(s.Length - tail)
