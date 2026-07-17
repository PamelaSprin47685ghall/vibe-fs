module Wanxiangshu.Hosts.Opencode.ChatHooksDecoders

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ChatHookOutputCodec
open Wanxiangshu.Runtime.ChildAgentRegistry

/// Read the model identifier from any of the candidate hook payload
/// locations. Mirrors the same fallback ladder OpenCode's `chat.message`
/// hook fills (input.model, input.message.model, input.info.model,
/// output.message.model).
let tryGetModelStringFromHook (input: obj) (output: obj) : string option =
    let candidates =
        [ Dyn.get input "model"
          (let msg = Dyn.get input "message" in

           if not (Dyn.isNullish msg) then
               Dyn.get msg "model"
           else
               null)
          (let info = Dyn.get input "info" in

           if not (Dyn.isNullish info) then
               Dyn.get info "model"
           else
               null)
          (let msg = chatMessageFromHookOutput output in if msg.IsSome then Dyn.get msg.Value "model" else null) ]

    candidates
    |> List.tryPick (fun mVal ->
        if Dyn.isNullish mVal then
            None
        elif Dyn.typeIs mVal "string" then
            let s = mVal :?> string
            if s <> "" then Some s else None
        else
            let providerID = Dyn.str mVal "providerID"
            let modelID = Dyn.str mVal "modelID"
            let variant = Dyn.str mVal "variant"
            let suffix = if variant <> "" then ":" + variant else ""

            if providerID = "" || modelID = "" then
                let idVal = Dyn.str mVal "id"
                if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" providerID modelID suffix))

/// Read the flat nonce from any part's metadata, if any. System
/// messages authored by the nudge or subsession paths stamp a flat
/// `metadata.nonce` on their text part.
let tryGetNonceFromParts (parts: obj) : string option =
    if Dyn.isNullish parts || not (Dyn.isArray parts) then
        None
    else
        let arr = parts :?> obj array

        arr
        |> Array.tryPick (fun part ->
            let metadata = Dyn.get part "metadata"

            if Dyn.isNullish metadata then
                None
            else
                let nonce = Dyn.str metadata "nonce"
                if nonce <> "" then Some nonce else None)

/// Read the wanxiangshu namespaced kind from any part's metadata, if
/// any. Used to identify system-synthesised messages (e.g. fallback
/// continuation prompts) that carry namespaced provenance instead of
/// a flat nonce.
let tryGetWanxiangshuKind (parts: obj) : string option =
    if Dyn.isNullish parts || not (Dyn.isArray parts) then
        None
    else
        let arr = parts :?> obj array

        arr
        |> Array.tryPick (fun part ->
            let metadata = Dyn.get part "metadata"

            if Dyn.isNullish metadata then
                None
            else
                let ws = Dyn.get metadata "wanxiangshu"

                if Dyn.isNullish ws then
                    None
                else
                    let kind = Dyn.str ws "kind"
                    if kind <> "" then Some kind else None)

/// Extract the role of a chat message from the host hook output, or
/// empty string when absent. Used to gate turn-boundary side effects
/// to genuine human messages.
///
/// Per OpenCode's `chat.message` hook contract
/// (packages/opencode/src/session/prompt.ts), `output.message` IS the
/// `UserMessage` (info body), so `role` lives at its top level — there
/// is no nested `info` wrapper. Reading `output.message.info.role`
/// would silently return "" in production and starve `OnNewHumanMessage`.
let tryGetChatMessageRole (output: obj) : string =
    let msg = Dyn.get output "message"

    if Dyn.isNullish msg then "" else Dyn.str msg "role"

/// Provenance extracted from wanxiangshu metadata in a message part.
type WanxiangshuProvenance =
    { Kind: string; ContinuationId: string }

/// Extract wanxiangshu provenance from message parts metadata.
/// Returns None when no wanxiangshu metadata is found or parts are invalid.
let tryDecodeWanxiangshuProvenance (parts: obj) : WanxiangshuProvenance option =
    if Dyn.isNullish parts || not (Dyn.isArray parts) then
        None
    else
        let arr = parts :?> obj array

        arr
        |> Array.tryPick (fun part ->
            let metadata = Dyn.get part "metadata"

            if Dyn.isNullish metadata then
                None
            else
                let ws = Dyn.get metadata "wanxiangshu"

                if Dyn.isNullish ws then
                    None
                else
                    let kind = Dyn.str ws "kind"
                    let continuationId = Dyn.str ws "continuationId"

                    if kind <> "" && continuationId <> "" then
                        Some
                            { Kind = kind
                              ContinuationId = continuationId }
                    else
                        None)

let resolveAgent (registry: ChildAgentRegistry) (input: obj) (output: obj) : string =
    resolveHookAgent registry input (Some output) "manager"
