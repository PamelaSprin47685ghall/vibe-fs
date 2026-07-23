module Wanxiangshu.Runtime.Tooling.ToolOutputToml

open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Serialization.TomlValue
open Wanxiangshu.Runtime.Serialization.Toml

let private nonEmpty (s: string option) : string option =
    match s with
    | Some v when System.String.IsNullOrWhiteSpace v -> None
    | _ -> s

let private nonEmptyList (xs: string list) : string list =
    xs |> List.filter (System.String.IsNullOrWhiteSpace >> not)

let private executorFields (e: ExecutorOutput) : (string * TomlValue) list =
    [ yield "stdout", String e.stdout
      yield "exit_status", String e.exitStatus
      yield "truncated", Boolean e.truncated
      match e.stderr with
      | Some s when s <> "" -> yield "stderr", String s
      | _ -> ()
      match e.exitCode with
      | Some c -> yield "exit_code", Integer c
      | None -> ()
      match e.signal with
      | Some s when s <> "" -> yield "signal", String s
      | _ -> ()
      match nonEmpty e.summary with
      | Some s -> yield "summary", String s
      | None -> () ]

let private annotationField (annotation: string option) : (string * TomlValue) list =
    match nonEmpty annotation with
    | Some a -> [ "annotation", String a ]
    | None -> []

let private fuzzyFindFields (f: FuzzyFindOutput) : (string * TomlValue) list =
    let matchTables =
        f.matches
        |> List.map (fun m ->
            [ yield "path", String m.path
              match nonEmpty m.pattern with
              | Some p -> yield "pattern", String p
              | None -> ()
              yield! annotationField m.annotation ])

    [ match nonEmpty f.pattern with
      | Some p -> yield "pattern", String p
      | None -> ()
      match f.totalMatched with
      | Some t -> yield "total_matched", Integer t
      | None -> ()
      match f.totalFiles with
      | Some t -> yield "total_files", Integer t
      | None -> ()
      yield "matches", TableArray matchTables ]

let private fuzzyGrepFields (g: FuzzyGrepOutput) : (string * TomlValue) list =
    let matchTables =
        g.matches
        |> List.map (fun m ->
            [ yield "path", String m.path
              yield "line", Integer m.line
              yield "content", String m.content
              match nonEmpty m.pattern with
              | Some p -> yield "pattern", String p
              | None -> ()
              if not (List.isEmpty m.contextBefore) then
                  yield "context_before", StringArray m.contextBefore
              if not (List.isEmpty m.contextAfter) then
                  yield "context_after", StringArray m.contextAfter
              yield! annotationField m.annotation ])

    [ match nonEmpty g.pattern with
      | Some p -> yield "pattern", String p
      | None -> ()
      match g.totalMatched with
      | Some t -> yield "total_matched", Integer t
      | None -> ()
      match nonEmpty g.regexFallbackError with
      | Some e -> yield "regex_fallback_error", String e
      | None -> ()
      yield "matches", TableArray matchTables ]

let private writeResultFields (w: WriteResultInfo) : (string * TomlValue) list =
    [ "path", String w.path
      "success", Boolean w.success
      "syntax_errors", StringArray (nonEmptyList w.syntaxErrors) ]

let toolOutputDocument (msg: ToolOutputMessage) : TomlValue =
    // Hints to the LLM must be disguised as read "extra://AGENTS.md" results, not
    // rendered as a bare `hint = "..."` field. The hint field is excluded from TOML output.
    let messageFields =
        [ match msg.syntax with
          | Some s when s <> "" -> yield "syntax", String s
          | _ -> ()
          match msg.iterator with
          | Some i when i <> "" -> yield "iterator", String i
          | _ -> ()
          match msg.status with
          | Some st when st <> "" -> yield "status", String st
          | _ -> () ]

    let contentFields =
        match msg.content with
        | Empty -> []
        | Plain s when System.String.IsNullOrWhiteSpace s -> []
        | Plain s -> [ "output", String s ]
        | Executor e -> executorFields e
        | FuzzyFind f -> fuzzyFindFields f
        | FuzzyGrep g -> fuzzyGrepFields g
        | WriteResult w -> writeResultFields w

    Table (messageFields @ contentFields)

let renderToolOutput (msg: ToolOutputMessage) : string =
    match toolOutputDocument msg with
    | Table [] -> ""
    | doc -> stringify doc

type SearchResultItem =
    { title: string
      url: string
      content: string }

let searchResultsDocument (results: SearchResultItem list) : TomlValue =
    let tables =
        results
        |> List.map (fun r -> [ "title", String r.title; "url", String r.url; "content", String r.content ])

    Table [ "results", TableArray tables ]

let renderSearchResults (results: SearchResultItem list) : string =
    searchResultsDocument results |> stringify

type FetchResult =
    { title: string option
      byline: string option
      length: int option
      content: string option }

let fetchResultDocument (fetch: FetchResult) : TomlValue =
    let mutable fields = []

    match fetch.title with
    | Some t when t <> "" -> fields <- fields @ [ "title", String t ]
    | _ -> ()

    match fetch.byline with
    | Some b when b <> "" -> fields <- fields @ [ "byline", String b ]
    | _ -> ()

    match fetch.length with
    | Some l -> fields <- fields @ [ "length", Integer l ]
    | None -> ()

    match fetch.content with
    | Some c -> fields <- fields @ [ "content", String c ]
    | None -> ()

    Table fields

let renderFetchResult (fetch: FetchResult) : string =
    match fetchResultDocument fetch with
    | Table [] -> ""
    | doc -> stringify doc

type CapsItem = { label: string; content: string }

let capsDocument (caps: CapsItem list) : TomlValue =
    let tables =
        caps
        |> List.map (fun c -> [ "label", String c.label; "content", String c.content ])

    Table [ "capabilities", TableArray tables ]

let renderCaps (caps: CapsItem list) : string = capsDocument caps |> stringify
