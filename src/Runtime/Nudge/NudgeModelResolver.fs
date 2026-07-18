module Wanxiangshu.Runtime.NudgeModelResolver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.NudgeMessageClassifier
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus

let private resolveModelFromMessages (msgs: obj array) (lastAssistantModel: string option) : string option =
    if isNull msgs || msgs.Length = 0 then
        lastAssistantModel
    else
        let lastUserMsgOpt =
            msgs
            |> Array.tryFindBack (fun msg ->
                let role = Dyn.str msg "role"
                let info = Dyn.get msg "info"
                let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""
                (role = "user" || msgRole = "user") && not (classifyUserMessage msg = "nudge"))

        match lastUserMsgOpt |> Option.bind tryGetModelStringFromMessage with
        | Some m -> Some m
        | None ->
            let lastAssistantModelFromMsg =
                msgs
                |> Array.tryFindBack (fun msg ->
                    let role = Dyn.str msg "role"
                    let info = Dyn.get msg "info"
                    let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""

                    (role = "assistant" || msgRole = "assistant")
                    && not (isSyntheticAssistantAgent (Dyn.str info "agent")))
                |> Option.bind tryGetModelStringFromMessage

            match lastAssistantModelFromMsg with
            | Some m -> Some m
            | None -> lastAssistantModel

let resolveNudgeModel
    (msgs: obj array)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionID: string)
    (lastAssistantModel: string option)
    : string option =
    // PRIORITY:
    // 1. Latest human (real user) message model — NEVER overridden by injected models.
    // 2. Runtime model — from session/agent config.
    // 3. Last real user message from message history (non-nudge, non-synthetic).
    // 4. Last non-synthetic assistant model.
    // 5. `lastAssistantModel` parameter (caller-provided fallback).
    match (fallbackRuntime.GetSession sessionID).LatestHumanModel with
    | Some m -> Some m
    | None ->
        match (fallbackRuntime.GetSession sessionID).Model with
        | Some m -> Some(modelWithVariantString m)
        | None -> resolveModelFromMessages msgs lastAssistantModel
