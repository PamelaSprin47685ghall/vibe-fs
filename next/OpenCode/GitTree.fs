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

    [<Import("statSync", "node:fs")>]
    let private statSync (path: string) : obj = jsNative

    [<Emit("$0.isFile()")>]
    let private isFile (stat: obj) : bool = jsNative

    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    [<Emit("($0.update($1), $0.digest('hex'))")>]
    let private digest (hash: obj) (content: string) : string = jsNative

    let private options = createObj [ "encoding", box "utf8" ]

    let private command directory fileName arguments =
        execFileSync fileName (Array.append [| "-C"; directory |] arguments) options

    let private currentWorkspacePayload directory =
        let diff =
            command directory "git" [| "diff"; "HEAD"; "--binary"; "--no-ext-diff"; "--" |]

        let untracked =
            command directory "git" [| "ls-files"; "--others"; "--exclude-standard" |]

        let files =
            untracked.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.sort
            |> Array.choose (fun path ->
                let fullPath = joinPath directory path

                try
                    let st = statSync fullPath

                    if isFile st then
                        let content = readFileSync fullPath "utf8"
                        Some(sprintf "\n--UNTRACKED %s--\n%s" path content)
                    else
                        None
                with _ ->
                    None)
            |> String.concat ""

        diff + files

    let create (directory: string) : GitTreePort =
        { GetTreeHash = fun () -> digest (createHash "sha256") (currentWorkspacePayload directory) }
