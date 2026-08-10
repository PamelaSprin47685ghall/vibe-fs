namespace Wanxiangshu.Journal

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Git

module RuntimePath =

    [<Import("isAbsolute", "node:path")>]
    let private isAbsolute (path: string) : bool = jsNative

    [<Import("resolve", "node:path")>]
    let private resolvePath (basePath: string) (relativePath: string) : string = jsNative

    [<Import("realpathSync", "node:fs")>]
    let private realpathSync (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private joinPath (left: string) (right: string) : string = jsNative

    [<Import("homedir", "node:os")>]
    let private homeDirectory () : string = jsNative

    [<Emit("process.env.XDG_STATE_HOME || ''")>]
    let private xdgStateHome () : string = jsNative

    let private runtimeDirectory root =
        joinPath (joinPath root "wanxiangshu-next") "runtimes"

    let private canonicalPath path =
        try
            realpathSync path
        with _ ->
            path

    /// `git rev-parse --git-common-dir` for a workspace, asked once per process.
    ///
    /// A checkout's common dir cannot move while the Host runs, and this is on the hot path: the
    /// journal resolves it on every store open, which showed up as ~69 `git` spawns in a single
    /// canary — synchronous ones, so each blocked the whole event loop for a fact that had not
    /// changed since the first answer.
    let private commonDirAnswers = Collections.Generic.Dictionary<string, string>()

    let private askGitCommonDir (workspace: string) : string =
        match commonDirAnswers.TryGetValue workspace with
        | true, cached -> cached
        | _ ->
            let answer = GitSubject.revParseGitCommonDir workspace
            commonDirAnswers.[workspace] <- answer
            answer

    let private stateDirectory workspace =
        let stateRoot =
            let configured = xdgStateHome ()

            if String.IsNullOrWhiteSpace configured then
                joinPath (joinPath (homeDirectory ()) ".local") "state"
            else
                configured

        runtimeDirectory (joinPath stateRoot (HostDigest.sha256Hex workspace))

    let gitCommonDir (workspace: string) : string =
        try
            let commonDirectory = askGitCommonDir workspace

            let resolved =
                if isAbsolute commonDirectory then
                    commonDirectory
                else
                    resolvePath workspace commonDirectory

            canonicalPath resolved
        with _ ->
            workspace

    let forWorkspace workspace =
        try
            let commonDirectory = askGitCommonDir workspace

            let gitDirectory =
                if isAbsolute commonDirectory then
                    commonDirectory
                else
                    resolvePath workspace commonDirectory

            runtimeDirectory (canonicalPath gitDirectory)
        with _ ->
            stateDirectory workspace
