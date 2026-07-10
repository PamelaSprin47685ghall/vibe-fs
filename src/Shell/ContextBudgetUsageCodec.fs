module Wanxiangshu.Shell.ContextBudgetUsageCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

let isBacklogEncodedMessage (host: Host) (msg: obj) : bool =
    if isNullish msg then false
    else
        let idVal = get msg "id"
        let isPrefix = not (isNullish idVal) && typeIs idVal "string" && (string idVal).StartsWith(backlogPrefixIdPrefix)
        let parts = get msg "parts"
        let isProjection =
            if not (isNullish parts) && typeIs parts "object" && isArray parts then
                let arr = unbox<obj array> parts
                arr |> Array.exists (fun p ->
                    let toolVal =
                        let t = get p "tool"
                        if not (isNullish t) then t
                        else
                            let tn = get p "toolName"
                            if not (isNullish tn) then tn else box ""
                    let toolStr = string toolVal
                    let state = get p "state"
                    if toolStr = todoWriteToolName host && not (isNullish state) then
                        let outputVal = get state "output"
                        not (isNullish outputVal) && typeIs outputVal "string" && (string outputVal).Contains("Completed work from folded turns")
                    else false)
            else false
        isPrefix || isProjection

let backlogBytesFromEncoded (host: Host) (encodedAll: obj array) : int =
    let mutable sum = 0
    for msg in encodedAll do
        if isBacklogEncodedMessage host msg then
            sum <- sum + JS.JSON.stringify(msg).Length
    sum

let tryGetGetContextUsage (target: obj) : (obj array -> JS.Promise<int option>) option =
    if isNullish target then None
    else
        let prop = get target "getContextUsage"
        if isNullish prop || not (typeIs prop "function") then None
        else
            Some (fun (encoded: obj array) ->
                promise {
                    try
                        let res = target?getContextUsage(encoded)
                        if isNullish res then
                            return None
                        else
                            let thenProp = get res "then"
                            if not (isNullish thenProp) && typeIs thenProp "function" then
                                let! resolved = unbox<JS.Promise<obj>> res
                                if isNullish resolved then return None
                                elif typeIs resolved "number" then return Some (int (unbox<float> resolved))
                                elif typeIs resolved "object" then
                                    let props = [ "tokenCount"; "inputTokens"; "totalTokens"; "tokens"; "input_tokens"; "total_tokens"; "usage" ]
                                    return props |> List.tryPick (fun p ->
                                        let v = get resolved p
                                        if not (isNullish v) && typeIs v "number" then Some (int (unbox<float> v)) else None)
                                else return None
                            elif typeIs res "number" then
                                return Some (int (unbox<float> res))
                            elif typeIs res "object" then
                                let props = [ "tokenCount"; "inputTokens"; "totalTokens"; "tokens"; "input_tokens"; "total_tokens"; "usage" ]
                                return props |> List.tryPick (fun p ->
                                    let v = get res p
                                    if not (isNullish v) && typeIs v "number" then Some (int (unbox<float> v)) else None)
                            else
                                return None
                    with _ ->
                        return None
                })
