namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session

module GitTree =
    [<Import("execFileSync", "node:child_process")>]
    let private execFileSync (fileName: string) (arguments: string array) (options: obj) : string = jsNative

    [<Import("join", "node:path")>]
    let private joinPath (directory: string) (fileName: string) : string = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string) (encoding: string) : string = jsNative

    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    [<Emit("($0.update($1), $0.digest('hex'))")>]
    let private digest (hash: obj) (content: string) : string = jsNative

    let private options = createObj [ "encoding", box "utf8" ]

    let private command directory fileName arguments =
        execFileSync fileName (Array.append [| "-C"; directory |] arguments) options

    /// Dirty payload only: empty when the worktree matches HEAD with no untracked files.
    let private dirtyPayload directory =
        let diff =
            command directory "git" [| "diff"; "HEAD"; "--binary"; "--no-ext-diff"; "--" |]

        let untracked =
            command directory "git" [| "ls-files"; "--others"; "--exclude-standard" |]

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
                (command directory "git" [| "rev-parse"; "HEAD^{tree}" |]).Trim()
            with _ ->
                "NO_HEAD_TREE"

        let dirty = dirtyPayload directory

        if String.IsNullOrEmpty dirty then
            headTree
        else
            digest (createHash "sha256") (headTree + "\n" + dirty)

    let create (directory: string) : GitTreePort =
        { GetTreeHash = fun () -> treeHash directory }
