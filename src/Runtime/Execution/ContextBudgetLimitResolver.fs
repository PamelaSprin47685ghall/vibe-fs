module Wanxiangshu.Runtime.ContextBudgetLimitResolver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ContextBudgetLimitResolverPure

type LimitResolution =
    | InputLimit of int
    | ContextLimit of int

let valueOf =
    function
    | InputLimit v -> v
    | ContextLimit v -> v

let extractLimitFromModelDetailed (modelObj: obj) : LimitResolution option =
    ContextBudgetLimitResolverPure.extractLimitFromModelDetailed modelObj
    |> Option.map (function
        | PureLimitResolution.PureInputLimit v -> InputLimit v
        | PureLimitResolution.PureContextLimit v -> ContextLimit v)

let extractLimitFromModel (modelObj: obj) : int option =
    extractLimitFromModelDetailed modelObj |> Option.map valueOf

let tryExtractMaxInputTokensDetailed (target: obj) : LimitResolution option =
    ContextBudgetLimitResolverPure.tryExtractMaxInputTokensDetailed target
    |> Option.map (function
        | PureLimitResolution.PureInputLimit v -> InputLimit v
        | PureLimitResolution.PureContextLimit v -> ContextLimit v)

let tryExtractMaxInputTokens (target: obj) : int option =
    tryExtractMaxInputTokensDetailed target |> Option.map valueOf

let private sessionGetArg = ContextBudgetLimitResolverPure.sessionGetArg
let private sessionModelOf = ContextBudgetLimitResolverPure.sessionModelOf
let private tryExtractModelRefFromModelObj = ContextBudgetLimitResolverPure.tryExtractModelRefFromModelObj
let tryGetClient = ContextBudgetLimitResolverPure.tryGetClient

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

                            match ContextBudgetLimitResolverPure.extractLimitFromModelDetailed modelObj with
                            | Some(PureLimitResolution.PureInputLimit v) -> return Some(InputLimit v)
                            | Some(PureLimitResolution.PureContextLimit v) -> return Some(ContextLimit v)
                            | None -> return None
                    with _ ->
                        return None
    }

let tryGetMaxInputTokensAsync (target: obj) (sessionID: string) (directory: string) : JS.Promise<int option> =
    promise {
        let! res = tryGetMaxInputTokensAsyncDetailed target sessionID directory
        return res |> Option.map valueOf
    }
