namespace Wanxiangshu.Git

open Fable.Core
open Fable.Core.JsInterop

/// Minimal synchronous Git tree capability shared by Host/runtime consumers.
/// Review no longer owns repository state in the Relay architecture.
[<Struct>]
type GitTreePort = { GetTreeHash: unit -> string }

/// Subject verbs that own the production `git` executable token (§37).
/// Tree-hash / common-dir / orchestrator-host callers outside Infrastructure/Git|Persist
/// must go through here (pre-GitGateway cutover).
module GitSubject =

    /// Sole production spelling of the git executable name.
    [<Literal>]
    let Executable = "git"

    // ── why this uses execFileSync, not the Process module ───────────────────
    //
    // The Process module (NodeProcessHost.spawn / ProcessRunner.run) is async-
    // only: it spawns a child and returns Task<Result<…>>.  GitSubject answers
    // synchronous callers — HookDispatcher installs hooks during activation,
    // HookSync resolves the repository root / common-dir before entering the
    // async converge loop, GitTree.dirtyPayload builds the tree-hash payload
    // inside a synchronous commit-build path, and RuntimePath resolves the
    // journal common-dir once per workspace.
    //
    // Converting these to Task would cascade async through every caller's
    // signature for no ergonomic gain: each is a startup-time introspection
    // call where blocking the event loop for ~2 ms is the point — the caller
    // cannot proceed without the answer.  execFileSync is the correct tool.
    [<Import("execFileSync", "node:child_process")>]
    let private execFileSync (fileName: string) (arguments: string array) (options: obj) : string = jsNative

    let private utf8 = createObj [ "encoding", box "utf8" ]

    /// Run git with `-C directory` then `arguments`.
    let execIn (directory: string) (arguments: string array) : string =
        execFileSync Executable (Array.append [| "-C"; directory |] arguments) utf8

    // --- common-dir ---

    let revParseGitCommonDir (workspace: string) : string =
        (execIn workspace [| "rev-parse"; "--git-common-dir" |]).Trim()

    // --- tree-hash ---

    let diffHeadBinary (directory: string) : string =
        execIn directory [| "diff"; "HEAD"; "--binary"; "--no-ext-diff"; "--" |]

    let lsFilesUntracked (directory: string) : string =
        execIn directory [| "ls-files"; "--others"; "--exclude-standard" |]

    let lsFilesUntrackedZ (directory: string) : string =
        execIn directory [| "ls-files"; "--others"; "--exclude-standard"; "-z" |]

    let statusPorcelainV2Z (directory: string) : string =
        execIn directory [| "status"; "--porcelain=v2"; "-z"; "--untracked-files=all" |]

    let lsFilesStageZ (directory: string) : string =
        execIn directory [| "ls-files"; "--stage"; "-z" |]

    let hashObjectNoFilters (directory: string) (path: string) : string =
        (execIn directory [| "hash-object"; "--no-filters"; "--"; path |]).Trim()

    let revParseHeadTree (directory: string) : string =
        (execIn directory [| "rev-parse"; "HEAD^{tree}" |]).Trim()
