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

    type private ResolvedCommitMutation =
        | ResolvedRewriteFile of path: string * resolvedPath: string * expectedCurrent: string * newText: string
        | ResolvedCreateFile of path: string * resolvedPath: string * newText: string

    type private ResolvedRollbackMutation =
        | ResolvedRestoreFile of resolvedPath: string * expectedCurrent: string * originalText: string
        | ResolvedRemoveCreatedFile of resolvedPath: string * expectedCurrent: string

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string) (data: string) : unit = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileWithOptions (path: string) (data: string) (options: obj) : unit = jsNative

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

    let private removeIfPresent (full: string) : unit =
        if existsSync full then
            unlinkSync full

    let private applyRestore (full: string) (restoreTo: string option) : unit =
        match restoreTo with
        | Some text -> writeFileSync full text
        | None -> removeIfPresent full

    let private undoMatchingFile (full: string) (restoreTo: string option) : unit =
        try
            applyRestore full restoreTo
        with _ ->
            ()

    let private undoIfMatchesResolved (full: string) (expectedCurrent: string) (restoreTo: string option) : unit =
        match JsUtf8Fs.readUtf8Classified full with
        | Ok current when current = expectedCurrent -> undoMatchingFile full restoreTo
        | _ -> ()

    let undoIfMatches (root: string) (path: string) (expectedCurrent: string) (restoreTo: string option) : unit =
        undoIfMatchesResolved (resolveToolPath root path) expectedCurrent restoreTo

    let private rollbackResolved mutation =
        match mutation with
        | ResolvedRestoreFile(resolvedPath, expectedCurrent, originalText) ->
            undoIfMatchesResolved resolvedPath expectedCurrent (Some originalText)
        | ResolvedRemoveCreatedFile(resolvedPath, expectedCurrent) ->
            undoIfMatchesResolved resolvedPath expectedCurrent None

    let private validateRewrite path resolvedPath expectedCurrent =
        match JsUtf8Fs.readUtf8Classified resolvedPath with
        | Ok current when current = expectedCurrent -> None
        | _ -> Some(JsFailure.FileChanged path)

    let private validateOne mutation =
        match mutation with
        | ResolvedRewriteFile(path, resolvedPath, expectedCurrent, _) ->
            validateRewrite path resolvedPath expectedCurrent
        | ResolvedCreateFile(path, resolvedPath, _) when existsPath resolvedPath -> Some(JsFailure.FileChanged path)
        | ResolvedCreateFile _ -> None

    let private validatePlan plan =
        plan
        |> List.tryPick validateOne
        |> function
            | Some failure -> Error failure
            | None -> Ok()

    let private validateMutation mutation =
        match validateOne mutation with
        | Some failure -> Error failure
        | None -> Ok mutation

    let private writeRewrite resolvedPath expectedCurrent newText =
        try
            writeFileSync resolvedPath newText
            Ok(ResolvedRestoreFile(resolvedPath, newText, expectedCurrent))
        with _ ->
            Error JsFailure.TransactionCommitFailed

    let private writeCreate path resolvedPath newText =
        try
            writeFileWithOptions resolvedPath newText (createObj [ "encoding" ==> "utf8"; "flag" ==> "wx" ])

            Ok(ResolvedRemoveCreatedFile(resolvedPath, newText))
        with
        | _ when existsPath resolvedPath -> Error(JsFailure.FileChanged path)
        | _ -> Error JsFailure.TransactionCommitFailed

    let private writeValidated mutation =
        match mutation with
        | ResolvedRewriteFile(_, resolvedPath, expectedCurrent, newText) ->
            writeRewrite resolvedPath expectedCurrent newText
        | ResolvedCreateFile(path, resolvedPath, newText) -> writeCreate path resolvedPath newText

    let private writeOne mutation =
        validateMutation mutation |> Result.bind writeValidated

    let private writeOrRollback applied mutation =
        match writeOne mutation with
        | Ok rollback -> Ok rollback
        | Error failure ->
            applied |> List.iter rollbackResolved
            Error failure

    let private applyWrites plan =
        let rec apply remaining applied =
            match remaining with
            | [] -> Ok()
            | mutation :: rest ->
                writeOrRollback applied mutation
                |> Result.bind (fun rollback -> apply rest (rollback :: applied))

        apply plan []

    let private resolveCommitMutation root mutation =
        match mutation with
        | JsCommitMutation.RewriteFile(path, expectedCurrent, newText) ->
            ResolvedRewriteFile(path, resolveToolPath root path, expectedCurrent, newText)
        | JsCommitMutation.CreateFile(path, newText) -> ResolvedCreateFile(path, resolveToolPath root path, newText)

    let private resolveRollbackMutation root mutation =
        match mutation with
        | JsRollbackMutation.RestoreFile(path, expectedCurrent, originalText) ->
            ResolvedRestoreFile(resolveToolPath root path, expectedCurrent, originalText)
        | JsRollbackMutation.RemoveCreatedFile(path, expectedCurrent) ->
            ResolvedRemoveCreatedFile(resolveToolPath root path, expectedCurrent)

    let commitPlan (root: string) (plan: JsCommitMutation list) : Result<unit, JsFailure> =
        let resolvedPlan = plan |> List.map (resolveCommitMutation root)

        validatePlan resolvedPlan |> Result.bind (fun () -> applyWrites resolvedPlan)

    let rollbackPlan (root: string) (plan: JsRollbackMutation list) : unit =
        plan |> List.map (resolveRollbackMutation root) |> List.iter rollbackResolved
