module Wanxiangshu.Kernel.FuzzyQuery

open System.Text.RegularExpressions

let private escapeRegex (pattern: string) : string =
    let escapeChar (m: Match) = "\\" + m.Value
    Regex.Replace(pattern, @"[.*+?^${}()|[\]\\]", escapeChar)

let private isValidRegex (pattern: string) : bool =
    try
        Regex(pattern) |> ignore
        true
    with _ ->
        false

let detectGrepMode (pattern: string) : string =
    let escaped = escapeRegex pattern

    if pattern = escaped then "plain"
    elif isValidRegex pattern then "regex"
    else "plain"

let checkWildcardOnly (pattern: string) (mode: string) : bool =
    if mode = "plain" then
        false
    else
        Regex.IsMatch(pattern.Trim(), @"^(?:[.^$]*(?:[.][*+?]|\*|\+)[.^$]*|[.^$\s]*|\.\*\??|\.*[+?]?|\.+\??|\.|\*|\?)$")

type FuzzyFindParams =
    { pattern: string list
      path: string option
      limit: int option }

type FuzzyGrepParams =
    { pattern: string list
      path: string option
      exclude: string list
      searchIgnored: bool option
      caseSensitive: bool option
      context: int option
      limit: int option }

type FuzzyContinueParams = { iterator: string }

type FuzzyFindState =
    { query: string
      pageSize: int
      pageIndex: int
      externalBasePath: string option }

type FuzzyGrepState =
    { query: string
      mode: string
      smartCase: bool
      beforeContext: int
      afterContext: int
      pageSize: int
      externalBasePath: string option }

type SearchOutcome = { output: string; isError: bool }


