namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// JS-013/015: the ephemeral staging, all-or-nothing commit and rollback
/// adapter behind the js-* runtime bindings. Durable facts never live here;
/// EventStore owns them (JS-012).
module JsMutationFs =

    [<Import("readFileSync", "node:fs")>]
    let private readFileBuffer (path: string) : obj = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string) (data: string) : unit = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("resolve", "node:path")>]
    let private pathResolve (path: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let private pathIsAbsolute (path: string) : bool = jsNative

    [<Emit("new TextDecoder('utf-8', { fatal: true }).decode($0)")>]
    let private decodeUtf8 (buffer: obj) : string = jsNative

    /// Resolve a tool path under root: relative paths join root; absolute
    /// paths resolve as-is (the bindings enforce the inside-root boundary).
    let resolveToolPath (root: string) (path: string) : string =
        if pathIsAbsolute path then
            pathResolve path
        else
            pathResolve (pathJoin root path)

    /// Existence probe used by preflight (JS-013).
    let existsPath (path: string) : bool =
        try
            existsSync path
        with _ ->
            false

    let private tryExists (path: string) : bool =
        try
            existsSync path
        with _ ->
            false

    let private readExistingSnapshot (path: string) (full: string) : Result<string * string option, JsFailure> =
        try
            Ok(path, Some(decodeUtf8 (readFileBuffer full)))
        with _ ->
            Error(JsFailure.FileReadFailed path)

    let private snapshotOne (resolvePath: string -> string) (path: string) : Result<string * string option, JsFailure> =
        let full = resolvePath path

        if tryExists full then
            readExistingSnapshot path full
        else
            Ok(path, None)

    let private removeIfPresent (full: string) : unit =
        if existsSync full then
            unlinkSync full

    let private writeOrRemove (full: string) (original: string option) : unit =
        match original with
        | Some text -> writeFileSync full text
        | None -> removeIfPresent full

    let private restoreOneQuietly (resolvePath: string -> string) (path: string) (original: string option) : unit =
        try
            writeOrRemove (resolvePath path) original
        with _ ->
            ()

    let private rollbackApplied (resolvePath: string -> string) (doneList: (string * string option) list) : unit =
        for (appliedPath, appliedOriginal) in doneList do
            restoreOneQuietly resolvePath appliedPath appliedOriginal

    let private writeOne
        (resolvePath: string -> string)
        (snapshotList: (string * string option) list)
        (path: string)
        (newText: string)
        : Result<string * string option, JsFailure> =
        let full = resolvePath path

        try
            writeFileSync full newText
            Ok(path, snd (List.find (fun (p, _) -> p = path) snapshotList))
        with _ ->
            Error JsFailure.TransactionCommitFailed

    let private afterWrite
        (resolvePath: string -> string)
        (attempt: Result<string * string option, JsFailure>)
        (rest: (string * string) list)
        (doneList: (string * string option) list)
        (continueApply: (string * string) list -> (string * string option) list -> Result<unit, JsFailure>)
        : Result<unit, JsFailure> =
        match attempt with
        | Ok doneItem -> continueApply rest (doneItem :: doneList)
        | Error failure ->
            rollbackApplied resolvePath doneList
            Error failure

    let private applyWrites
        (resolvePath: string -> string)
        (snapshotList: (string * string option) list)
        (plan: (string * string) list)
        : Result<unit, JsFailure> =
        let rec apply (remaining: (string * string) list) (doneList: (string * string option) list) =
            match remaining with
            | [] -> Ok()
            | (path, newText) :: rest ->
                afterWrite resolvePath (writeOne resolvePath snapshotList path newText) rest doneList apply

        apply plan []

    /// JS-013: apply a commit plan under root — two phases. Phase 1 reads every
    /// original snapshot; any read failure aborts BEFORE any write (a target
    /// that cannot be snapshotted cannot be rolled back). Phase 2 writes all
    /// files; a write failure rolls back every already-written path
    /// (rewrites restored, creates removed) — all-or-nothing.
    let commitPlan (root: string) (plan: (string * string) list) : Result<unit, JsFailure> =
        let resolvePath path = resolveToolPath root path

        result {
            let! snapshotList =
                plan
                |> List.traverseResultM (fun (path, _) -> snapshotOne resolvePath path)

            return! applyWrites resolvePath snapshotList plan
        }

    let private restorePlanItem (root: string) (path: string) (original: string option) : unit =
        restoreOneQuietly (resolveToolPath root) path original

    /// JS-015: rollback — restore originals / remove creates, reversed order.
    let rollbackPlan (root: string) (plan: (string * string option) list) : unit =
        for (path, original) in plan do
            restorePlanItem root path original

    let private applyRestore (full: string) (restoreTo: string option) : unit =
        match restoreTo with
        | Some text -> writeFileSync full text
        | None -> removeIfPresent full

    let private undoMatchingFile (full: string) (restoreTo: string option) : unit =
        try
            applyRestore full restoreTo
        with _ ->
            ()

    /// JS-015: undo one mutation only when the disk still holds the text we
    /// wrote (expectedCurrent). If the file was changed by someone else, or we
    /// never wrote it, nothing is touched — recovery never clobbers external
    /// edits.
    let undoIfMatches (root: string) (path: string) (expectedCurrent: string) (restoreTo: string option) : unit =
        let full = resolveToolPath root path

        match JsUtf8Fs.readUtf8Classified full with
        | Ok current when current = expectedCurrent -> undoMatchingFile full restoreTo
        | _ -> ()
