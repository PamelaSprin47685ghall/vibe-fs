module Wanxiangshu.Hosts.Opencode.SubsessionDispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec

module Metadata = Wanxiangshu.Runtime.OpencodeSessionPromptCodec.WanxiangshuMetadataCodec

/// Format a FallbackModel option into the string option shape expected by
/// createPromptBodyWithModelAndNonce. None means ModelDirective.DelegateToHost:
/// no model field will be sent to the host, letting OpenCode's own
/// agent.<name>.model static config (or currentModel fallback chain) resolve
/// the model — this is the exact mechanism that stops wanxiangshu's parent-
/// session model injection from overriding opencode.jsonc.
let buildDispatchModelString (model: FallbackModel option) : string option =
    model
    |> Option.map (fun m ->
        match m.Variant with
        | Some v -> sprintf "%s/%s:%s" m.ProviderID m.ModelID v
        | None -> sprintf "%s/%s" m.ProviderID m.ModelID)

let private getNestedStateStatus (stateObj: obj) : string =
    if not (Dyn.isNullish stateObj) then
        if Dyn.typeIs stateObj "string" then
            string stateObj
        else
            Dyn.str stateObj "status"
    else
        ""

let private checkActive (s: string) =
    let ls = s.Trim().ToLower()
    ls = "busy" || ls = "running" || ls = "pending"

let isMessageMatch (nonce: string) (msg: obj) : bool =
    if Dyn.str msg "id" = nonce then
        true
    else
        let parts = Dyn.get msg "parts"

        if not (Dyn.isNullish parts) && Dyn.isArray parts then
            let arr = unbox<obj array> parts

            arr
            |> Array.tryPick (fun part -> Metadata.tryDecodeFromPart part |> Option.filter (fun m -> m.Nonce = nonce))
            |> Option.isSome
        else
            false

let isMessageActive (msg: obj) : bool =
    let status = Dyn.str msg "status"
    let props = Dyn.get msg "props"
    let info = Dyn.get msg "info"

    let infoStatus =
        if not (Dyn.isNullish info) then
            Dyn.str info "status"
        else
            ""

    let propsStatus =
        if not (Dyn.isNullish props) then
            Dyn.str props "status"
        else
            ""

    let state = Dyn.get msg "state"
    let stateStatus = getNestedStateStatus state
    let infoStateStatus = getNestedStateStatus (Dyn.get info "state")

    checkActive status
    || checkActive infoStatus
    || checkActive propsStatus
    || checkActive stateStatus
    || checkActive infoStateStatus
