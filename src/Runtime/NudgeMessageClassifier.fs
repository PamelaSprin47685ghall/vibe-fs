module Wanxiangshu.Runtime.NudgeMessageClassifier

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FallbackKernel.Types

let isNudgePromptText (text: string) : bool =
    let t = text.Trim()

    t.Contains("There are still incomplete todos")
    || t.Contains("You are in loop mode. You must call the submit_review")
    || t.Contains("A background runner task is still active")
    || t.Contains("the system context is about to be suspended")
    || t.Contains("You must immediately force an emergency stop")

let private messageTexts (message: obj) : string list =
    let parts = Dyn.get message "parts"

    if not (Dyn.isArray parts) then
        []
    else
        (parts :?> obj array)
        |> Array.toList
        |> List.choose (fun part ->
            match Dyn.str part "type" with
            | "text" ->
                let t = Dyn.get part "text"
                if Dyn.isNullish t then None else Some(string t)
            | "tool"
            | "dynamic-tool" ->
                let output =
                    let direct = Dyn.get part "output"

                    if not (Dyn.isNullish direct) then
                        string direct
                    else
                        let state = Dyn.get part "state"

                        if Dyn.isNullish state then
                            ""
                        else
                            string (Dyn.get state "output")

                if output = "" then None else Some output
            | _ -> None)

let classifyUserMessage (msg: obj) : string =
    let text = messageTexts msg |> String.concat "\n"
    if isNudgePromptText text then "nudge" else "user"

let tryGetModelStringFromMessage (msg: obj) : string option =
    let info = Dyn.get msg "info"

    if isNull info || Dyn.isNullish info then
        None
    else
        let modelVal = Dyn.get info "model"

        if isNull modelVal || Dyn.isNullish modelVal then
            None
        else if Dyn.typeIs modelVal "string" then
            let s = string modelVal
            if s = "" then None else Some s
        else
            let providerID = Dyn.str modelVal "providerID"
            let modelID = Dyn.str modelVal "modelID"
            let variant = Dyn.str modelVal "variant"
            let suffix = if variant <> "" then ":" + variant else ""

            if providerID = "" || modelID = "" then
                let idVal = Dyn.str modelVal "id"
                if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" providerID modelID suffix)

let modelWithVariantString (m: FallbackModel) : string =
    match m.Variant with
    | Some v -> sprintf "%s/%s:%s" m.ProviderID m.ModelID v
    | None -> sprintf "%s/%s" m.ProviderID m.ModelID
