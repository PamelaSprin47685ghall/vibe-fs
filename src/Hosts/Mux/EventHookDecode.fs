module Wanxiangshu.Hosts.Mux.EventHookDecode

open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.NudgeRuntimeEvent

type DecodedHookEvent =
    { eventType: string
      workspaceId: string
      properties: obj
      stopReason: string
      errorType: string }

let decodeHookEvent (event: obj) : DecodedHookEvent =
    let props = Dyn.get event "properties"

    let meta =
        if Dyn.isNullish props then null else Dyn.get props "metadata"

    { eventType = if Dyn.isNullish event then "" else Dyn.str event "type"
      workspaceId = Dyn.str event "workspaceId"
      properties = if Dyn.isNullish props then null else props
      stopReason = if Dyn.isNullish meta then "" else Dyn.str meta "muxStopReason"
      errorType = if Dyn.isNullish props then "" else Dyn.str props "errorType" }

let private getLastAssistantText (properties: obj) : string =
    if Dyn.isNullish properties then
        ""
    else
        let parts = Dyn.get properties "parts"

        if Dyn.isNullish parts || not (Dyn.isArray parts) then
            ""
        else
            (parts :?> obj array)
            |> Array.filter (fun p -> Dyn.str p "type" = "text")
            |> Array.map (fun p -> Dyn.str p "text")
            |> String.concat "\n"

let parseHookEvent (event: obj) : NudgeRuntimeEvent =
    let decoded = decodeHookEvent event

    if decoded.workspaceId = "" then
        Ignore
    else
        match decoded.eventType with
        | "stream-end" -> StreamEnd(decoded.workspaceId, decoded.stopReason, getLastAssistantText decoded.properties)
        | "stream-abort" -> StreamAbort decoded.workspaceId
        | "error" when decoded.errorType = "aborted" -> AbortedError decoded.workspaceId
        | _ -> Ignore

let shouldObserveMuxEvent (eventType: string) : bool =
    match eventType with
    | "stream-end"
    | "stream-abort"
    | "error"
    | "session.error"
    | "session.deleted"
    | "session.close"
    | "session.delete"
    | "session.remove" -> true
    | _ -> false
