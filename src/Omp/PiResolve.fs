module Wanxiangshu.Omp.PiResolve

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.RuntimeScope

[<Import("homedir", "node:os")>]
let private homedir () : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (p: string) : bool = jsNative

[<Import("pathToFileURL", "node:url")>]
let private pathToFileURL (p: string) : obj = jsNative

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private piBaseCandidates () : string array =
    let home = homedir ()
    let env = nodeProcess?env?("PI_BASE")
    let fromEnv = if isNull env then None else Some (string env)
    [|
        yield! (fromEnv |> Option.toArray)
        pathJoin home ".cache/.bun/install/global/node_modules/@oh-my-pi"
        pathJoin home ".bun/install/global/node_modules/@oh-my-pi"
    |]

let mutable private resolvedBase : string option = None

let getPiBase () : string =
    match resolvedBase with
    | Some b -> b
    | None ->
        let found =
            piBaseCandidates ()
            |> Array.tryFind existsSync
        match found with
        | None ->
            let tried = piBaseCandidates () |> Array.map (fun p -> "  - " + p) |> String.concat "\n"
            failwith $"Cannot locate @oh-my-pi base path. Tried:\n{tried}\nSet PI_BASE environment variable to the @oh-my-pi install root."
        | Some b ->
            resolvedBase <- Some b
            b

let mutable private cachedModule : obj option = None

let getCodingAgentModule (scope: RuntimeScope) : JS.Promise<obj> =
    promise {
        match scope.TryFindKey "omp.coding_agent_module" with
        | Some m -> return m
        | None ->
            match cachedModule with
            | Some m -> return m
            | None ->
                let basePath = getPiBase ()
                let fileUrl = pathToFileURL (pathJoin basePath "pi-coding-agent/src/index.ts")
                let href = fileUrl?href
                let! module' = importDynamic<obj> (string href)
                cachedModule <- Some module'
                return module'
    }

