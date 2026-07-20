module Wanxiangshu.Runtime.ContextBudgetLimitResolverPure

open Fable.Core
open Fable.Core.JsInterop

open Wanxiangshu.Runtime.Dyn

type PureLimitResolution =
    | PureInputLimit of int
    | PureContextLimit of int

let valueOf =
    function
    | PureInputLimit v -> v
    | PureContextLimit v -> v

let extractLimitFromModelDetailed (modelObj: obj) : PureLimitResolution option =
    if isNullish modelObj then
        None
    else
        let limitObj = get modelObj "limit"

        if isNullish limitObj then
            None
        else
            let inputVal = get limitObj "input"

            if not (isNullish inputVal) && typeIs inputVal "number" then
                Some(PureInputLimit(int (unbox<float> inputVal)))
            else
                let ctxVal = get limitObj "context"

                if not (isNullish ctxVal) && typeIs ctxVal "number" then
                    Some(PureContextLimit(int (unbox<float> ctxVal)))
                else
                    None

let extractLimitFromModel (modelObj: obj) : int option =
    extractLimitFromModelDetailed modelObj |> Option.map valueOf

let tryExtractMaxInputTokensDetailed (target: obj) : PureLimitResolution option =
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

let sessionGetArg (sessionID: string) (directory: string) : obj =
    createObj
        [ "sessionID", box sessionID
          "id", box sessionID
          "directory", box directory
          "path", box (createObj [ "id", box sessionID; "sessionID", box sessionID ])
          "query", box (createObj [ "directory", box directory; "workspace", box "" ]) ]

let sessionModelOf (data: obj) : obj =
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

let tryExtractModelRefFromModelObj (modelObj: obj) : (string * string) option =
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
