module VibeFs.Shell.FuzzyFinderShell

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

type FinderLike =
    abstract member fileSearch: query: string * opts: obj -> obj
    abstract member grep: query: string * opts: obj -> obj
    abstract member destroy: unit -> unit
    abstract member isDestroyed: bool with get

let private asPromise<'T> (value: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> value

let private createFinderRaw (basePath: string) : JS.Promise<obj> =
    async {
        let! module' = importDynamic<obj> "@ff-labs/fff-node" |> Async.AwaitPromise
        let fileFinder = Dyn.get module' "FileFinder"
        let result = fileFinder?create({| basePath = basePath; aiMode = true |})

        if not (Dyn.truthy (Dyn.get result "ok")) then
            return createObj [ "ok" ==> false; "error" ==> Dyn.get result "error" ]
        else
            let finder = Dyn.get result "value"
            try
                do! finder?waitForScan(15000) |> asPromise<unit> |> Async.AwaitPromise
            with _ -> ()
            return createObj [ "ok" ==> true; "value" ==> finder ]
    }
    |> Async.StartAsPromise

let resultFromRaw (raw: obj) : Result<FinderLike, string> =
    if Dyn.truthy (Dyn.get raw "ok") then
        Ok (Dyn.get raw "value" :?> FinderLike)
    else
        Error (if Dyn.isNullish (Dyn.get raw "error") then "createFinder failed" else Dyn.str raw "error")

let createFinder (basePath: string) : JS.Promise<Result<FinderLike, string>> =
    async {
        let! raw = createFinderRaw basePath |> Async.AwaitPromise
        return resultFromRaw raw
    }
    |> Async.StartAsPromise

type FinderCache() =
    let instances = Dictionary<string, FinderLike>()
    let pending = Dictionary<string, JS.Promise<Result<FinderLike, string>>>()

    member _.Get(cwd: string) : JS.Promise<Result<FinderLike, string>> =
        async {
            match instances.TryGetValue cwd with
            | true, finder when not finder.isDestroyed -> return Ok finder
            | _ ->
                match pending.TryGetValue cwd with
                | true, promise -> return! promise |> Async.AwaitPromise
                | _ ->
                    let promise = createFinder cwd
                    pending.[cwd] <- promise

                    try
                        let! result = promise |> Async.AwaitPromise
                        match result with
                        | Ok finder -> instances.[cwd] <- finder
                        | Error _ -> ()
                        pending.Remove(cwd) |> ignore
                        return result
                    with error ->
                        pending.Remove(cwd) |> ignore
                        return raise error
        }
        |> Async.StartAsPromise

    member _.Destroy(cwd: string) : unit =
        match instances.TryGetValue cwd with
        | true, finder when not finder.isDestroyed -> finder.destroy()
        | _ -> ()
        instances.Remove(cwd) |> ignore
        pending.Remove(cwd) |> ignore

    member this.DestroyAll() : unit =
        instances.Keys |> Seq.toArray |> Array.iter this.Destroy
