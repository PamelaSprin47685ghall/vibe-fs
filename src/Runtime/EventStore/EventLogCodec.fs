module Wanxiangshu.Runtime.EventLogCodec

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Thoth.Json
open Fable.Core.JsInterop

let wanEventToLine (e: WanEvent) : string = Encode.Auto.toString (0, e)

let tryParseEventLine (line: string) : WanEvent option =
    let trimmed = if isNull line then "" else line.Trim()

    if trimmed = "" then
        None
    else
        match Decode.Auto.fromString<WanEvent> trimmed with
        | Ok ev -> Some ev
        | Error _ -> None

let buildEvent (session: string) (kind: string) (payload: Map<string, string>) (atIso: string) : WanEvent =
    { V = 1
      Session = session
      Kind = kind
      At = atIso
      Payload = payload }

[<Import("readFile", "node:fs/promises")>]
let private readFileAsync (path: string) (encoding: string) : JS.Promise<string> = jsNative

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
