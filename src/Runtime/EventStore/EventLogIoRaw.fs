module Wanxiangshu.Runtime.EventLogIoRaw

open Fable.Core
open Fable.Core.JsInterop

[<Import("appendFileSync", "node:fs")>]
let appendFileSync (path: string) (data: string) : unit = jsNative

[<Import("appendFile", "node:fs/promises")>]
let appendFileAsync (path: string) (data: string) : JS.Promise<unit> = jsNative

[<Import("writeFile", "node:fs/promises")>]
let writeFileFlagAsync (path: string) (data: string) (options: obj) : JS.Promise<unit> = jsNative

[<Import("unlink", "node:fs/promises")>]
let unlinkAsync (path: string) : JS.Promise<unit> = jsNative

[<Import("stat", "node:fs/promises")>]
let statAsync (path: string) : JS.Promise<obj> = jsNative

[<Import("lock", "proper-lockfile")>]
let private lockfileLock (path: string) (options: obj) : JS.Promise<unit -> JS.Promise<unit>> = jsNative

let private lockfileOptions () =
    createObj
        [ "stale", box 15000
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

let fileExists (filePath: string) : JS.Promise<bool> =
    promise {
        try
            let! _ = statAsync filePath
            return true
        with _ ->
            return false
    }

let private ensureFileExists (filePath: string) : JS.Promise<unit> =
    promise {
        let! exists = fileExists filePath

        if not exists then
            try
                do! writeFileFlagAsync filePath "" (createObj [ "flag", box "wx" ])
            with _ ->
                ()
    }

let private cleanupStaleLockFile (lockPath: string) : JS.Promise<unit> =
    promise {
        // Do not unconditionially unlink the lock file, as it can delete an active lock from another process.
        // proper-lockfile handles its own stale locks safely.
        ()
    }

let private acquireFileLock (filePath: string) : JS.Promise<unit -> JS.Promise<unit>> =
    promise {
        let mutable releaseVal = None
        let mutable lockError = None

        try
            let! rel = lockfileLock filePath (lockfileOptions ())
            releaseVal <- Some rel
        with ex ->
            lockError <- Some ex

        match lockError with
        | Some ex -> return raise ex
        | _ -> ()

        let release = releaseVal.Value
        return release
    }

let withWorkspaceLock<'T> (filePath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    let prev =
        match fileQueues.TryGetValue(filePath) with
        | true, p -> p
        | _ -> Promise.lift (box null)

    let mutable selfPromise = None

    let next =
        promise {
            try
                let! _ = prev
                ()
            with _ ->
                ()

            let lockPath = filePath + ".lock"

            do! ensureFileExists filePath
            do! cleanupStaleLockFile lockPath

            let! release = acquireFileLock filePath

            let mutable caught = None
            let mutable resOpt = None

            try
                let! res = action ()
                resOpt <- Some res
            with ex ->
                caught <- Some ex

            try
                do! release ()
            with _ ->
                ()

            match fileQueues.TryGetValue(filePath) with
            | true, current when Some current = selfPromise -> fileQueues.Remove(filePath) |> ignore
            | _ -> ()

            match caught with
            | Some ex -> return raise ex
            | None -> return box resOpt.Value
        }

    selfPromise <- Some next
    fileQueues.[filePath] <- next

    promise {
        let! res = next
        return unbox<'T> res
    }
