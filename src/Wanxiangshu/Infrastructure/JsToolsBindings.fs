namespace Wanxiangshu.Infrastructure

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain

/// JS-010/JS-016: the api object injected into the sandbox — the only
/// authority a model program sees. Every member returns a JSON-compatible
/// object; failures carry `{ ok: false, code, reason }` with stable codes
/// (JS-019). Mutations only stage (JS-012); the transaction engine commits.
module JsToolsBindings =

    [<Import("resolve", "node:path")>]
    let private pathResolve (path: string) : string = jsNative

    [<Import("relative", "node:path")>]
    let private pathRelative (from: string) (toPath: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let private pathIsAbsolute (path: string) : bool = jsNative

    [<Emit("$0 === undefined || $0 === null")>]
    let private isUndefined (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("new RegExp($0, 'g')")>]
    let private regexGlobal (pattern: string) : obj = jsNative

    [<Emit("""
        ((text, re) => {
          const hits = [];
          let m;
          while ((m = re.exec(text)) !== null) {
            hits.push({ index: m.index, text: m[0] });
            if (m.index === re.lastIndex) re.lastIndex += 1;
          }
          return hits;
        })($0, $1)
    """)>]
    let private allMatches (text: string) (re: obj) : obj array = jsNative

    let private failureObj (failure: JsFailure) : obj =
        createObj
            [ "ok" ==> false
              "code" ==> JsFailure.code failure
              "reason" ==> JsFailure.reason failure ]

    /// Path boundary: a path is legal iff its resolved form stays inside root.
    /// Absolute paths are allowed when they resolve inside root; anything else
    /// is PATH_DENIED (JS-007 capability boundary).
    let private resolveInside (root: string) (path: string) : Result<string, JsFailure> =
        let full =
            if System.String.IsNullOrEmpty path then
                pathResolve root
            else
                pathResolve (pathJoin root path)

        let rel = pathRelative root full

        if rel = "" || (not (rel.StartsWith "..") && not (pathIsAbsolute rel)) then
            Ok full
        else
            Error(JsFailure.PathDenied path)

    /// Interpret a JS find value: string → Exact anchor, RegExp → Regex anchor.
    let private anchorOf (find: obj) : Result<AnchorSpec, JsFailure> =
        if isString find then
            Ok(AnchorSpec.Exact(string find))
        else
            let source = string (find?source)

            if System.String.IsNullOrEmpty source then
                Error JsFailure.AnchorEmptyContent
            else
                Ok(AnchorSpec.Regex source)

    /// Build the api object for one sandbox run. `staging` collects every
    /// mutation the program makes; the caller commits or discards it.
    let createApi (root: string) (staging: ResizeArray<JsStagedMutation>) : obj =
        createObj
            [ "js"
              ==> createObj
                      [ "read"
                        ==> fun (path: string) ->
                            match resolveInside root path with
                            | Error failure -> failureObj failure
                            | Ok full ->
                                match JsToolsFs.readUtf8Classified full with
                                | Ok text ->
                                    createObj
                                        [ "ok" ==> true; "path" ==> path; "text" ==> text; "byteCount" ==> text.Length ]
                                | Error failure -> failureObj failure
                        "glob"
                        ==> fun (pattern: string) ->
                            match JsToolsFs.glob root pattern 256 16 with
                            | Ok paths -> createObj [ "ok" ==> true; "paths" ==> (List.toArray paths) ]
                            | Error failure -> failureObj failure
                        "grep"
                        ==> fun (needle: obj) (pattern: string) ->
                            match anchorOf needle with
                            | Error failure -> failureObj failure
                            | Ok spec ->
                                match JsToolsFs.glob root pattern 256 16 with
                                | Error failure -> failureObj failure
                                | Ok paths ->
                                    let matches =
                                        paths
                                        |> List.choose (fun rel ->
                                            match resolveInside root rel with
                                            | Error _ -> None
                                            | Ok full ->
                                                match JsToolsFs.readUtf8Classified full with
                                                | Error _ -> None
                                                | Ok text ->
                                                    match spec with
                                                    | AnchorSpec.Exact needleText ->
                                                        /// Every occurrence of needleText, in order (ordinal).
                                                        let rec allOccurrences (fromIndex: int) : (int * string) list =
                                                            let idx =
                                                                text.IndexOf(
                                                                    needleText,
                                                                    fromIndex,
                                                                    System.StringComparison.Ordinal
                                                                )

                                                            if idx < 0 then
                                                                []
                                                            else
                                                                (idx, needleText)
                                                                :: allOccurrences (idx + needleText.Length)

                                                        let hits = allOccurrences 0

                                                        if List.isEmpty hits then None else Some(rel, hits)
                                                    | AnchorSpec.Regex regexSource ->
                                                        let hits = allMatches text (regexGlobal regexSource)

                                                        if hits.Length = 0 then
                                                            None
                                                        else
                                                            Some(
                                                                rel,
                                                                hits
                                                                |> Array.toList
                                                                |> List.map (fun h -> (int (h?index), string (h?text)))
                                                            ))

                                    let flattened =
                                        matches
                                        |> List.collect (fun (rel, hits) ->
                                            hits
                                            |> List.map (fun (index, text) ->
                                                createObj [ "path" ==> rel; "index" ==> index; "text" ==> text ]))

                                    createObj [ "ok" ==> true; "matches" ==> (List.toArray flattened) ]
                        "edit"
                        ==> fun (path: string) (newText: obj) ->
                            let replacement = string newText

                            match resolveInside root path with
                            | Error failure -> failureObj failure
                            | Ok full ->
                                match JsToolsFs.readUtf8Classified full with
                                | Error failure -> failureObj failure
                                | Ok current ->
                                    staging.Add(JsStagedMutation.Rewrite(path, current, replacement))
                                    createObj [ "ok" ==> true ]
                        "write"
                        ==> fun (path: string) (text: string) ->
                            match resolveInside root path with
                            | Error failure -> failureObj failure
                            | Ok _ ->
                                staging.Add(JsStagedMutation.Create(path, text))
                                createObj [ "ok" ==> true ] ] ]
