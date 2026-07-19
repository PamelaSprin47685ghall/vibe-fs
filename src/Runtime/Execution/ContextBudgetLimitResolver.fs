module Wanxiangshu.Runtime.ContextBudgetLimitResolver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.Dyn

type LimitResolution =
    | InputLimit of int
    | ContextLimit of int

let valueOf =
    function
    | InputLimit v -> v
    | ContextLimit v -> v

let extractLimitFromModelDetailed (modelObj: obj) : LimitResolution option =
    if isNullish modelObj then
        None
    else
        let limitObj = get modelObj "limit"

        if isNullish limitObj then
            None
        else
            let inputVal = get limitObj "input"

            if not (isNullish inputVal) && typeIs inputVal "number" then
                Some(InputLimit(int (unbox<float> inputVal)))
            else
                let ctxVal = get limitObj "context"

                if not (isNullish ctxVal) && typeIs ctxVal "number" then
                    Some(ContextLimit(int (unbox<float> ctxVal)))
                else
                    None

let extractLimitFromModel (modelObj: obj) : int option =
    extractLimitFromModelDetailed modelObj |> Option.map valueOf

let tryExtractMaxInputTokensDetailed (target: obj) : LimitResolution option =
    if isNullish target then
        None
    else
        let trySessionModel (sess: obj) =
            if isNullish sess then
                None
            else
                extractLimitFromModelDetailed (get sess "model")

        match trySessionModel target with
        | Some limit -> Some limit
        | None ->
            let sessionObj = get target "session"

            match trySessionModel sessionObj with
            | Some limit -> Some limit
            | None ->
                let clientObj = get target "client"

                let clientSession =
                    if not (isNullish clientObj) then
                        get clientObj "session"
                    else
                        box null

                trySessionModel clientSession

let tryExtractMaxInputTokens (target: obj) : int option =
    tryExtractMaxInputTokensDetailed target |> Option.map valueOf

let private sessionGetArg (sessionID: string) (directory: string) : obj =
    createObj
        [ "sessionID", box sessionID
          "id", box sessionID
          "directory", box directory
          "path", box (createObj [ "id", box sessionID; "sessionID", box sessionID ])
          "query", box (createObj [ "directory", box directory; "workspace", box "" ]) ]

let private sessionModelOf (data: obj) : obj =
    if isNullish data then
        data
    else
        let data2 = get data "data"
        let sessionBody = if isNullish data2 then data else data2
        get sessionBody "model"

let tryGetClient (target: obj) : obj =
    if not (isNullish (get target "session")) then
        target
    else
        let c = get target "client"

        if not (isNullish c) && not (isNullish (get c "session")) then
            c
        else
            box null

let private tryExtractModelRefFromModelObj (modelObj: obj) : (string * string) option =
    if isNullish modelObj then
        None
    elif typeIs modelObj "string" then
        let modelStr = string modelObj
        let parts = modelStr.Split([| '/' |])

        if parts.Length = 2 then
            Some(parts.[1], parts.[0])
        else
            None
    else
        let mId =
            let v = string (get modelObj "modelID")
            if v <> "" then v else string (get modelObj "id")

        let pId =
            let v = string (get modelObj "providerID")
            if v <> "" then v else string (get modelObj "provider")

        if mId = "" || pId = "" then None else Some(mId, pId)

let tryGetSessionModelRef (target: obj) (sessionID: string) : JS.Promise<(string * string) option> =
    promise {
        if isNullish target then
            return None
        else
            let client = tryGetClient target

            if isNullish client then
                return None
            else
                let sessionApi = get client "session"

                let getFn =
                    if isNullish sessionApi then
                        box null
                    else
                        get sessionApi "get"

                if isNullish getFn || not (typeIs getFn "function") then
                    return None
                else
                    try
                        let arg = sessionGetArg sessionID ""
                        let raw = Wanxiangshu.Runtime.Dyn.callWithThis1 getFn sessionApi arg

                        let! res =
                            if Wanxiangshu.Runtime.Dyn.typeIs (Wanxiangshu.Runtime.Dyn.get raw "then") "function" then
                                unbox<JS.Promise<obj>> raw
                            else
                                Promise.lift raw

                        if isNullish res then
                            return None
                        else
                            let data = get res "data"
                            return tryExtractModelRefFromModelObj (sessionModelOf data)
                    with _ ->
                        return None
    }



let tryGetMaxInputTokensAsyncDetailed
    (target: obj)
    (sessionID: string)
    (directory: string)
    : JS.Promise<LimitResolution option> =
    promise {
        if isNullish target then
            return None
        else
            let client = tryGetClient target

            if isNullish client then
                return None
            else
                let sessionApi = get client "session"
                let getFn = get sessionApi "get"

                if isNullish sessionApi || isNullish getFn then
                    return None
                else
                    try
                        let arg = sessionGetArg sessionID directory
                        let raw = Wanxiangshu.Runtime.Dyn.callWithThis1 getFn sessionApi arg

                        let! res =
                            if Wanxiangshu.Runtime.Dyn.typeIs (Wanxiangshu.Runtime.Dyn.get raw "then") "function" then
                                unbox<JS.Promise<obj>> raw
                            else
                                Promise.lift raw

                        if isNullish res then
                            return None
                        else
                            let data = get res "data"
                            let modelObj = sessionModelOf data

                            match extractLimitFromModelDetailed modelObj with
                            | Some limit -> return Some limit
                            | None -> return None
                    with _ ->
                        return None
    }

let tryGetMaxInputTokensAsync (target: obj) (sessionID: string) (directory: string) : JS.Promise<int option> =
    promise {
        let! res = tryGetMaxInputTokensAsyncDetailed target sessionID directory
        return res |> Option.map valueOf
    }
