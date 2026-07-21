module Wanxiangshu.Runtime.EventLogLock

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.EventLogIoRaw

[<Import("lock", "proper-lockfile")>]
let private lockfileLock (path: string) (options: obj) : JS.Promise<unit -> JS.Promise<unit>> = jsNative

let private lockfileOptions () =
    createObj
        [ "stale", box 15000
          "update", box 5000
          "retries",
          box (
              createObj
                  [ "retries", box 100
                    "factor", box 1
                    "minTimeout", box 50
                    "maxTimeout", box 100 ]
          ) ]

let private fileQueues =
    System.Collections.Generic.Dictionary<string, JS.Promise<obj>>()

[<Import("mkdir", "node:fs/promises")>]
let private mkdirAsync (path: string, options: obj) : JS.Promise<unit> = jsNative

let unpoisonFile (filePath: string) : unit =
    poisonedFiles.Remove(filePath) |> ignore

let private ensureFileExists (filePath: string) : JS.Promise<unit> =
    promise {
        let! exists = fileExists filePath

        if not exists then
            try
                let lastSlash = max (filePath.LastIndexOf('/')) (filePath.LastIndexOf('\\'))
                if lastSlash > 0 then
                    let dir = filePath.Substring(0, lastSlash)
                    do! mkdirAsync (dir, {| recursive = true |})
                do! writeFileFlagAsync filePath "" (createObj [ "flag", box "wx" ])
            with ex when isExistingPathError (box ex) ->
                ()
    }

let private acquireFileLock (filePath: string) : JS.Promise<unit -> JS.Promise<unit>> =
    promise {
        let mutable releaseVal = None
        let mutable lockError = None

        try
            let! rel =
                match lockfileLockOverride with
                | Some f -> f.Invoke(filePath, lockfileOptions ())
                | None -> lockfileLock filePath (lockfileOptions ())

            releaseVal <- Some rel
        with ex ->
            lockError <- Some ex

        match lockError with
        | Some ex -> return raise ex
        | _ -> ()

        let release = releaseVal.Value
        return release
    }

let private runAction (action: unit -> JS.Promise<'T>) : JS.Promise<Result<'T, exn>> =
    promise {
        try
            let! actionResult = PromiseQueue.withTimeout actionTimeoutMs (action ())

            match actionResult with
            | None -> return Error(exn "EventStoreTimeout: Action timed out")
            | Some res -> return Ok res
        with ex ->
            return Error ex
    }

let private runRelease (release: unit -> JS.Promise<unit>) : JS.Promise<Result<unit, exn>> =
    promise {
        try
            do! release ()
            return Ok()
        with ex ->
            return Error ex
    }

let private acquireLockOrFail (filePath: string) : JS.Promise<unit -> JS.Promise<unit>> =
    promise {
        let! lockResult = PromiseQueue.withTimeout lockAcquireTimeoutMs (acquireFileLock filePath)

        match lockResult with
        | None -> return raise (exn "WorkspaceLockTimeout: Failed to acquire lock within timeout")
        | Some release -> return release
    }

let private runLockedAction (filePath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<Result<'T, exn>> =
    promise {
        let! actionRes = runAction action

        match actionRes with
        | Error ex when ex.Message = "EventStoreTimeout: Action timed out" -> poisonedFiles.Add(filePath) |> ignore
        | _ -> ()

        return actionRes
    }

let private releaseLockOrPoison
    (filePath: string)
    (release: unit -> JS.Promise<unit>)
    (actionErr: exn option)
    : JS.Promise<exn option> =
    promise {
        let! releaseRes = PromiseQueue.withTimeout lockReleaseTimeoutMs (runRelease release)

        match releaseRes with
        | None ->
            poisonedFiles.Add(filePath) |> ignore

            return
                actionErr
                |> Option.orElse (Some(exn "LockReleaseIndeterminate: Lock release timed out"))
        | Some(Error ex) ->
            poisonedFiles.Add(filePath) |> ignore

            return
                actionErr
                |> Option.orElse (Some(exn ("LockReleaseFailed: Lock release failed: " + ex.Message)))
        | Some(Ok _) -> return actionErr
    }

let private executeWorkspaceLockAction (filePath: string) (action: unit -> JS.Promise<'T>) =
    promise {
        let! release = acquireLockOrFail filePath
        let! actionRes = runLockedAction filePath action

        let resOpt, actionErr =
            match actionRes with
            | Ok res -> Some res, None
            | Error ex -> None, Some ex

        let! finalErr = releaseLockOrPoison filePath release actionErr

        match finalErr with
        | Some ex -> return raise ex
        | None -> return box resOpt.Value
    }

let private waitPrev (prev: JS.Promise<obj>) (filePath: string) : JS.Promise<unit> =
    promise {
        let! prevRes =
            PromiseQueue.withTimeout queueWaitTimeoutMs prev
            |> Promise.catch (fun _ -> Some(box null))

        match prevRes with
        | None ->
            poisonedFiles.Add(filePath) |> ignore
            return raise (exn ("WorkspaceQueueTimeout: previous operation did not complete within timeout"))
        | Some _ -> ()
    }

let private ensureFileOrFail (filePath: string) : JS.Promise<unit> =
    promise {
        let! ensureRes = PromiseQueue.withTimeout ensureFileTimeoutMs (ensureFileExists filePath)

        match ensureRes with
        | None ->
            poisonedFiles.Add(filePath) |> ignore
            return raise (exn ("WorkspaceEnsureFileTimeout: timed out ensuring NDJSON file exists"))
        | Some() -> ()
    }

let withWorkspaceLock<'T> (filePath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    if poisonedFiles.Contains(filePath) then
        Promise.reject (
            exn (
                "EventStorePoisoned: Workspace is poisoned due to previous locking/action timeout: "
                + filePath
            )
        )
    else
        let prev =
            match fileQueues.TryGetValue(filePath) with
            | true, p -> p
            | _ -> Promise.lift (box null)

        let mutable selfPromise = None

        let removeSelfFromQueue () =
            match fileQueues.TryGetValue(filePath) with
            | true, current when Some current = selfPromise -> fileQueues.Remove(filePath) |> ignore
            | _ -> ()

        let next =
            promise {
                try
                    do! waitPrev prev filePath

                    if poisonedFiles.Contains(filePath) then
                        return
                            raise (
                                exn (
                                    "EventStorePoisoned: Workspace is poisoned due to previous locking/action timeout: "
                                    + filePath
                                )
                            )

                    do! ensureFileOrFail filePath
                    return! executeWorkspaceLockAction filePath action
                finally
                    removeSelfFromQueue ()
            }

        selfPromise <- Some next
        fileQueues.[filePath] <- next

        promise {
            let! res = next
            return unbox<'T> res
        }
