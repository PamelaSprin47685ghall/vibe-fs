module Wanxiangshu.Runtime.EventLogIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec

[<Import("appendFileSync", "node:fs")>]
let private appendFileSync (path: string) (data: string) : unit = jsNative

[<Import("appendFile", "node:fs/promises")>]
let appendFileAsync (path: string) (data: string) : JS.Promise<unit> = jsNative

[<Import("readFile", "node:fs/promises")>]
let readFileAsync (path: string) (encoding: string) : JS.Promise<string> = jsNative

[<Import("writeFile", "node:fs/promises")>]
let writeFileFlagAsync (path: string) (data: string) (options: obj) : JS.Promise<unit> = jsNative

[<Import("unlink", "node:fs/promises")>]
let unlinkAsync (path: string) : JS.Promise<unit> = jsNative

[<Import("stat", "node:fs/promises")>]
let statAsync (path: string) : JS.Promise<obj> = jsNative

[<Import("open", "node:fs/promises")>]
let private openFileHandleAsync (path: string) (flags: string) : JS.Promise<obj> = jsNative

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let readChunkAsync (path: string) (position: float) (length: int) : JS.Promise<string> =
    promise {
        let! handle = openFileHandleAsync path "r"

        try
            let buffer = nodeBuffer?alloc (length)
            let! readResult = handle?read (buffer, 0, length, position)
            let bytesRead = unbox<int> readResult?bytesRead
            return buffer?toString ("utf-8", 0, bytesRead)
        finally
            handle?close () |> ignore
    }

let readEventsFromText (text: string) : WanEvent list =
    if text = "" then
        []
    else
        let events = ResizeArray<WanEvent>()
        let mutable stop = false

        for line in text.Split('\n') do
            if stop then
                ()
            else
                match tryParseEventLine line with
                | Some e -> events.Add(e)
                | None when line.Trim() <> "" -> stop <- true
                | _ -> ()

        events |> Seq.toList

let readEventsFile (path: string) : JS.Promise<WanEvent list> =
    promise {
        let! text =
            promise {
                try
                    return! readFileAsync path "utf-8"
                with _ ->
                    return ""
            }

        return readEventsFromText text
    }

let lockFileName = ".wanxiangshu.ndjson.lock"

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

            let! exists = fileExists filePath

            if not exists then
                try
                    do! writeFileFlagAsync filePath "" (createObj [ "flag", box "wx" ])
                with _ ->
                    ()

            let lockPath = filePath + ".lock"

            try
                let! stats = statAsync lockPath
                let isDir = unbox<bool> (stats?isDirectory ())

                if not isDir then
                    do! unlinkAsync lockPath
            with _ ->
                ()

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

let appendLine (path: string) (e: WanEvent) : JS.Promise<unit> =
    promise {
        let line = wanEventToLine e + "\n"
        do! appendFileAsync path line
    }
