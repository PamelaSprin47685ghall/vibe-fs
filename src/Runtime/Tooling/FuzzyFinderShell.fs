module Wanxiangshu.Runtime.FuzzyFinderShell

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.PromiseQueue

type FinderLike =
    abstract member fileSearch: query: string * opts: obj -> obj
    abstract member grep: query: string * opts: obj -> obj
    abstract member destroy: unit -> unit
    abstract member isDestroyed: bool with get

[<Emit("process.env[$0]")>]
let private getEnv (key: string) : string = jsNative

let getScanTimeoutEnv () : int option =
    let v = getEnv "WANXIANGSHU_SCAN_TIMEOUT"

    if isNull v || v = "" then
        None
    else
        match System.Int32.TryParse(v) with
        | true, n when n > 0 -> Some n
        | _ -> None

let getScanTimeout () : int =
    match getScanTimeoutEnv () with
    | Some n -> n
    | None -> 15000

let private createFinderRaw (basePath: string) : JS.Promise<obj> =
    promise {
        let! module' = importDynamic<obj> "@ff-labs/fff-node"
        let fileFinder = Dyn.get module' "FileFinder"
        let result = fileFinder?create ({| basePath = basePath; aiMode = true |})

        if not (Dyn.truthy (Dyn.get result "ok")) then
            return createObj [ "ok" ==> false; "error" ==> Dyn.get result "error" ]
        else
            let finder = Dyn.get result "value"

            try
                do! finder?waitForScan (getScanTimeout ())
                return createObj [ "ok" ==> true; "value" ==> finder; "scanWarn" ==> null ]
            with ex ->
                return
                    createObj
                        [ "ok" ==> true
                          "value" ==> finder
                          "scanWarn" ==> $"waitForScan failed: {ex.Message}" ]
    }

let resultFromRaw (raw: obj) : Result<FinderLike, string> =
    if Dyn.truthy (Dyn.get raw "ok") then
        Ok(Dyn.get raw "value" :?> FinderLike)
    else
        Error(
            if Dyn.isNullish (Dyn.get raw "error") then
                "createFinder failed"
            else
                Dyn.str raw "error"
        )

let createFinder (basePath: string) : JS.Promise<Result<FinderLike, string>> =
    promise {
        let! raw = createFinderRaw basePath
        return resultFromRaw raw
    }

type FinderCache(?createFinderFn: string -> JS.Promise<Result<FinderLike, string>>) =
    let createFinderImpl = defaultArg createFinderFn createFinder
    let queue = SerialQueue()
    let mutable instances = Map.empty<string, FinderLike>
    let mutable pending = Map.empty<string, JS.Promise<Result<FinderLike, string>>>

    member _.Get(cwd: string) : JS.Promise<Result<FinderLike, string>> =
        let timeout = max 30000 (getScanTimeout () + 10000)
        queue.Enqueue(
            (fun () ->
                match Map.tryFind cwd instances with
                | Some finder when not finder.isDestroyed -> Promise.lift (Ok finder)
                | _ ->
                    match Map.tryFind cwd pending with
                    | Some finderPromise -> finderPromise
                    | None ->
                        let finderPromise = createFinderImpl cwd
                        pending <- Map.add cwd finderPromise pending

                        finderPromise
                        |> Promise.bind (fun result ->
                            match result with
                            | Ok finder -> instances <- Map.add cwd finder instances
                            | Error _ -> ()

                            pending <- Map.remove cwd pending
                            Promise.lift result)),
            timeoutMs = timeout
        )

    member _.Destroy(cwd: string) : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            match Map.tryFind cwd instances with
            | Some finder when not finder.isDestroyed -> finder.destroy ()
            | _ -> ()

            instances <- Map.remove cwd instances
            pending <- Map.remove cwd pending
            Promise.lift ())

    member _.DestroyAll() : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            let allInstances = instances |> Map.toList

            allInstances
            |> List.iter (fun (cwd, finder) ->
                if not finder.isDestroyed then
                    finder.destroy ())

            instances <- Map.empty
            pending <- Map.empty
            Promise.lift ())
