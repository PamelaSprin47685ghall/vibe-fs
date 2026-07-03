module Wanxiangshu.Shell.EventLogCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FileSys
open FileSys

let eventLogFileName = ".wanxiangshu.ndjson"

let eventPath (workspaceRoot: string) : string =
    resolve workspaceRoot eventLogFileName

let private payloadToObj (payload: Map<string, string>) : obj =
    payload |> Map.toList |> List.map (fun (k, v) -> k, box v) |> List.toArray |> createObj

let private payloadFromObj (o: obj) : Map<string, string> =
    if Dyn.isNullish o then Map.empty
    else
        unbox<string array> (JS.Constructors.Object.keys o)
        |> Array.fold
            (fun m k ->
                let v = Dyn.str o k
                if v = "" then m else Map.add k v m)
            Map.empty

let wanEventToLine (e: WanEvent) : string =
    JS.JSON.stringify(
        createObj
            [| "v", box e.V
               "session", box e.Session
               "kind", box e.Kind
               "at", box e.At
               "payload", payloadToObj e.Payload |])

let tryParseEventLine (line: string) : WanEvent option =
    let trimmed = if isNull line then "" else line.Trim()
    if trimmed = "" then None
    else
        try
            let o = JS.JSON.parse trimmed
            let v =
                try Dyn.get o "v" |> unbox<int>
                with _ -> 1
            let session = Dyn.str o "session"
            let kind = Dyn.str o "kind"
            let at = Dyn.str o "at"
            let payload = payloadFromObj (Dyn.get o "payload")
            if session = "" || kind = "" then None
            else Some { V = v; Session = session; Kind = kind; At = at; Payload = payload }
        with _ ->
            None

let buildEvent (session: string) (kind: string) (payload: Map<string, string>) (atIso: string) : WanEvent =
    { V = 1; Session = session; Kind = kind; At = atIso; Payload = payload }