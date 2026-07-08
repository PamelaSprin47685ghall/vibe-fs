module Wanxiangshu.Shell.EventLogIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Shell.EventLogCodec

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

let withWorkspaceLock (filePath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    promise {
        try
            let! _ = statAsync filePath
            ()
        with _ ->
            try
                do! writeFileFlagAsync filePath "" (createObj [ "flag", box "w" ])
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

        let! release = lockfileLock filePath (lockfileOptions ())
        let mutable caught = None

        let! resOpt =
            promise {
                try
                    let! result = action ()
                    return Some result
                with ex ->
                    caught <- Some ex
                    return None
            }

        try
            do! release ()
        with _ ->
            ()

        match caught with
        | Some ex -> return raise ex
        | None -> return resOpt.Value
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
