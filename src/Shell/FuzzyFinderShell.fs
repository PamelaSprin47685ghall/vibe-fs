module VibeFs.Shell.FuzzyFinderShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell.PromiseQueue

type FinderLike =
    abstract member fileSearch: query: string * opts: obj -> obj
    abstract member grep: query: string * opts: obj -> obj
    abstract member destroy: unit -> unit
    abstract member isDestroyed: bool with get

let private createFinderRaw (basePath: string) : JS.Promise<obj> =
    promise {
        let! module' = importDynamic<obj> "@ff-labs/fff-node"
        let fileFinder = Dyn.get module' "FileFinder"
        let result = fileFinder?create({| basePath = basePath; aiMode = true |})

        if not (Dyn.truthy (Dyn.get result "ok")) then
            return createObj [ "ok" ==> false; "error" ==> Dyn.get result "error" ]
        else
            let finder = Dyn.get result "value"
            try
                do! finder?waitForScan(15000)
                return createObj [ "ok" ==> true; "value" ==> finder; "scanWarn" ==> null ]
            with ex ->
                return createObj [ "ok" ==> true; "value" ==> finder; "scanWarn" ==> $"waitForScan failed: {ex.Message}" ]
    }

let resultFromRaw (raw: obj) : Result<FinderLike, string> =
    if Dyn.truthy (Dyn.get raw "ok") then
        Ok (Dyn.get raw "value" :?> FinderLike)
    else
        Error (if Dyn.isNullish (Dyn.get raw "error") then "createFinder failed" else Dyn.str raw "error")

let createFinder (basePath: string) : JS.Promise<Result<FinderLike, string>> =
    promise {
        let! raw = createFinderRaw basePath
        return resultFromRaw raw
    }

type FinderCache() =
    let queue = SerialQueue()
    let mutable instances = Map.empty<string, FinderLike>
    let mutable pending = Map.empty<string, JS.Promise<Result<FinderLike, string>>>

    member _.Get(cwd: string) : JS.Promise<Result<FinderLike, string>> =
        queue.Enqueue(fun () ->
            match Map.tryFind cwd instances with
            | Some finder when not finder.isDestroyed -> Promise.lift (Ok finder)
            | _ ->
                match Map.tryFind cwd pending with
                | Some finderPromise -> finderPromise
                | None ->
                    let finderPromise = createFinder cwd
                    pending <- Map.add cwd finderPromise pending
                    finderPromise
                    |> Promise.bind (fun result ->
                        match result with
                        | Ok finder -> instances <- Map.add cwd finder instances
                        | Error _ -> ()
                        pending <- Map.remove cwd pending
                        Promise.lift result))

    member _.Destroy(cwd: string) : unit =
        match Map.tryFind cwd instances with
        | Some finder when not finder.isDestroyed -> finder.destroy()
        | _ -> ()
        instances <- Map.remove cwd instances
        pending <- Map.remove cwd pending

    member this.DestroyAll() : unit =
        instances |> Map.toList |> List.map fst |> List.iter this.Destroy
