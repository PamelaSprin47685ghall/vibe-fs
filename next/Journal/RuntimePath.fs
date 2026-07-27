namespace Wanxiangshu.Next.Journal

open System
open Fable.Core
open Fable.Core.JsInterop

module RuntimePath =

    [<Import("execFileSync", "node:child_process")>]
    let private execFileSync (command: string) (args: string array) (options: obj) : string = jsNative

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

    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    [<Emit("$0.update($1).digest('hex')")>]
    let private digest (hash: obj) (value: string) : string = jsNative

    [<Emit("process.env.XDG_STATE_HOME || ''")>]
    let private xdgStateHome () : string = jsNative

    let private runtimeDirectory root =
        joinPath (joinPath root "wanxiangshu-next") "runtimes"

    let private canonicalPath path =
        try
            realpathSync path
        with _ ->
            path

    let private stateDirectory workspace =
        let stateRoot =
            let configured = xdgStateHome ()

            if String.IsNullOrWhiteSpace configured then
                joinPath (joinPath (homeDirectory ()) ".local") "state"
            else
                configured

        runtimeDirectory (joinPath stateRoot (digest (createHash "sha256") workspace))

    let internal gitCommonDir (workspace: string) : string =
        try
            let output =
                execFileSync
                    "git"
                    [| "-C"; workspace; "rev-parse"; "--git-common-dir" |]
                    (createObj [ "encoding", box "utf8" ])

            let commonDirectory = output.Trim()

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
            let commonDirectory =
                let output =
                    execFileSync
                        "git"
                        [| "-C"; workspace; "rev-parse"; "--git-common-dir" |]
                        (createObj [ "encoding", box "utf8" ])

                output.Trim()

            let gitDirectory =
                if isAbsolute commonDirectory then
                    commonDirectory
                else
                    resolvePath workspace commonDirectory

            runtimeDirectory (canonicalPath gitDirectory)
        with _ ->
            stateDirectory workspace
