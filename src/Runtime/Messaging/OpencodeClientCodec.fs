module Wanxiangshu.Runtime.OpencodeClientCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.Dyn

let getClientFromPluginCtx (ctx: obj) : Result<obj, DomainError> =
    let client = Dyn.get ctx "client"

    if Dyn.isNullish client then
        Error(InvalidIntent("plugin", "client", "missing"))
    else
        Ok client

let getSessionApiFromClient (client: obj) : Result<obj, DomainError> =
    let session = Dyn.get client "session"

    if Dyn.isNullish session then
        Error(InvalidIntent("plugin", "session", "missing"))
    else
        Ok session

let getConfigApiFromClient (client: obj) : Result<obj, DomainError> =
    let config = Dyn.get client "config"

    if Dyn.isNullish config then
        Error(InvalidIntent("plugin", "config", "missing"))
    else
        Ok config

let invoke1 (arg: obj) (methodName: string) (target: obj) : JS.Promise<obj> = unbox (target?(methodName) (arg))

let invokeClient (client: obj) (method_: string) (arg: obj) : JS.Promise<obj> =
    if Dyn.isNullish client then
        Promise.lift (unbox null)
    else
        match getSessionApiFromClient client with
        | Error _ -> Promise.lift (unbox null)
        | Ok session ->
            let api: obj = Dyn.get session method_

            if Dyn.isNullish api then
                Promise.lift (unbox null)
            else
                unbox<JS.Promise<obj>> (Dyn.callMethod1 session method_ arg)

let parseModelVal (modelVal: obj) : string option =
    if Dyn.isNullish modelVal then
        None
    elif Dyn.typeIs modelVal "string" then
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

let getTurnId (info: obj) (idx: int) : string =
    let time = Dyn.get info "time"
    let completed = Dyn.str time "completed"

    if completed <> "" then
        completed
    else
        let msgId = Dyn.str info "id"

        if msgId <> "" then
            msgId
        else
            sprintf "nudge-fallback-anchor-%d" idx
