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

let fuzzyIteratorDescriptionHint =
    "Every result "
    + "ends with "
    + "iterator="
    + "\"...\"; feed this iterator value into the fuzzy_continue tool to get the next page; iteration is finished when it becomes "
    + "iterator="
    + "\"\"."

let fuzzyFindDescriptionOmpPrefix =
    "Search for files by fuzzy path text matching. Returns file paths ranked by relevance and frecency. Regex and glob syntax are not supported.\n\nFirst call: provide pattern (an array of strings) and optional path. To fetch the next page, call fuzzy_continue with the `iterator` field in the tool output.\nMultiple patterns run in parallel and results are grouped per pattern.\n"

let fuzzyFindDescriptionOmp =
    fuzzyFindDescriptionOmpPrefix + fuzzyIteratorDescriptionHint

let fuzzyGrepDescriptionOmpPrefix =
    "Search file contents using fuzzy-aware content search. Smart-case, git-aware, frecency-ranked.\n\nFirst call: provide pattern (an array of strings) and optional filters. To fetch the next page, call fuzzy_continue with the `iterator` field in the tool output.\nMultiple patterns run in parallel and results are grouped per pattern.\n"

let fuzzyGrepDescriptionOmp =
    fuzzyGrepDescriptionOmpPrefix + fuzzyIteratorDescriptionHint

/// Body text only. Runtime wraps withIterator when nextIterator is non-empty.
let buildGrepBody (body: string) (regexError: string option) : string =
    match regexError with
    | Some error -> body + "\n\nInvalid regex: " + error + ", used literal match"
    | None -> body
