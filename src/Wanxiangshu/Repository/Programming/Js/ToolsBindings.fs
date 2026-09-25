namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling

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

    let private renderOutcome (outcome: Result<obj, JsFailure>) : obj =
        match outcome with
        | Error failure -> failureObj failure
        | Ok value -> value

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
        elif System.String.IsNullOrEmpty(string (find?source)) then
            Error JsFailure.AnchorEmptyContent
        else
            Ok(AnchorSpec.Regex(string (find?source)))

    /// Exact empty text is a domain guard, separate from anchor parsing.
    let private requireNonEmptyExact (spec: AnchorSpec) : Result<unit, JsFailure> =
        match spec with
        | AnchorSpec.Exact text when System.String.IsNullOrEmpty text -> Error JsFailure.AnchorEmptyContent
        | _ -> Ok()

    let private makeReadMember (root: string) (readSnapshots: ResizeArray<JsReadSnapshot>) =
        "read"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path
                let! text = JsUtf8Fs.readUtf8Classified full
                readSnapshots.Add { Path = path; Text = text }

                return createObj [ "ok" ==> true; "path" ==> path; "text" ==> text; "byteCount" ==> text.Length ]
            }
            |> renderOutcome

    let private makeGlobMember (root: string) =
        "glob"
        ==> fun (pattern: string) ->
            task {
                let! globRes = JsGlobFs.glob root pattern

                return
                    match globRes with
                    | Ok listing -> createObj [ "ok" ==> true; "paths" ==> (List.toArray listing.Paths) ]
                    | Error failure -> failureObj failure
            }

    let private renderGrepListing (readSnapshots: ResizeArray<JsReadSnapshot>) (listing: JsAnchorFs.JsGrepListing) =
        listing.ReadSnapshots |> List.iter readSnapshots.Add

        let matches =
            listing.Matches
            |> List.map (fun hit ->
                createObj
                    [ "path" ==> hit.Path
                      "line" ==> hit.Line
                      "column" ==> hit.Column
                      "text" ==> hit.Text ])

        createObj [ "ok" ==> true; "matches" ==> (List.toArray matches) ]

    let private executeGrep
        (root: string)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (spec: AnchorSpec)
        (pattern: string)
        =
        task {
            let! grepRes = JsAnchorFs.grep root spec pattern

            return
                match grepRes with
                | Error failure -> failureObj failure
                | Ok listing -> renderGrepListing readSnapshots listing
        }

    let private runGrep (root: string) (readSnapshots: ResizeArray<JsReadSnapshot>) (needle: obj) (pattern: string) =
        task {
            if
                isUndefined pattern
                || not (isString pattern)
                || System.String.IsNullOrEmpty pattern
            then
                return failureObj JsFailure.AnchorInvalidPattern
            else
                let specRes =
                    result {
                        let! spec = anchorOf needle
                        do! requireNonEmptyExact spec
                        return spec
                    }

                match specRes with
                | Error e -> return failureObj e
                | Ok spec -> return! executeGrep root readSnapshots spec pattern
        }

    let private makeGrepMember (root: string) (readSnapshots: ResizeArray<JsReadSnapshot>) =
        "grep"
        ==> fun (needle: obj) (pattern: string) -> runGrep root readSnapshots needle pattern

    let private makeEditMember (root: string) (staging: ResizeArray<JsStagedMutation>) =
        "edit"
        ==> fun (path: string) (newText: obj) ->
            let replacement = string newText

            result {
                let! full = resolveInside root path
                let! current = JsUtf8Fs.readUtf8Classified full
                staging.Add(JsStagedMutation.Rewrite(path, current, replacement))
                return createObj [ "ok" ==> true ]
            }
            |> renderOutcome

    let private makeWriteMember (root: string) (staging: ResizeArray<JsStagedMutation>) =
        "write"
        ==> fun (path: string) (text: string) ->
            result {
                let! full = resolveInside root path

                do!
                    if JsMutationFs.existsPath full then
                        Error(JsFailure.FileAlreadyExists path)
                    else
                        Ok()

                staging.Add(JsStagedMutation.Create(path, text))
                return createObj [ "ok" ==> true ]
            }
            |> renderOutcome

    /// Build the api object for one sandbox run. `staging` collects every
    /// mutation the program makes; the caller commits or discards it.
    let createApi
        (capabilities: Set<JsCapability>)
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        : obj =
        let members =
            [ if Set.contains JsCapability.Read capabilities then
                  makeReadMember root readSnapshots

              if Set.contains JsCapability.Glob capabilities then
                  makeGlobMember root

              if Set.contains JsCapability.Grep capabilities then
                  makeGrepMember root readSnapshots

              if Set.contains JsCapability.Edit capabilities then
                  makeEditMember root staging

              if Set.contains JsCapability.Write capabilities then
                  makeWriteMember root staging ]

        createObj [ "js" ==> createObj members ]
