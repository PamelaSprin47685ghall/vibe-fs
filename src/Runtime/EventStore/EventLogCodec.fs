module Wanxiangshu.Runtime.EventLogCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Runtime.EventLogIoRaw
open Thoth.Json
open System

[<Import("createHash", "crypto")>]
let private createHash (algorithm: string) : obj = jsNative

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let computeEventChecksum (session: string) (kind: string) (at: string) (payload: Map<string, string>) (eventId: string) (writerId: string) (sequence: int) : string =
    let payloadStr =
        payload
        |> Map.toList
        |> List.sortBy fst
        |> List.map (fun (k, v) -> k + "=" + v)
        |> String.concat ";"
    let dataStr = sprintf "%s|%s|%s|%s|%s|%s|%d" session kind at payloadStr eventId writerId sequence
    let hashObj = createHash "sha256"
    let buf = nodeBuffer?from(dataStr, "utf-8")
    unbox<string> (hashObj?update(buf)?digest("hex"))

let verifyEventChecksum (e: WanEvent) : bool =
    match e.EventId, e.WriterId, e.Sequence, e.Checksum with
    | Some eid, Some wid, Some seq, Some chk ->
        let expected = computeEventChecksum e.Session e.Kind e.At e.Payload eid wid seq
        expected = chk
    | _ -> true

let wanEventToLine (e: WanEvent) : string = Encode.Auto.toString (0, e)

let tryParseEventLine (line: string) : WanEvent option =
    let trimmed = if isNull line then "" else line.Trim()
    if trimmed = "" then
        None
    else
        match Decode.Auto.fromString<WanEvent> trimmed with
        | Ok ev when isNull ev.Kind || ev.Kind.Trim() = "" -> None
        | Ok ev -> Some ev
        | Error _ -> None

let buildEvent (session: string) (kind: string) (payload: Map<string, string>) (atIso: string) : WanEvent =
    { V = 1
      Session = session
      Kind = kind
      At = atIso
      Payload = payload
      EventId = None
      WriterId = None
      Sequence = None
      Checksum = None }

type ScanResult =
    | Clean of validEndOffset: int * events: WanEvent list
    | ValidFinalLineMissingNewline of validEndOffset: int * events: WanEvent list
    | CorruptTail of validEndOffset: int * badOffset: int * badLineNumber: int * reason: string * removedBytes: int * events: WanEvent list
    | CorruptMiddle of validEndOffset: int * badOffset: int * badLineNumber: int * reason: string * removedBytes: int * events: WanEvent list

let private splitBufferLines (buffer: obj) (len: int) : ResizeArray<int * int * bool> =
    let uint8Array = unbox<Fable.Core.JS.Uint8Array> buffer
    let lines = ResizeArray<int * int * bool>()
    let mutable start = 0
    for i = 0 to len - 1 do
        let b = uint8Array.[i]
        if b = 10uy then
            lines.Add((start, i, true))
            start <- i + 1
    if start < len then
        lines.Add((start, len, false))
    lines

let private parseSingleLine (buffer: obj) (startOff: int) (endOff: int) (hasNewline: bool) (len: int) (lastValidOffset: int ref) (idx: int) =
    let lineBuf = buffer?subarray(startOff, endOff)
    let lineStr = unbox<string> (lineBuf?toString("utf-8"))
    let trimmed = if isNull lineStr then "" else lineStr.Trim()
    if trimmed = "" then
        lastValidOffset.Value <- if hasNewline then endOff + 1 else endOff
        None
    else
        match tryParseEventLine lineStr with
        | Some ev ->
            if verifyEventChecksum ev then
                lastValidOffset.Value <- if hasNewline then endOff + 1 else endOff
                Some (Choice1Of2 ev)
            else
                let removedBytes = len - lastValidOffset.Value
                let badLineNum = idx + 1
                Some (Choice2Of2 (lastValidOffset.Value, startOff, badLineNum, "Checksum verification failed", removedBytes))
        | None ->
            let removedBytes = len - lastValidOffset.Value
            let badLineNum = idx + 1
            Some (Choice2Of2 (lastValidOffset.Value, startOff, badLineNum, "JSON parse failure", removedBytes))

let scanEventLog (buffer: obj) : ScanResult =
    let len = unbox<int> buffer?length
    if len = 0 then
        Clean(0, [])
    else
        let lines = splitBufferLines buffer len
        let events = ResizeArray<WanEvent>()
        let lastValidOffset = ref 0
        let mutable corruptInfo = None
        for idx = 0 to lines.Count - 1 do
            if corruptInfo.IsNone then
                let startOff, endOff, hasNewline = lines.[idx]
                match parseSingleLine buffer startOff endOff hasNewline len lastValidOffset idx with
                | Some (Choice1Of2 ev) -> events.Add(ev)
                | Some (Choice2Of2 err) -> corruptInfo <- Some err
                | None -> ()
        match corruptInfo with
        | Some(validEnd, badOff, badLine, reason, removed) ->
            if badLine = lines.Count then
                CorruptTail(validEnd, badOff, badLine, reason, removed, events |> Seq.toList)
            else
                CorruptMiddle(validEnd, badOff, badLine, reason, removed, events |> Seq.toList)
        | None ->
            if lines.Count > 0 then
                let _, _, lastHasNewline = lines.[lines.Count - 1]
                if not lastHasNewline then
                    ValidFinalLineMissingNewline(lastValidOffset.Value, events |> Seq.toList)
                else
                    Clean(lastValidOffset.Value, events |> Seq.toList)
            else
                Clean(lastValidOffset.Value, events |> Seq.toList)

[<Import("open", "node:fs/promises")>]
let private openFileHandleAsync (path: string) (flags: string) : JS.Promise<obj> = jsNative

[<Import("stat", "node:fs/promises")>]
let private statAsync (path: string) : JS.Promise<obj> = jsNative

let readEventLogChunk (path: string) (position: float) (length: int) : JS.Promise<string> =
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

let decodeEventsFromFile (path: string) : JS.Promise<WanEvent list> =
    promise {
        let! stats = statAsync path
        let size = unbox<int> stats?size
        if size = 0 then
            return []
        else
            let! handle = openFileHandleAsync path "r"
            try
                let buffer = nodeBuffer?alloc(size)
                let! _ = handle?read(buffer, 0, size, 0)
                match scanEventLog buffer with
                | Clean(_, evs) -> return evs
                | ValidFinalLineMissingNewline(_, evs) -> return evs
                | CorruptTail(_, _, _, _, _, evs) -> return evs
                | CorruptMiddle(_, _, _, _, _, evs) -> return evs
            finally
                handle?close() |> ignore
    }

let inscribeEventLogLine (path: string) (e: WanEvent) : JS.Promise<unit> =
    promise {
        let line = wanEventToLine e + "\n"
        do! inscribeRawEventLog path line
    }

let decorateEvent (writerId: string) (eventCountRead: int) (e: WanEvent) : WanEvent =
    let eid = match e.EventId with Some id -> id | None -> Guid.NewGuid().ToString()
    let seq = eventCountRead + 1
    let chk = computeEventChecksum e.Session e.Kind e.At e.Payload eid writerId seq
    { e with EventId = Some eid; WriterId = Some writerId; Sequence = Some seq; Checksum = Some chk }

let decorateEvents (writerId: string) (eventCountRead: int) (events: WanEvent list) : WanEvent list =
    let mutable currentEvents = []
    let mutable nextSeq = eventCountRead + 1
    for e in events do
        let eid = match e.EventId with Some id -> id | None -> Guid.NewGuid().ToString()
        let chk = computeEventChecksum e.Session e.Kind e.At e.Payload eid writerId nextSeq
        let decorated = { e with EventId = Some eid; WriterId = Some writerId; Sequence = Some nextSeq; Checksum = Some chk }
        currentEvents <- decorated :: currentEvents
        nextSeq <- nextSeq + 1
    List.rev currentEvents
