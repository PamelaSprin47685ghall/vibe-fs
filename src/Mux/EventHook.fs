module Wanxiangshu.Mux.EventHook

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Shell.NudgeRuntime
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Mux.FallbackHooks

type private DecodedHookEvent =
    { eventType: string
      workspaceId: string
      properties: obj
      stopReason: string
      errorType: string }

let private decodeHookEvent (event: obj) : DecodedHookEvent =
    let props = Dyn.get event "properties"
    let meta = if Dyn.isNullish props then null else Dyn.get props "metadata"
    { eventType = if Dyn.isNullish event then "" else Dyn.str event "type"
      workspaceId = Dyn.str event "workspaceId"
      properties = if Dyn.isNullish props then null else props
      stopReason = if Dyn.isNullish meta then "" else Dyn.str meta "muxStopReason"
      errorType = if Dyn.isNullish props then "" else Dyn.str props "errorType" }

let private getLastAssistantText (properties: obj) : string =
    if Dyn.isNullish properties then ""
    else
        let parts = Dyn.get properties "parts"
        if Dyn.isNullish parts || not (Dyn.isArray parts) then ""
        else
            (parts :?> obj array)
            |> Array.filter (fun p -> Dyn.str p "type" = "text")
            |> Array.map (fun p -> Dyn.str p "text")
            |> String.concat "\n"

let private parseHookEvent (event: obj) : NudgeRuntimeEvent =
    let decoded = decodeHookEvent event
    if decoded.workspaceId = "" then Ignore
    else
        match decoded.eventType with
        | "stream-end" ->
            StreamEnd(decoded.workspaceId, decoded.stopReason, getLastAssistantText decoded.properties)
        | "stream-abort" -> StreamAbort decoded.workspaceId
        | "error" when decoded.errorType = "aborted" -> AbortedError decoded.workspaceId
        | _ -> Ignore

let createEventHook (deps: obj) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    let getChatHistory =
        if Dyn.isNullish deps then None
        else
            let getter = Dyn.get deps "getChatHistory"
            if Dyn.isNullish getter then None
            else Some (fun (workspaceId: string) -> unbox<JS.Promise<obj array>> (Dyn.call1 getter workspaceId))

    let cleanupOnAbort (event: obj) : unit =
        match parseHookEvent event with
        | NudgeRuntime.StreamAbort workspaceId
        | NudgeRuntime.AbortedError workspaceId when workspaceId <> "" ->
            reviewStore.deactivateReview workspaceId
        | _ -> ()

    let runtime = createNudgeRuntime getChatHistory

    let fallbackRuntime = FallbackRuntimeState()
    let directory =
        if Dyn.isNullish deps then "" else Dyn.str deps "directory"
    let fallbackConfigOpt = loadFallbackConfig directory
    let configLookup : ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> emptyConfig)
    let fallbackHandler =
        createMuxFallbackHandler fallbackRuntime configLookup deps

    let fn = System.Func<obj, obj, JS.Promise<unit>>(fun event helpers ->
        promise {
            cleanupOnAbort event
            let! fbResult = fallbackHandler event
            if not fbResult.Consumed then
                do! runtime.HandleEvent(parseHookEvent event, helpers)
        })
    box fn
