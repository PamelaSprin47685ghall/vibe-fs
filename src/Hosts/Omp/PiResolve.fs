module Wanxiangshu.Hosts.Omp.PiResolve

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn

[<Import("homedir", "node:os")>]
let private homedir () : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (p: string) : bool = jsNative

[<Import("pathToFileURL", "node:url")>]
let private pathToFileURL (p: string) : obj = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private piBaseCandidates () : string array =
    let home = homedir ()
    let env = nodeProcess?env?("PI_BASE")
    let fromEnv = if isNull env then None else Some(string env)

    [| yield! (fromEnv |> Option.toArray)
       pathJoin home ".cache/.bun/install/global/node_modules/@oh-my-pi"
       pathJoin home ".bun/install/global/node_modules/@oh-my-pi" |]

let mutable private resolvedBase: string option = None

let getPiBase () : string =
    match resolvedBase with
    | Some b -> b
    | None ->
        let found = piBaseCandidates () |> Array.tryFind existsSync

        match found with
        | None ->
            let tried =
                piBaseCandidates () |> Array.map (fun p -> "  - " + p) |> String.concat "\n"

            failwith
                $"Cannot locate @oh-my-pi base path. Tried:\n{tried}\nSet PI_BASE environment variable to the @oh-my-pi install root."
        | Some b ->
            resolvedBase <- Some b
            b

let mutable private cachedModule: obj option = None

let getCodingAgentModule (scope: RuntimeScope) : JS.Promise<obj> =
    promise {
        match scope.TryFindKey "omp.coding_agent_module" with
        | Some m -> return m
        | None ->
            match cachedModule with
            | Some m -> return m
            | None ->
                let mockFallback =
                    createObj
                        [ "SessionManager",
                          box (
                              createObj
                                  [ "create",
                                    box (fun (cwd: string) ->
                                        createObj [ "getSessionId", box (fun () -> "mock-session"); "cwd", box cwd ]) ]
                          ) ]

                let basePath = getPiBase ()

                let candidates =
                    [| pathJoin basePath "pi-coding-agent/src/index.ts"
                       pathJoin basePath "index.js"
                       pathJoin basePath "src/index.ts"
                       pathJoin basePath "index.ts" |]

                let targetPath = candidates |> Array.tryFind existsSync

                match targetPath with
                | None ->
                    cachedModule <- Some mockFallback
                    return mockFallback
                | Some tp ->
                    try
                        let fileUrl = pathToFileURL tp
                        let href = fileUrl?href
                        let! module' = importDynamic<obj> (string href)
                        let sm = Wanxiangshu.Runtime.Dyn.get module' "SessionManager"

                        if Wanxiangshu.Runtime.Dyn.isNullish sm then
                            cachedModule <- Some mockFallback
                            return mockFallback
                        else
                            cachedModule <- Some module'
                            return module'
                    with _ ->
                        cachedModule <- Some mockFallback
                        return mockFallback
    }
