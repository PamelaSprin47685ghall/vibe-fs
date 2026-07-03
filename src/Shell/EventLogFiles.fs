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

let lockFileName = ".wanxiangshu.ndjson.lock"

let lockFilePath (workspaceRoot: string) : string =
    resolve workspaceRoot lockFileName

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

let private tryAcquireLock (lkPath: string) : JS.Promise<bool> =
    promise {
        try
            do! writeFileFlagAsync lkPath "1" (createObj [ "flag", box "wx" ])
            return true
        with _ -> return false
    }

let private releaseLock (lkPath: string) : JS.Promise<unit> =
    promise {
        try do! unlinkAsync lkPath with _ -> ()
    }

[<Emit("new Promise(function(resolve){ queueMicrotask(resolve); })")>]
let private yieldMicrotask () : JS.Promise<unit> = jsNative

let private withWorkspaceLock (lkPath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    let rec waitAndRun (attempt: int) : JS.Promise<'T> =
        promise {
            let! acquired = tryAcquireLock lkPath
            if acquired then
                let! caught = action () |> Promise.result
                do! releaseLock lkPath
                return
                    match caught with
                    | Ok v -> v
                    | Error ex -> raise ex
            else
                if attempt >= 96 then return failwith "EventLog lock timeout"
                do! yieldMicrotask ()
                return! waitAndRun (attempt + 1)
        }
    waitAndRun 0

type EventLogStore(workspaceRoot: string) =
    let queue = SerialQueue()
    let root = workspaceRoot
    let lkPath = lockFilePath root

    member _.ReadAllEvents() : JS.Promise<WanEvent list> =
        queue.Enqueue(fun () ->
            withWorkspaceLock lkPath (fun () ->
                readEventsFile (eventPath root)))

    member _.AppendEvent(e: WanEvent) : JS.Promise<Result<unit, string>> =
        queue.Enqueue(fun () ->
            withWorkspaceLock lkPath (fun () ->
                promise {
                    try
                        let path = eventPath root
                        let line = wanEventToLine e + "\n"
                        do! appendFileAsync path line
                        return Ok ()
                    with ex ->
                        return Error ex.Message
                }))

    member _.AppendEventOrFail(e: WanEvent) : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            withWorkspaceLock lkPath (fun () ->
                promise {
                    let path = eventPath root
                    let line = wanEventToLine e + "\n"
                    do! appendFileAsync path line
                }))

    member _.TryClaimNudgeDispatch
        (sessionId: string)
        (action: NudgeAction)
        (anchor: string)
        (isBlocked: NudgeDedupState -> string -> bool)
        : JS.Promise<bool> =
        queue.Enqueue(fun () ->
            withWorkspaceLock lkPath (fun () ->
                promise {
                    let! events = readEventsFile (eventPath root)
                    let trimmedAnchor = anchor.Trim()
                    if isBlocked (foldNudgeDedup sessionId events) trimmedAnchor then return false
                    else
                        let payload =
                            Map [ "action", Wanxiangshu.Kernel.Nudge.toString action; "anchor", trimmedAnchor ]
                        let ev =
                            buildEvent sessionId eventKindNudgeDispatched payload (getTimestampMs().ToString())
                        let line = wanEventToLine ev + "\n"
                        do! appendFileAsync (eventPath root) line
                        return true
                }))