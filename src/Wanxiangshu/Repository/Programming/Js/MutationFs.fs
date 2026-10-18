namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core
open Fable.Core.JsInterop

/// Ephemeral staging, all-or-nothing commit and rollback, bridged to
/// `wanxiangli/fs` (CUT-24). The write/rollback algorithm lives in the
/// library; this module keeps the main-repo signature stable. Durable facts
/// never live here; EventStore owns them (JS-012).
module JsMutationFs =

    [<Import("Filesystem", "wanxiangli/fs")>]
    let private Filesystem: obj = jsNative

    [<Emit("$0({ root: $1 })")>]
    let private createFilesystem (factory: obj) (root: string) : obj = jsNative

    /// Commit-plan mutation as the library's staged shape: a rewrite's
    /// originalText is the expectedCurrent content.
    let private commitToJs (mutation: JsCommitMutation) : obj =
        match mutation with
        | JsCommitMutation.RewriteFile(path, expectedCurrent, newText) ->
            createObj
                [ "kind" ==> "rewrite"
                  "path" ==> path
                  "originalText" ==> expectedCurrent
                  "newText" ==> newText ]
        | JsCommitMutation.CreateFile(path, newText) ->
            createObj [ "kind" ==> "create"; "path" ==> path; "text" ==> newText ]

    /// Rollback-plan mutation as the library's staged shape: a restore maps
    /// to Rewrite(originalText = text to restore, newText = expectedCurrent);
    /// a removeCreated maps to Create(expectedCurrent).
    let private rollbackToJs (mutation: JsRollbackMutation) : obj =
        match mutation with
        | JsRollbackMutation.RestoreFile(path, expectedCurrent, originalText) ->
            createObj
                [ "kind" ==> "rewrite"
                  "path" ==> path
                  "originalText" ==> originalText
                  "newText" ==> expectedCurrent ]
        | JsRollbackMutation.RemoveCreatedFile(path, expectedCurrent) ->
            createObj [ "kind" ==> "create"; "path" ==> path; "text" ==> expectedCurrent ]

    let private ofResult (result: obj) : Result<unit, JsFailure> =
        if unbox<bool> (result?ok) then
            Ok()
        else
            Error(JsFailure.ofWire (string (result?code)) (string (result?reason)))

    /// Resolve a tool path under root: relative paths join root; absolute
    /// paths resolve as-is (the bindings enforce the inside-root boundary).
    let resolveToolPath (root: string) (path: string) : string =
        let api = createFilesystem Filesystem root
        unbox<string> (api?resolveToolPath (path))

    /// Existence probe used by preflight (JS-013). Root-independent.
    let existsPath (path: string) : bool =
        let rootlessApi = createFilesystem Filesystem ""
        unbox<bool> (rootlessApi?existsPath (path))

    let undoIfMatches (root: string) (path: string) (expectedCurrent: string) (restoreTo: string option) : unit =
        let api = createFilesystem Filesystem root

        let restore =
            match restoreTo with
            | Some text -> box text
            | None -> null

        api?undoIfMatches (path, expectedCurrent, restore)

    let commitPlan (root: string) (plan: JsCommitMutation list) : Result<unit, JsFailure> =
        let api = createFilesystem Filesystem root
        let mutations = plan |> List.map commitToJs |> Array.ofList
        ofResult (api?applyCommit (mutations))

    let rollbackPlan (root: string) (plan: JsRollbackMutation list) : unit =
        let api = createFilesystem Filesystem root
        let mutations = plan |> List.map rollbackToJs |> Array.ofList
        api?applyRollback (mutations)

    // ---- rm/mv primitives (JS-020/JS-030, PLAN S2 step 9) -------------------

    [<Import("lstatSync", "node:fs")>]
    let private lstatSync (path: string) : obj = jsNative

    [<Import("readdirSync", "node:fs")>]
    let private readdirSync (path: string) : string array = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("rmdirSync", "node:fs")>]
    let private rmdirSync (path: string) : unit = jsNative

    [<Import("renameSync", "node:fs")>]
    let private renameSync (fromPath: string) (toPath: string) : unit = jsNative

    [<Import("statSync", "node:fs")>]
    let private statSync (path: string) : obj = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string) : unit = jsNative


    [<Emit("$0.isDirectory()")>]
    let private statIsDirectory (stat: obj) : bool = jsNative

    [<Emit("$0.size")>]
    let private statSize (stat: obj) : float = jsNative

    [<Emit("$0.mtime.toISOString()")>]
    let private statMtime (stat: obj) : string = jsNative

    let private removeDirectory (path: string) : Result<unit, JsFailure> =
        let entries = readdirSync path

        if entries.Length > 0 then
            Error(JsFailure.DirectoryNotEmpty path)
        else
            rmdirSync path
            Ok()

    let private removeExistingEntry (path: string) : Result<unit, JsFailure> =
        let stat = lstatSync path

        if statIsDirectory stat then
            removeDirectory path
        else
            unlinkSync path
            Ok()

    let private classifyRemoveError (path: string) (error: obj) : JsFailure =
        match string error?code with
        | "ENOTEMPTY" -> JsFailure.DirectoryNotEmpty path
        | "ENOENT" -> JsFailure.FileNotFound path
        | _ -> JsFailure.FileReadFailed path

    /// Delete a file or an empty directory; a non-empty directory is
    /// DIRECTORY_NOT_EMPTY, a missing target is FILE_NOT_FOUND. The caller
    /// (ToolsBindings) owns the path boundary (resolveInside) and the
    /// write-before FILE_CHANGED check; this primitive only maps the
    /// filesystem effect to the stable failure algebra. It takes effect
    /// immediately (as-is) and touches no durable fact — the per-mutation
    /// ledger belongs to the workflow's accounting channel.
    let removePath (path: string) : Result<unit, JsFailure> =
        try
            removeExistingEntry path
        with error ->
            Error(classifyRemoveError path error)

    let private classifyMoveError (fromPath: string) (error: obj) : JsFailure =
        if string error?code = "EXDEV" then
            JsFailure.CrossDeviceMove fromPath
        else
            JsFailure.FileReadFailed fromPath

    /// Move or rename a file or directory (POSIX mv) over renameSync
    /// (same-device atomic; overwrite allowed). A cross-device move (EXDEV)
    /// is refused: renameSync is atomic, so neither side changed and no
    /// two-sided intermediate state is ever left behind. Same ownership
    /// boundary as removePath — path checks live in the caller, durable
    /// accounting lives in the workflow.
    let movePath (fromPath: string) (toPath: string) : Result<unit, JsFailure> =
        try
            renameSync fromPath toPath
            Ok()
        with error ->
            Error(classifyMoveError fromPath error)

    let private handleStatError (path: string) (error: obj) : Result<obj, JsFailure> =
        if string error?code = "ENOENT" then
            Ok(createObj [ "exists" ==> false; "size" ==> 0; "mtime" ==> null; "isDir" ==> false ])
        else
            Error(JsFailure.FileReadFailed path)

    /// One path's metadata. A missing path is a normal observation
    /// (`exists = false`), not a failure; the caller decides what absence
    /// means. Any other filesystem error is FILE_READ_FAILED.
    let statPath (path: string) : Result<obj, JsFailure> =
        try
            let stat = statSync path

            Ok(
                createObj
                    [ "exists" ==> true
                      "size" ==> statSize stat
                      "mtime" ==> statMtime stat
                      "isDir" ==> statIsDirectory stat ]
            )
        with error ->
            handleStatError path error

    let private classifyMakeDirectoryError (path: string) (error: obj) : JsFailure =
        match string error?code with
        | "EEXIST" -> JsFailure.DirectoryExists path
        | "ENOENT" -> JsFailure.ParentDirectoryMissing path
        | _ -> JsFailure.FileReadFailed path

    /// Create exactly one directory (POSIX mkdir, non-recursive). An existing
    /// target is DIRECTORY_EXISTS; a missing parent is
    /// PARENT_DIRECTORY_MISSING so the caller can tell the two apart.
    let makeDirectory (path: string) : Result<unit, JsFailure> =
        try
            mkdirSync path
            Ok()
        with error ->
            Error(classifyMakeDirectoryError path error)
