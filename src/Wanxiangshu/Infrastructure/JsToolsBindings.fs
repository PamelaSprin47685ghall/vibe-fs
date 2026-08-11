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
                            match JsToolsFs.glob root pattern 256 with
                            | Ok listing ->
                                createObj
                                    [ "ok" ==> true
                                      "paths" ==> (List.toArray listing.Paths)
                                      "truncated" ==> listing.Truncated ]
                            | Error failure -> failureObj failure
                        "grep"
                        ==> fun (needle: obj) (pattern: string) ->
                            let globPattern =
                                if isUndefined pattern || System.String.IsNullOrEmpty pattern then
                                    "**/*"
                                else
                                    pattern

                            match anchorOf needle with
                            | Error failure -> failureObj failure
                            | Ok spec ->
                                match spec with
                                | AnchorSpec.Exact text when System.String.IsNullOrEmpty text ->
                                    failureObj JsFailure.AnchorEmptyContent
                                | _ ->
                                    match JsToolsFs.grep root spec globPattern 500 with
                                    | Error failure -> failureObj failure
                                    | Ok listing ->
                                        let matches =
                                            listing.Matches
                                            |> List.map (fun hit ->
                                                createObj
                                                    [ "path" ==> hit.Path
                                                      "line" ==> hit.Line
                                                      "column" ==> hit.Column
                                                      "text" ==> hit.Text ])

                                        createObj
                                            [ "ok" ==> true
                                              "matches" ==> (List.toArray matches)
                                              "truncated" ==> listing.Truncated ]
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
