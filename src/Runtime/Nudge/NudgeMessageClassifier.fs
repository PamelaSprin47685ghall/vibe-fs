module Wanxiangshu.Runtime.NudgeMessageClassifier

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec

let tryGetOriginFromMessage (msg: obj) : MessageOrigin option =
    if Dyn.isNullish msg then
        None
    else
        let parts = Dyn.get msg "parts"
        let metaRecord = WanxiangshuMetadataCodec.tryDecodeFromParts parts

        match metaRecord with
        | Some m when m.Origin.IsSome -> m.Origin
        | _ ->
            let meta =
                let topMeta = Dyn.get msg "metadata"

                if not (Dyn.isNullish topMeta) then
                    topMeta
                else
                    let info = Dyn.get msg "info"

                    if not (Dyn.isNullish info) then
                        Dyn.get info "metadata"
                    else
                        null

            let ws =
                if Dyn.isNullish meta then
                    null
                else
                    Dyn.get meta "wanxiangshu"

            if not (Dyn.isNullish ws) then
                let o = Dyn.str ws "origin"
                if o <> "" then MessageOrigin.tryParse o else None
            else
                None

let isNudgeFromParts (parts: obj) : bool =
    match WanxiangshuMetadataCodec.tryDecodeFromParts parts with
    | Some m ->
        match m.Origin with
        | Some orig -> MessageOrigin.isNudge orig
        | None -> m.Kind = WanxiangshuMetadataCodec.nudgeKind
    | None -> false

let classifyUserMessage (msg: obj) : string =
    if Dyn.isNullish msg then
        "user"
    else
        match tryGetOriginFromMessage msg with
        | Some orig when MessageOrigin.isNudge orig -> "nudge"
        | _ ->
            let parts = Dyn.get msg "parts"
            if isNudgeFromParts parts then "nudge" else "user"

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
