module Wanxiangshu.Runtime.ContextBudgetUsageCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.ContextBudget

let utf8JsonBytes (value: obj) : int =
    emitJsExpr value "new TextEncoder().encode(JSON.stringify($0)).length"

let tryResolveClient (target: obj) : obj option =
    if isNullish target then
        None
    else
        let client =
            if not (isNullish (get target "session")) then
                target
            else
                let c = get target "client"

                if not (isNullish c) && not (isNullish (get c "session")) then
                    c
                else
                    box null

        if isNullish client then None else Some client

let tryGetUsageFromSessionApi (sessionApi: obj) (sessionID: string) (directory: string) : JS.Promise<int option> =
    promise {
        try
            let arg = createObj [ "sessionID", box sessionID; "directory", box directory ]

            let! res = unbox<JS.Promise<obj>> (sessionApi?get (arg))

            if isNullish res then
                return None
            else
                let data = get res "data"

                if isNullish data then
                    return None
                else
                    let tokens = get data "tokens"

                    if isNullish tokens then
                        return None
                    else
                        let inputVal = get tokens "input"
                        let cacheObj = get tokens "cache"

                        let cacheRead =
                            if isNullish cacheObj then
                                0.0
                            else
                                let r = get cacheObj "read"

                                if isNullish r || not (typeIs r "number") then
                                    0.0
                                else
                                    unbox<float> r

                        if isNullish inputVal || not (typeIs inputVal "number") then
                            return None
                        else
                            let inputNum = int (unbox<float> inputVal)
                            let cacheNum = int cacheRead
                            return Some(inputNum + cacheNum)
        with _ ->
            return None
    }

let isBacklogEncodedMessage (host: Host) (msg: obj) : bool =
    if isNullish msg then
        false
    else
        let idVal = get msg "id"

        let isPrefix =
            not (isNullish idVal)
            && typeIs idVal "string"
            && (string idVal).StartsWith(backlogPrefixIdPrefix)

        let parts = get msg "parts"

        let isProjection =
            if not (isNullish parts) && typeIs parts "object" && isArray parts then
                let arr = unbox<obj array> parts

                arr
                |> Array.exists (fun p ->
                    let toolVal =
                        let t = get p "tool"

                        if not (isNullish t) then
                            t
                        else
                            let tn = get p "toolName"
                            if not (isNullish tn) then tn else box ""

                    let toolStr = string toolVal
                    let state = get p "state"

                    if toolStr = todoWriteToolName host && not (isNullish state) then
                        let outputVal = get state "output"

                        not (isNullish outputVal)
                        && typeIs outputVal "string"
                        && (string outputVal).Contains("Completed work from folded turns")
                    else
                        false)
            else
                false

        isPrefix || isProjection

let backlogBytesFromEncoded (host: Host) (encodedAll: obj array) : int =
    let mutable sum = 0

    for msg in encodedAll do
        if isBacklogEncodedMessage host msg then
            sum <- sum + utf8JsonBytes msg

    sum

let tryExtractMaxInputTokens (target: obj) : int option =
    ContextBudgetLimitResolver.tryExtractMaxInputTokens target

let tryGetMaxInputTokensAsync (target: obj) (sessionID: string) (directory: string) : JS.Promise<int option> =
    ContextBudgetLimitResolver.tryGetMaxInputTokensAsync target sessionID directory

let tryGetRealContextUsage
    (target: obj)
    (sessionID: string)
    (directory: string)
    : (unit -> JS.Promise<UsageObservation option>) option =
    if isNullish target then
        None
    else
        match tryResolveClient target with
        | None -> None
        | Some client ->
            let sessionApi = get client "session"

            if isNullish sessionApi || isNullish (get sessionApi "get") then
                None
            else
                Some(fun () ->
                    promise {
                        let! tokens = tryGetUsageFromSessionApi sessionApi sessionID directory

                        return
                            tokens
                            |> Option.map (fun value ->
                                { AssistantMessageID = sessionID
                                  InputTokens = int64 value })
                    })

let resolveMaxInputTokens (targets: obj list) (sessionID: string) (directory: string) : JS.Promise<int> =
    ContextBudgetResolve.resolveMaxInputTokens targets sessionID directory
