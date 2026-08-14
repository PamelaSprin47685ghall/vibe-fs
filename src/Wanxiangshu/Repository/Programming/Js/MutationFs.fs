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

    /// JS-013: apply a commit plan under root — two phases. Phase 1 reads every
    /// original snapshot; any read failure aborts BEFORE any write (a target
    /// that cannot be snapshotted cannot be rolled back). Phase 2 writes all
    /// files; a write failure rolls back every already-written path
    /// (rewrites restored, creates removed) — all-or-nothing.
    let commitPlan (root: string) (plan: (string * string) list) : Result<unit, JsFailure> =
        let resolvePath (path: string) =
            if pathIsAbsolute path then
                pathResolve path
            else
                pathResolve (pathJoin root path)

        // Phase 1 — snapshot every target before touching anything.
        let snapshots =
            plan
            |> List.map (fun (path, _) ->
                let full = resolvePath path

                let existed =
                    try
                        existsSync full
                    with _ ->
                        false

                if existed then
                    try
                        Ok(path, Some(decodeUtf8 (readFileBuffer full)))
                    with _ ->
                        Error(JsFailure.FileReadFailed path)
                else
                    Ok(path, None))

        match
            List.tryPick
                (function
                | Error failure -> Some failure
                | Ok _ -> None)
                snapshots
        with
        | Some failure -> Error failure
        | None ->
            let snapshotList =
                snapshots
                |> List.choose (function
                    | Ok(path, original) -> Some(path, original)
                    | Error _ -> None)

            // Phase 2 — write all; roll back on the first failure.
            let rec apply (remaining: (string * string) list) (doneList: (string * string option) list) =
                match remaining with
                | [] -> Ok()
                | (path, newText) :: rest ->
                    let full = resolvePath path

                    try
                        writeFileSync full newText
                        apply rest ((path, snd (List.find (fun (p, _) -> p = path) snapshotList)) :: doneList)
                    with _ ->
                        // roll back everything applied so far
                        for (appliedPath, appliedOriginal) in doneList do
                            try
                                let appliedFull = resolvePath appliedPath

                                match appliedOriginal with
                                | Some text -> writeFileSync appliedFull text
                                | None ->
                                    if existsSync appliedFull then
                                        unlinkSync appliedFull
                            with _ ->
                                ()

                        Error JsFailure.TransactionCommitFailed

            apply plan []

    /// JS-015: rollback — restore originals / remove creates, reversed order.
    let rollbackPlan (root: string) (plan: (string * string option) list) : unit =
        for (path, original) in plan do
            try
                let full =
                    if pathIsAbsolute path then
                        pathResolve path
                    else
                        pathResolve (pathJoin root path)

                match original with
                | Some text -> writeFileSync full text
                | None ->
                    if existsSync full then
                        unlinkSync full
            with _ ->
                ()

    /// JS-015: undo one mutation only when the disk still holds the text we
    /// wrote (expectedCurrent). If the file was changed by someone else, or we
    /// never wrote it, nothing is touched — recovery never clobbers external
    /// edits.
    let undoIfMatches (root: string) (path: string) (expectedCurrent: string) (restoreTo: string option) : unit =
        let full = resolveToolPath root path

        match JsUtf8Fs.readUtf8Classified full with
        | Ok current when current = expectedCurrent ->
            try
                match restoreTo with
                | Some text -> writeFileSync full text
                | None ->
                    if existsSync full then
                        unlinkSync full
            with _ ->
                ()
        | _ -> ()
