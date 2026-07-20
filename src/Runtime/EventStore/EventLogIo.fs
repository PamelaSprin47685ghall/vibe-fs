module Wanxiangshu.Runtime.EventLogIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogIoRaw

let appendFileAsync (path: string) (data: string) : JS.Promise<unit> =
    Wanxiangshu.Runtime.EventLogIoRaw.appendFileAsync path data

let writeFileFlagAsync (path: string) (data: string) (options: obj) : JS.Promise<unit> =
    Wanxiangshu.Runtime.EventLogIoRaw.writeFileFlagAsync path data options

let unlinkAsync (path: string) : JS.Promise<unit> =
    Wanxiangshu.Runtime.EventLogIoRaw.unlinkAsync path

let statAsync (path: string) : JS.Promise<obj> =
    Wanxiangshu.Runtime.EventLogIoRaw.statAsync path

let fileExists (filePath: string) : JS.Promise<bool> =
    Wanxiangshu.Runtime.EventLogIoRaw.fileExists filePath

let withWorkspaceLock<'T> (filePath: string) (action: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    Wanxiangshu.Runtime.EventLogIoRaw.withWorkspaceLock<'T> filePath action

let readChunkAsync (path: string) (position: float) (length: int) : JS.Promise<string> =
    Wanxiangshu.Runtime.EventLogCodec.readChunkAsync path position length

let readEventsFromText (text: string) : WanEvent list =
    Wanxiangshu.Runtime.EventLogCodec.readEventsFromText text

let readEventsFile (path: string) : JS.Promise<WanEvent list> =
    Wanxiangshu.Runtime.EventLogCodec.readEventsFile path

let appendLine (path: string) (e: WanEvent) : JS.Promise<unit> =
    promise {
        let line = wanEventToLine e + "\n"
        do! appendFileAsync path line
    }
