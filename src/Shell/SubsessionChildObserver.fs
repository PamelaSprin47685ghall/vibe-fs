module Wanxiangshu.Shell.SubsessionChildObserver

open Fable.Core
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackMessageCodec
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.SubsessionActorRegistry
open Wanxiangshu.Shell.SubsessionEventRouter

/// Non-control observation: update agent/model metadata from child busy/updated/message events.
/// Does NOT modify Actor lifecycle — only FallbackRuntimeState caches.
let observeChildMetadata (runtime: FallbackRuntimeState) (sessionId: string) (rawEvent: obj) : unit =
    if sessionId = "" || isNull rawEvent || Dyn.isNullish rawEvent then
        ()
    else
        let eventObj =
            let e = Dyn.get rawEvent "event"
            if Dyn.isNullish e then rawEvent else e

        let infoCandidates =
            [ Dyn.get eventObj "info"
              Dyn.get rawEvent "info"
              Dyn.get (Dyn.get eventObj "properties") "info"
              Dyn.get eventObj "properties"
              // message.updated often carries model on message.info
              Dyn.get (Dyn.get eventObj "message") "info"
              Dyn.get (Dyn.get rawEvent "props") "info" ]

        for info in infoCandidates do
            if not (Dyn.isNullish info) then
                let agent = Dyn.str info "agent"

                if agent <> "" then
                    runtime.SetAgentName sessionId agent

                let modelObj = Dyn.get info "model"

                match decodeModelFromObj modelObj with
                | Some m -> runtime.SetModel sessionId m
                | None -> ()

                // Some hosts put model at top-level of message info as string fields.
                let provider = Dyn.str info "providerID"
                let modelId = Dyn.str info "modelID"

                if provider <> "" && modelId <> "" then
                    match
                        decodeModelFromObj (
                            box
                                {| providerID = provider
                                   modelID = modelId
                                   variant = Dyn.str info "variant" |}
                        )
                    with
                    | Some m -> runtime.SetModel sessionId m
                    | None -> ()

/// Consume a non-control child event: observe metadata, never enter Main FallbackEventBridge.
let absorbChildMetadata
    (runtime: FallbackRuntimeState)
    (sessionId: string)
    (rawEvent: obj)
    : bool =
    if isChildSession sessionId then
        observeChildMetadata runtime sessionId rawEvent
        true
    else
        false
