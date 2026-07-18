module Wanxiangshu.Hosts.Opencode.NudgeEffectHelpers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let invokeClient (client: obj) (method_: string) (arg: obj) : JS.Promise<obj> =
    if isNullish client then
        Promise.lift (unbox null)
    else
        match getSessionApiFromClient client with
        | Error _ -> Promise.lift (unbox null)
        | Ok session ->
            let api: obj = get session method_

            if isNullish api then
                Promise.lift (unbox null)
            else
                unbox<JS.Promise<obj>> (callMethod1 session method_ arg)

let parseModelVal (modelVal: obj) : string option =
    if isNullish modelVal then
        None
    elif typeIs modelVal "string" then
        let s = string modelVal
        if s = "" then None else Some s
    else
        let providerID = str modelVal "providerID"
        let modelID = str modelVal "modelID"
        let variant = str modelVal "variant"
        let suffix = if variant <> "" then ":" + variant else ""

        if providerID = "" || modelID = "" then
            let idVal = str modelVal "id"
            if idVal <> "" then Some(idVal + suffix) else None
        else
            Some(sprintf "%s/%s%s" providerID modelID suffix)

let getTurnId (info: obj) (idx: int) : string =
    let time = get info "time"
    let completed = str time "completed"

    if completed <> "" then
        completed
    else
        let msgId = str info "id"

        if msgId <> "" then
            msgId
        else
            sprintf "nudge-fallback-anchor-%d" idx
