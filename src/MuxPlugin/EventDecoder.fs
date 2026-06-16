module VibeFs.MuxPlugin.EventDecoder

open VibeFs.Kernel

type DecodedHookEvent =
    { eventType: string
      workspaceId: string
      properties: obj
      metadata: obj
      stopReason: string
      errorType: string }

let decodeHookEvent (event: obj) : DecodedHookEvent =
    let props = Dyn.get event "properties"
    let meta = if Dyn.isNullish props then null else Dyn.get props "metadata"
    { eventType = if Dyn.isNullish event then "" else Dyn.str event "type"
      workspaceId = Dyn.str event "workspaceId"
      properties = if Dyn.isNullish props then null else props
      metadata = meta
      stopReason = if Dyn.isNullish meta then "" else Dyn.str meta "muxStopReason"
      errorType = if Dyn.isNullish props then "" else Dyn.str props "errorType" }

let getLastAssistantText (properties: obj) : string =
    if Dyn.isNullish properties then ""
    else
        let parts = Dyn.get properties "parts"
        if Dyn.isNullish parts || not (Dyn.isArray parts) then ""
        else
            (parts :?> obj array)
            |> Array.filter (fun p -> Dyn.str p "type" = "text")
            |> Array.map (fun p -> Dyn.str p "text")
            |> String.concat "\n"
