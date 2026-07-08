module Wanxiangshu.Shell.EventLogCodec

open Fable.Core
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Shell.FileSys
open FileSys
open Thoth.Json

let eventLogFileName = ".wanxiangshu.ndjson"

let eventPath (workspaceRoot: string) : string = resolve workspaceRoot eventLogFileName

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
