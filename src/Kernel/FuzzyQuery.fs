module VibeFs.Kernel.FuzzyQuery

open System.Text.RegularExpressions
open VibeFs.Kernel.ToolOutputInfo

let private escapeRegex (pattern: string) : string =
    let escapeChar (m: Match) = "\\" + m.Value
    Regex.Replace(pattern, @"[.*+?^${}()|[\]\\]", escapeChar)

let private isValidRegex (pattern: string) : bool =
    try
        Regex(pattern) |> ignore
        true
    with _ -> false

let detectGrepMode (pattern: string) : string =
    let escaped = escapeRegex pattern
    if pattern = escaped then "plain"
    elif isValidRegex pattern then "regex"
    else "plain"

let checkWildcardOnly (pattern: string) (mode: string) : bool =
    if mode = "plain" then false
    else Regex.IsMatch(pattern.Trim(), @"^(?:[.^$]*(?:[.][*+?]|\*|\+)[.^$]*|[.^$\s]*|\.\*\??|\.*[+?]?|\.+\??|\.|\*|\?)$")

type FuzzyFindParams =
    { pattern: string option
      path: string option
      limit: int option
      iterator: string option }

type FuzzyGrepParams =
    { pattern: string option
      path: string option
      exclude: string list
      caseSensitive: bool option
      context: int option
      limit: int option
      iterator: string option }

type FuzzyFindState = { query: string; pageSize: int; pageIndex: int; externalBasePath: string option }

type FuzzyGrepState =
    { query: string
      mode: string
      smartCase: bool
      beforeContext: int
      afterContext: int
      pageSize: int
      externalBasePath: string option }

type SearchOutcome = { output: string; isError: bool }

let buildGrepOutput (body: string) (regexError: string option) (nextIterator: string) : string =
    let body' =
        match regexError with
        | Some error -> body + "\n\nInvalid regex: " + error + ", used literal match"
        | None -> body
    if nextIterator = "" then body'
    else withIterator body' nextIterator
