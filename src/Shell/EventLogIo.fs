module Wanxiangshu.Shell.EventLogIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Shell.EventLogCodec

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

let withWorkspaceLock<'T> (filePath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    let prev =
        match fileQueues.TryGetValue(filePath) with
        | true, p -> p
        | _ -> Promise.lift (box null)

    let next =
        promise {
            try
                let! _ = prev
                ()
            with _ ->
                ()

            let! res = action ()
            return box res
        }

    fileQueues.[filePath] <- next

    promise {
        let! res = next
        return unbox<'T> res
    }

let fileExists (filePath: string) : JS.Promise<bool> =
    promise {
        try
            let! _ = statAsync filePath
            return true
        with _ ->
            return false
    }

let appendLine (path: string) (e: WanEvent) : JS.Promise<unit> =
    promise {
        let line = wanEventToLine e + "\n"
        do! appendFileAsync path line
    }
