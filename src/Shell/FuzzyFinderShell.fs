module VibeFs.Shell.FuzzyFinderShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

/// The fff-node FileFinder surface the coordinator relies on.
type FinderLike =
    abstract member fileSearch: query: string * opts: obj -> obj
    abstract member grep: query: string * opts: obj -> obj
    abstract member destroy: unit -> unit
    abstract member isDestroyed: bool with get

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

/// Create the fff-node FileFinder and wait for the initial scan.
let private createFinderRaw (basePath: string) : JS.Promise<obj> =
    async {
        let! module' = importDynamic<obj> "@ff-labs/fff-node" |> Async.AwaitPromise
        let fileFinder = Dyn.get module' "FileFinder"
        let r = fileFinder?create({| basePath = basePath; aiMode = true |})
        if not (Dyn.truthy (Dyn.get r "ok")) then
            return createObj [ "ok" ==> false; "error" ==> Dyn.get r "error" ]
        else
            let f = Dyn.get r "value"
            try
                do! f?waitForScan(15000) |> asPromise<unit> |> Async.AwaitPromise
            with _ -> ()
            return createObj [ "ok" ==> true; "value" ==> f ]
    }
    |> Async.StartAsPromise

/// Convert a raw JS `{ok, value?, error?}` object into a typed F# Result.
/// Extracted so the shape-mapping is directly testable without fff-node.
let resultFromRaw (raw: obj) : Result<FinderLike, string> =
    if Dyn.truthy (Dyn.get raw "ok")
    then Ok(Dyn.get raw "value" :?> FinderLike)
    else Error(if Dyn.isNullish (Dyn.get raw "error") then "createFinder failed" else Dyn.str raw "error")

/// Create a fresh finder, converting the raw JS `{ok,value,error}` into a typed
/// F# Result so downstream `match Result with` works at runtime.
let createFinder (basePath: string) : JS.Promise<Result<FinderLike, string>> =
    async {
        let! raw = createFinderRaw basePath |> Async.AwaitPromise
        return resultFromRaw raw
    }
    |> Async.StartAsPromise

/// A per-cwd finder cache with in-flight de-duplication.
let private instances = System.Collections.Generic.Dictionary<string, FinderLike>()
let private pending = System.Collections.Generic.Dictionary<string, JS.Promise<Result<FinderLike, string>>>()

/// Return a cached finder for cwd, or create one (de-duplicating concurrent calls).
let getCachedFinder (cwd: string) : JS.Promise<Result<FinderLike, string>> =
    async {
        match instances.TryGetValue cwd with
        | true, f when not f.isDestroyed -> return Ok f
        | _ ->
            match pending.TryGetValue cwd with
            | true, p -> return! p |> Async.AwaitPromise
            | _ ->
                let promise = createFinder cwd
                pending.[cwd] <- promise
                try
                    let! result = promise |> Async.AwaitPromise
                    match result with
                    | Ok f -> instances.[cwd] <- f
                    | Error _ -> ()
                    pending.Remove(cwd) |> ignore
                    return result
                with err ->
                    pending.Remove(cwd) |> ignore
                    return raise err
    }
    |> Async.StartAsPromise

/// Destroy and forget the finder for a given cwd.
let destroyFinder (cwd: string) : unit =
    match instances.TryGetValue cwd with
    | true, f when not f.isDestroyed -> f.destroy()
    | _ -> ()
    instances.Remove(cwd) |> ignore
    pending.Remove(cwd) |> ignore

/// Destroy every cached finder.
let destroyAllFinders () : unit =
    let cwds = instances.Keys |> Seq.toArray
    for cwd in cwds do destroyFinder cwd
