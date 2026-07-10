module Wanxiangshu.Opencode.FallbackHooksHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackMessageCodec

let getEventType (rawEvent: obj) : string =
    Dyn.str (Dyn.get rawEvent "event") "type"

let getProps (rawEvent: obj) : obj =
    let event = Dyn.get rawEvent "event"
    let rawProps = Dyn.get event "properties"
    if Dyn.isNullish rawProps then event else rawProps

let opencodeErrorInput (errorObj: obj) : ErrorInput =
    let errorName = Dyn.str errorObj "name"
    let message = Dyn.str errorObj "message"

    { ErrorName = errorName
      DomainError = Some(translateJsError errorObj)
      Message = message
      StatusCode =
        let sc = Dyn.str errorObj "statusCode"
        if sc <> "" then Some(int sc) else None
      IsRetryable =
        let ir = Dyn.str errorObj "isRetryable"
        if ir <> "" then Some(ir = "true") else None }

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

let tryReadLatestMessageInfo (client: obj) (sessionID: string) : JS.Promise<obj option> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        let! resp = invokeClient client "messages" arg
        let data = Dyn.get resp "data"

        if not (Dyn.isArray data) then
            return None
        else
            let messages = data :?> obj array

            return
                messages
                |> Array.tryFindBack (fun msg ->
                    let info = Dyn.get msg "info"

                    if Dyn.isNullish info then
                        false
                    else
                        let mObj = Dyn.get info "model"
                        let aObj = Dyn.get info "agent"
                        not (Dyn.isNullish mObj) || (not (Dyn.isNullish aObj) && string aObj <> ""))
                |> Option.map (fun msg -> Dyn.get msg "info")
    }

let getSessionStaticModelAndAgent (session: obj) : FallbackModel option * string option =
    let modelObj = Dyn.get session "model"
    let modelOpt = decodeModelFromObj modelObj

    let agentOpt =
        let agentStr = Dyn.str session "agent"
        if agentStr <> "" then Some agentStr else None

    modelOpt, agentOpt

let tryGetSessionModelAndAgentAsync
    (client: obj)
    (sessionID: string)
    : JS.Promise<FallbackModel option * string option> =
    promise {
        if Dyn.isNullish client then
            return None, None
        else
            match getSessionApiFromClient client with
            | Error _ -> return None, None
            | Ok session ->
                let api: obj = Dyn.get session "get"

                if Dyn.isNullish api then
                    return getSessionStaticModelAndAgent session
                else
                    try
                        let! resp =
                            unbox<JS.Promise<obj>> (Dyn.callMethod1 session "get" (box {| sessionID = sessionID |}))

                        if Dyn.isNullish resp then
                            return getSessionStaticModelAndAgent session
                        else
                            let data = Dyn.get resp "data"

                            if Dyn.isNullish data then
                                return getSessionStaticModelAndAgent session
                            else
                                let modelOpt =
                                    let modelObj = Dyn.get data "model"
                                    decodeModelFromObj modelObj

                                let agentOpt =
                                    let agentStr = Dyn.str data "agent"
                                    if agentStr <> "" then Some agentStr else None

                                match modelOpt, agentOpt with
                                | None, None -> return getSessionStaticModelAndAgent session
                                | _ -> return modelOpt, agentOpt
                    with _ ->
                        return getSessionStaticModelAndAgent session
    }

let tryReadCurrentModel (client: obj) (sessionID: string) : JS.Promise<FallbackModel option> =
    promise {
        if Dyn.isNullish client then
            return None
        else
            let! liveModelOpt, _ = tryGetSessionModelAndAgentAsync client sessionID

            match liveModelOpt with
            | Some m -> return Some m
            | None ->
                let! infoOpt = tryReadLatestMessageInfo client sessionID

                match infoOpt with
                | None -> return None
                | Some info ->
                    let model = Dyn.get info "model"
                    return decodeModelFromObj model
    }
