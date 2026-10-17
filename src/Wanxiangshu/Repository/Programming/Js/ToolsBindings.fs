namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling

/// JS-010/JS-016: the api object injected into the sandbox — the only
/// authority a model program sees. Every member returns a JSON-compatible
/// object; failures carry `{ ok: false, code, reason }` with stable codes
/// (JS-019). Mutations write as-is: each edit/write is atomically persisted
/// through MutationFs at call time; the staging buffer records what was
/// actually written, so a later failure keeps earlier writes on disk (JS-012).
module JsToolsBindings =

    [<Import("resolve", "node:path")>]
    let private pathResolve (path: string) : string = jsNative

    [<Import("relative", "node:path")>]
    let private pathRelative (from: string) (toPath: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let private pathIsAbsolute (path: string) : bool = jsNative

    [<Import("realpathSync", "node:fs")>]
    let private realpathSync (path: string) : string = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("readdirSync", "node:fs")>]
    let private readdirSync (path: string) : string array = jsNative

    [<Import("lstatSync", "node:fs")>]
    let private lstatSync (path: string) : obj = jsNative

    [<Import("renameSync", "node:fs")>]
    let private renameSync (fromPath: string) (toPath: string) : unit = jsNative

    [<Import("rmdirSync", "node:fs")>]
    let private rmdirSync (path: string) : unit = jsNative

    [<Emit("$0.isDirectory()")>]
    let private statIsDirectory (stat: obj) : bool = jsNative

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

    let private readPathResolutionFailure path (error: obj) =
        if string error?code = "ENOENT" then
            JsFailure.FileNotFound path
        else
            JsFailure.FileReadFailed path

    let private resolveReadPath path =
        try
            Ok(realpathSync path)
        with error ->
            Error(readPathResolutionFailure path error)

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

    let private makeReadMember
        (root: string)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        =
        "read"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path
                let! readPath = resolveReadPath full
                let! readRoot = resolveReadPath root
                let relative = (pathRelative readRoot readPath).Replace("\\", "/")

                do!
                    if relative = ".." || relative.StartsWith("../") || pathIsAbsolute relative then
                        Error(JsFailure.PathDenied path)
                    else
                        Ok()

                let! text = JsUtf8Fs.readUtf8Classified readPath
                readSnapshots.Add { Path = path; Text = text }
                explicitReads.Add { Path = readPath; ResultBytes = text }

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
                let mutation = JsStagedMutation.Rewrite(path, current, replacement)

                // As-is write: the single-mutation commit plan validates the
                // target is still fresh (FILE_CHANGED otherwise) and atomically
                // persists this one write; it never rolls back earlier writes.
                do! JsMutationFs.commitPlan root (JsTransaction.commitPlan [ mutation ])
                staging.Add mutation
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

                let mutation = JsStagedMutation.Create(path, text)

                // As-is write: the single-mutation commit plan creates the
                // file with a no-clobber flag (FILE_CHANGED if a third party
                // created it meanwhile) and persists immediately.
                do! JsMutationFs.commitPlan root (JsTransaction.commitPlan [ mutation ])
                staging.Add mutation
                return createObj [ "ok" ==> true ]
            }
            |> renderOutcome

    /// JS-020/JS-030 write-before check: a mutation target that was read
    /// through file() must still match its snapshot, exactly like edit. A
    /// third-party change is FILE_CHANGED with no implicit retry.
    let private checkSnapshotFreshness
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (path: string)
        (full: string)
        : Result<unit, JsFailure> =
        readSnapshots
        |> Seq.tryFind (fun snapshot -> snapshot.Path = path)
        |> function
            | None -> Ok()
            | Some snapshot ->
                match JsUtf8Fs.readUtf8Classified full with
                | Ok current when current = snapshot.Text -> Ok()
                | _ -> Error(JsFailure.FileChanged path)

    /// JS-020/JS-030 rm: delete a file or an empty directory; refuse a
    /// non-empty directory (DIRECTORY_NOT_EMPTY). Missing target is
    /// FILE_NOT_FOUND. Takes effect immediately (as-is) and records one
    /// durable Remove entry.
    let private makeRmMember
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        =
        "rm"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path

                if not (JsMutationFs.existsPath full) then
                    return! Error(JsFailure.FileNotFound path)

                do! checkSnapshotFreshness readSnapshots path full

                try
                    let stat = lstatSync full

                    if statIsDirectory stat then
                        let entries = readdirSync full

                        if entries.Length > 0 then
                            return! Error(JsFailure.DirectoryNotEmpty path)

                        rmdirSync full
                    else
                        unlinkSync full
                with error ->
                    if string error?code = "ENOTEMPTY" then
                        return! Error(JsFailure.DirectoryNotEmpty path)
                    elif string error?code = "ENOENT" then
                        return! Error(JsFailure.FileNotFound path)
                    else
                        return! Error(JsFailure.FileReadFailed path)

                staging.Add(JsStagedMutation.Remove path)
                return createObj [ "ok" ==> true; "removed" ==> path ]
            }
            |> renderOutcome

    /// JS-020/JS-030 mv: move or rename a file or directory (POSIX mv).
    /// Overwrite is allowed; a missing source is FILE_NOT_FOUND. Both sides
    /// resolve inside the capability boundary. A cross-device move (EXDEV) is
    /// refused and rolled back: renameSync is atomic, so on EXDEV neither side
    /// changed and no two-sided intermediate state is ever left behind.
    let private makeMvMember
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        =
        "mv"
        ==> fun (fromPath: string) (toPath: string) ->
            result {
                let! fromFull = resolveInside root fromPath
                let! toFull = resolveInside root toPath

                if not (JsMutationFs.existsPath fromFull) then
                    return! Error(JsFailure.FileNotFound fromPath)

                do! checkSnapshotFreshness readSnapshots fromPath fromFull

                if JsMutationFs.existsPath toFull then
                    do! checkSnapshotFreshness readSnapshots toPath toFull

                try
                    renameSync fromFull toFull
                with error ->
                    if string error?code = "EXDEV" then
                        return! Error(JsFailure.CrossDeviceMove fromPath)
                    else
                        return! Error(JsFailure.FileReadFailed fromPath)

                staging.Add(JsStagedMutation.Move(fromPath, toPath))
                return createObj [ "ok" ==> true; "moved" ==> fromPath; "destination" ==> toPath ]
            }
            |> renderOutcome

    /// PLAN S2 step 4: the park member. `await js.park(payload)` suspends the
    /// program at this await point: it fires the park signal (which the
    /// workflow races against the program promise) with the payload, then
    /// returns a deferred promise the host keeps the resolve/reject right to.
    /// `unpark` resolves the deferred with the injected value, letting the
    /// program continue as if the await returned that value. `abandon` rejects
    /// it with the `__jsAbandoned` marker so native try/finally runs. During
    /// an abandon cleanup window park is refused with
    /// CANNOT_PARK_WHILE_ABANDONING.
    let private makeParkMember (parkState: Wanxiangshu.Process.JsSandbox.ParkState) =
        "park"
        ==> fun (payload: obj) ->
            if parkState.AbandonRequested then
                failureObj JsFailure.CannotParkWhileAbandoning
            else
                parkState.ParkRequested <- true
                parkState.ParkPayload <- payload

                if not (isNull parkState.ParkSignalResolve) then
                    emitJsExpr (parkState.ParkSignalResolve, payload) "$0($1)" |> ignore

                // The deferred promise: the host keeps the resolve/reject
                // closures in ParkState. The program awaits this promise, so it
                // suspends here until resumed (unpark) or abandoned.
                emitJsExpr
                    (parkState)
                    "(() => { const state = $0; return new Promise((resolve, reject) => { state.ParkDeferredResolve = resolve; state.ParkDeferredReject = reject; }); })()"
                    : obj

    let private createApiCore
        (capabilities: Set<JsCapability>)
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        : obj =
        let members =
            [ if Set.contains JsCapability.Read capabilities then
                  makeReadMember root readSnapshots explicitReads

              if Set.contains JsCapability.Glob capabilities then
                  makeGlobMember root

              if Set.contains JsCapability.Grep capabilities then
                  makeGrepMember root readSnapshots

              if Set.contains JsCapability.Edit capabilities then
                  makeEditMember root staging

              if Set.contains JsCapability.Write capabilities then
                  makeWriteMember root staging

              if Set.contains JsCapability.Remove capabilities then
                  makeRmMember root staging readSnapshots

              if Set.contains JsCapability.Move capabilities then
                  makeMvMember root staging readSnapshots

              match parkState with
              | Some state -> makeParkMember state
              | None -> () ]

        createObj [ "js" ==> createObj members ]

    /// Build the api object for one sandbox run. `staging` records every
    /// mutation that was actually persisted as-is; the caller reports it. The
    /// park member is absent here (no park state); use `createApiWithPark`.
    let createApi
        (capabilities: Set<JsCapability>)
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        : obj =
        createApiCore capabilities root staging readSnapshots explicitReads None

    /// Build the api object for one sandbox run that carries a park lifecycle
    /// state (PLAN S2 step 4). `parkState` is the mutable object shared with
    /// the sandbox wrapper; the generated `js.park` member suspends through it.
    let createApiWithPark
        (capabilities: Set<JsCapability>)
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState)
        : obj =
        createApiCore capabilities root staging readSnapshots explicitReads (Some parkState)

