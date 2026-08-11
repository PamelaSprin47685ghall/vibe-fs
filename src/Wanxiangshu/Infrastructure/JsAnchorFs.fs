namespace Wanxiangshu.Infrastructure

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain

/// JS-006/020: the ordered anchor matching and host grep adapter behind the
/// js-* runtime bindings. Durable facts never live here; EventStore owns them
/// (JS-012).
module JsAnchorFs =

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Emit("new RegExp($0, 'g')")>]
    let private regexGlobal (pattern: string) : obj = jsNative

    [<Emit("""
        ((text, re, occurrence) => {
          let index = 0;
          let m;
          while ((m = re.exec(text)) !== null) {
            index += 1;
            if (index === occurrence) {
              const matched = m[0] === undefined ? '' : m[0];
              return { found: true, start: m.index, end: m.index + matched.length };
            }
            if (m.index === re.lastIndex) re.lastIndex += 1;
          }
          return { found: false };
        })($0, $1, $2)
    """)>]
    let private findRegexOccurrence (text: string) (re: obj) (occurrence: int) : obj = jsNative

    [<Emit("""
        ((text, re) => {
          const hits = [];
          let m;
          while ((m = re.exec(text)) !== null) {
            hits.push({ index: m.index, text: m[0] === undefined ? '' : m[0] });
            if (m.index === re.lastIndex) re.lastIndex += 1;
          }
          return hits;
        })($0, $1)
    """)>]
    let private allRegexHits (text: string) (re: obj) : obj array = jsNative

    [<Emit("$0.found === true")>]
    let private matchFound (m: obj) : bool = jsNative

    [<Emit("$0.start")>]
    let private matchStart (m: obj) : int = jsNative

    [<Emit("$0.end")>]
    let private matchEnd (m: obj) : int = jsNative

    type JsGrepHit =
        { Path: string
          Line: int
          Column: int
          Text: string }

    type JsGrepListing =
        { Matches: JsGrepHit list
          Truncated: bool }

    let private lineColumn (text: string) (index: int) : int * int =
        let rec loop i line col =
            if i >= index || i >= text.Length then line, col
            elif text.[i] = '\n' then loop (i + 1) (line + 1) 1
            else loop (i + 1) line (col + 1)

        loop 0 1 1

    let private exactHits (text: string) (needle: string) : (int * string) list =
        let rec go fromIndex =
            let idx = text.IndexOf(needle, fromIndex, System.StringComparison.Ordinal)

            if idx < 0 then
                []
            else
                (idx, needle) :: go (idx + max needle.Length 1)

        if needle = "" then [] else go 0

    let private regexHits (text: string) (source: string) : (int * string) list =
        try
            allRegexHits text (regexGlobal source)
            |> Array.toList
            |> List.map (fun hit -> (int (hit?index), string (hit?text)))
        with _ ->
            []

    /// JS-020: Host grep over gitignore-selected UTF-8 files.
    let grep (root: string) (spec: AnchorSpec) (pattern: string) (maxMatches: int) : Result<JsGrepListing, JsFailure> =
        let globPattern =
            if System.String.IsNullOrEmpty pattern then
                "**/*"
            else
                pattern

        let cap = max maxMatches 0

        match JsGlobFs.glob root globPattern 4096 with
        | Error failure -> Error failure
        | Ok listing ->
            let acc = ResizeArray<JsGrepHit>()
            // DSL-MUTABLE: resource — grep match truncation latch
            let mutable truncated = listing.Truncated

            for rel in listing.Paths do
                if acc.Count >= cap then
                    truncated <- true
                else
                    match JsUtf8Fs.readUtf8Classified (pathJoin root rel) with
                    | Error _ -> ()
                    | Ok text ->
                        let hits =
                            match spec with
                            | AnchorSpec.Exact needle -> exactHits text needle
                            | AnchorSpec.Regex source -> regexHits text source

                        for (index, matched) in hits do
                            if acc.Count >= cap then
                                truncated <- true
                            else
                                let line, column = lineColumn text index

                                acc.Add
                                    { Path = rel
                                      Line = line
                                      Column = column
                                      Text = matched }

            Ok
                { Matches = List.ofSeq acc
                  Truncated = truncated }

    /// JS-006: ordered string/RegExp anchor matching. `occurrence` is 1-based.
    /// Returns (start, end); zero-width matches yield start = end.
    let findAnchor (text: string) (spec: AnchorSpec) (occurrence: int) : Result<int * int, JsFailure> =
        /// Index of the nth occurrence of an exact needle, scanning forward
        /// from fromIndex (ordinal). -1 when absent.
        let rec nthIndex (needle: string) (fromIndex: int) (n: int) : int =
            if n <= 0 then
                -1
            else
                let idx = text.IndexOf(needle, fromIndex, System.StringComparison.Ordinal)

                if idx < 0 then -1
                elif n = 1 then idx
                else nthIndex needle (idx + needle.Length) (n - 1)

        if occurrence < 1 then
            Error JsFailure.AnchorInvalidPattern
        else
            match spec with
            | AnchorSpec.Exact needle ->
                if System.String.IsNullOrEmpty needle then
                    Error JsFailure.AnchorEmptyContent
                else
                    let start = nthIndex needle 0 occurrence

                    if start >= 0 then
                        Ok(start, start + needle.Length)
                    else
                        Error(JsFailure.AnchorNotFound("anchor did not match at occurrence " + string occurrence))
            | AnchorSpec.Regex pattern ->
                if System.String.IsNullOrEmpty pattern then
                    Error JsFailure.AnchorEmptyContent
                else
                    try
                        let re = regexGlobal pattern
                        let m = findRegexOccurrence text re occurrence

                        if matchFound m then
                            Ok(matchStart m, matchEnd m)
                        else
                            Error(JsFailure.AnchorNotFound("anchor did not match at occurrence " + string occurrence))
                    with _ ->
                        Error JsFailure.AnchorInvalidPattern

    /// JS-006: uniqueness refusal — when no occurrence is declared, the anchor
    /// must match exactly once.
    let requireUnique (text: string) (spec: AnchorSpec) : Result<int * int, JsFailure> =
        match findAnchor text spec 1 with
        | Error failure -> Error failure
        | Ok first ->
            match findAnchor text spec 2 with
            | Ok _ -> Error JsFailure.AnchorNotUnique
            | Error _ -> Ok first
