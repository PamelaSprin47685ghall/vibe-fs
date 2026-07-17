module Wanxiangshu.Runtime.OpencodeContextBudgetObservation

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.ContextBudget

let private positiveInt64 (value: obj) : int64 option =
    match value with
    | :? int as n when n > 0 -> Some(int64 n)
    | :? int64 as n when n > 0L -> Some n
    | :? float as f when f > 0.0 && not (System.Double.IsNaN f) && not (System.Double.IsInfinity f) -> Some(int64 f)
    | _ -> None

let private lastAssistantTokenUsage (data: obj) : UsageObservation option =
    if isNullish data || not (isArray data) then
        None
    else
        let messages = unbox<obj array> data

        messages
        |> Array.rev
        |> Array.tryPick (fun item ->
            // SDK: SessionMessagesResponses item = { info: AssistantMessage, parts: Part[] }
            let info = get item "info"

            if isNullish info || str info "role" <> "assistant" then
                None
            else
                let tokens = get info "tokens"

                if isNullish tokens then
                    None
                else
                    let inputOpt = positiveInt64 (get tokens "input")
                    let cacheReadOpt = positiveInt64 (get (get tokens "cache") "read")

                    match inputOpt with
                    | None -> None
                    | Some input ->
                        let cacheRead = cacheReadOpt |> Option.defaultValue 0L

                        Some
                            { AssistantMessageID = str info "id"
                              InputTokens = input + cacheRead })

let tryObserveLatestUsage (client: obj) sessionID (directory: string) : JS.Promise<UsageObservation option> =
    promise {
        let session = get client "session"

        if isNullish session || isNullish (get session "messages") then
            return None
        else
            try
                let arg =
                    createObj
                        [ "path", box (createObj [ "id", box sessionID ])
                          "query", box (createObj [ "directory", box directory ]) ]

                let! response = unbox<JS.Promise<obj>> (session?messages arg)

                if isNullish response then
                    return None
                else
                    let data = get response "data"
                    return lastAssistantTokenUsage data
            with _ ->
                return None
    }
