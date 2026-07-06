module Wanxiangshu.Shell.EventLogFiles

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.FileSys

[<Import("appendFile", "node:fs/promises")>]
let private appendFileAsync (path: string) (data: string) : JS.Promise<unit> = jsNative

[<Import("readFile", "node:fs/promises")>]
let private readFileAsync (path: string) (encoding: string) : JS.Promise<string> = jsNative

[<Import("writeFile", "node:fs/promises")>]
let private writeFileFlagAsync (path: string) (data: string) (options: obj) : JS.Promise<unit> = jsNative

[<Import("unlink", "node:fs/promises")>]
let private unlinkAsync (path: string) : JS.Promise<unit> = jsNative

[<Import("stat", "node:fs/promises")>]
let private statAsync (path: string) : JS.Promise<obj> = jsNative

type private EventLogCache =
    { mutable Size : float
      mutable Mtime : float
      mutable Events : WanEvent list }


let private readEventsFromText (text: string) : WanEvent list =
    if text = "" then []
    else
        let events = ResizeArray<WanEvent>()
        let mutable stop = false
        for line in text.Split('\n') do
            if stop then ()
            else
                match tryParseEventLine line with
                | Some e -> events.Add(e)
                | None when line.Trim() <> "" -> stop <- true
                | _ -> ()
        events |> Seq.toList

let private readEventsFile (path: string) : JS.Promise<WanEvent list> =
    promise {
        let! text =
            promise {
                try return! readFileAsync path "utf-8"
                with _ -> return ""
            }
        return readEventsFromText text
    }

let lockFileName = ".wanxiangshu.ndjson.lock"

[<Import("lock", "proper-lockfile")>]
let private lockfileLock (path: string) (options: obj) : JS.Promise<unit -> JS.Promise<unit>> = jsNative

let private lockfileOptions () =
    createObj [
        "stale", box 15000
        "retries", box (createObj [
            "retries", box 100
            "factor", box 1
            "minTimeout", box 50
            "maxTimeout", box 100
        ])
    ]

let private withWorkspaceLock (filePath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    promise {
        try
            let! _ = statAsync filePath
            ()
        with _ ->
            try do! writeFileFlagAsync filePath "" (createObj [ "flag", box "wx" ]) with _ -> ()

        // If the lock file exists but is NOT a directory (e.g. it is a regular file),
        // delete it to prevent proper-lockfile from throwing ENOTDIR on rmdir.
        let lockPath = filePath + ".lock"
        try
            let! stats = statAsync lockPath
            let isDir = unbox<bool> (stats?isDirectory())
            if not isDir then
                do! unlinkAsync lockPath
        with _ ->
            ()

        let! release = lockfileLock filePath (lockfileOptions ())
        let! caught = action () |> Promise.result
        do! release ()
        return
            match caught with
            | Ok v -> v
            | Error ex -> raise ex
    }

type EventLogStore(workspaceRoot: string) =
    let queue = SerialQueue()
    let root = workspaceRoot
    let eventFilePath = eventPath root
    let mutable cache : EventLogCache option = None

    member _.ReadAllEvents() : JS.Promise<WanEvent list> =
        queue.Enqueue(fun () ->
            match cache with
            | Some c -> Promise.lift c.Events
            | None ->
                withWorkspaceLock eventFilePath (fun () ->
                    promise {
                        let path = eventFilePath
                        let! events = readEventsFile path
                        cache <- Some { Size = 0.0; Mtime = 0.0; Events = events }
                        return events
                    }))

    member _.AppendEvent(e: WanEvent) : JS.Promise<Result<unit, string>> =
        queue.Enqueue(fun () ->
            withWorkspaceLock eventFilePath (fun () ->
                promise {
                    try
                        let path = eventFilePath
                        let line = wanEventToLine e + "\n"
                        do! appendFileAsync path line
                        match cache with
                        | Some c -> c.Events <- c.Events @ [e]
                        | None ->
                            let! events = readEventsFile path
                            cache <- Some { Size = 0.0; Mtime = 0.0; Events = events }
                        return Ok ()
                    with ex ->
                        return Error ex.Message
                }))

    member _.AppendEventOrFail(e: WanEvent) : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            withWorkspaceLock eventFilePath (fun () ->
                promise {
                    let path = eventFilePath
                    let line = wanEventToLine e + "\n"
                    do! appendFileAsync path line
                    match cache with
                    | Some c -> c.Events <- c.Events @ [e]
                    | None ->
                        let! events = readEventsFile path
                        cache <- Some { Size = 0.0; Mtime = 0.0; Events = events }
                }))

    member _.TryClaimNudgeDispatch
        (sessionId: string)
        (action: NudgeAction)
        (anchor: string)
        (isBlocked: NudgeDedupState -> string -> bool)
        : JS.Promise<bool> =
        queue.Enqueue(fun () ->
            withWorkspaceLock eventFilePath (fun () ->
                promise {
                    let path = eventFilePath
                    let! events =
                        match cache with
                        | Some c -> Promise.lift c.Events
                        | None ->
                            promise {
                                let! evs = readEventsFile path
                                cache <- Some { Size = 0.0; Mtime = 0.0; Events = evs }
                                return evs
                            }
                    let trimmedAnchor = anchor.Trim()
                    let snap = foldNudgeSnapshot sessionId events
                    let currentAnchor = nudgeAnchorKey snap.turnId snap.lastAssistantText
                    if currentAnchor.Trim() <> trimmedAnchor then return false
                    elif isBlocked (foldNudgeDedup sessionId events) trimmedAnchor then return false
                    else
                        let payload =
                            Map [ "action", Wanxiangshu.Kernel.Nudge.toString action; "anchor", trimmedAnchor ]
                        let ev =
                            buildEvent sessionId eventKindNudgeDispatched payload (getTimestampMs().ToString())
                        let line = wanEventToLine ev + "\n"
                        do! appendFileAsync path line
                        match cache with
                        | Some c -> c.Events <- c.Events @ [ev]
                        | None ->
                            let! evs = readEventsFile path
                            cache <- Some { Size = 0.0; Mtime = 0.0; Events = evs }
                        return true
                }))