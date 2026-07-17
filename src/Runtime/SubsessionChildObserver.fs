module Wanxiangshu.Runtime.SubsessionChildObserver

open Fable.Core
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventRouter

/// Non-control observation: update agent/model metadata from child busy/updated/message events.
/// Does NOT modify Actor lifecycle — only FallbackRuntimeStore caches.
let observeChildMetadata (runtime: FallbackRuntimeStore) (sessionId: string) (rawEvent: obj) : unit =
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

/// Consume a non-control child event: observe metadata, never enter the main fallback coordinator.
let absorbChildMetadata
    (workspaceRoot: string)
    (runtime: FallbackRuntimeStore)
    (sessionId: string)
    (rawEvent: obj)
    : bool =
    if isChildSession workspaceRoot sessionId then
        observeChildMetadata runtime sessionId rawEvent
        true
    else
        false
