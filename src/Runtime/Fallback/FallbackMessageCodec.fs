// Based on opencode-auto-resume raw-tool-call detection patterns + Wanxiangshu protocol extensions.
module Wanxiangshu.Runtime.Fallback.FallbackMessageCodec

open Wanxiangshu.Runtime
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.FallbackMessageParser
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.Fallback.FallbackMessageDetection

let private parseModelFromString (s: string) : FallbackModel option =
    let colon = s.IndexOf(':')
    let providerAndModel = if colon > 0 then s.[0 .. colon - 1] else s

    let variantOpt =
        if colon > 0 && colon < s.Length - 1 then
            Some(s.[colon + 1 ..].Trim())
        else
            None

    let slash = providerAndModel.IndexOf('/')

    if slash <= 0 || slash >= providerAndModel.Length - 1 then
        None
    else
        Some
            { ProviderID = providerAndModel.[0 .. slash - 1].Trim()
              ModelID = providerAndModel.[slash + 1 ..].Trim()
              Variant = variantOpt
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

let private buildModelFromObjFields (modelObj: obj) : FallbackModel option =
    let getStr k k2 =
        let v = Dyn.str modelObj k in if v <> "" then v else Dyn.str modelObj k2

    let provider = getStr "providerID" "provider"

    let modelId =
        let v = Dyn.str modelObj "modelID"
        let v = if v <> "" then v else Dyn.str modelObj "id"
        if v <> "" then v else Dyn.str modelObj "model"

    let variant = Dyn.str modelObj "variant"
    let variantOpt = if variant <> "" then Some variant else None

    if provider <> "" && modelId <> "" then
        Some
            { ProviderID = provider
              ModelID = modelId
              Variant = variantOpt
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }
    else
        None

/// Decode a FallbackModel from a raw host object (which can be a string like "openai/gpt-5" or an object).
let decodeModelFromObj (modelObj: obj) : FallbackModel option =
    if isNull modelObj || Dyn.isNullish modelObj then
        None
    elif Dyn.typeIs modelObj "string" then
        parseModelFromString (string modelObj)
    else
        buildModelFromObjFields modelObj

/// Find the last user message in the message list and extract its specified model if present.
/// Whether the user message was injected by the fallback runtime is now a
/// per-session log question, not a text-sniffing one: callers should compose
/// `tryGetLatestUserModel` with `fallbackRuntime.GetInjectedModel` when they
/// need the most authoritative "what model should this turn be routed to"
/// answer (see `ResolveLatestModel.resolve`).
let tryGetLatestUserModel (msgs: obj array) : FallbackModel option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let info = Dyn.get msg "info"

            if isNull info || Dyn.isNullish info then
                None
            else
                let role = Dyn.str info "role"

                if role <> "user" then
                    None
                else
                    let modelObj = Dyn.get info "model"
                    decodeModelFromObj modelObj)
