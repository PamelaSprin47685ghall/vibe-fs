namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Git
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

module GitTree =
    [<Import("join", "node:path")>]
    let private joinPath (directory: string) (fileName: string) : string = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string) (encoding: string) : string = jsNative

    /// Dirty payload only: empty when the worktree matches HEAD with no untracked files.
    let private dirtyPayload directory =
        let diff = GitSubject.diffHeadBinary directory

        let untracked = GitSubject.lsFilesUntracked directory

        let files =
            untracked.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.sort
            |> Array.map (fun path ->
                let content = readFileSync (joinPath directory path) "utf8"
                sprintf "\n--UNTRACKED %s--\n%s" path content)
            |> String.concat ""

        diff + files

    /// HEAD tree object when clean; otherwise HEAD tree + dirty payload.
    /// A fully clean worktree must never collapse to the empty-string hash.
    let private treeHash directory =
        let headTree =
            try
                GitSubject.revParseHeadTree directory
            with _ ->
                "NO_HEAD_TREE"

        let dirty = dirtyPayload directory

        if String.IsNullOrEmpty dirty then
            headTree
        else
            HostDigest.sha256Hex (headTree + "\n" + dirty)

    let create (directory: string) : GitTreePort =
        { GetTreeHash = fun () -> treeHash directory }
