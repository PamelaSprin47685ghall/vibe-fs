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
                    let cacheWriteOpt = positiveInt64 (get (get tokens "cache") "write")

                    match inputOpt with
                    | None -> None
                    | Some input ->
                        let cacheRead = cacheReadOpt |> Option.defaultValue 0L
                        let cacheWrite = cacheWriteOpt |> Option.defaultValue 0L
                        let totalInput = input + cacheRead + cacheWrite

                        // Skip zero-token assistants (current in-progress step)
                        if totalInput <= 0L then
                            None
                        else
                            // Skip compaction summary assistants
                            let isCompaction =
                                let agent = str info "agent"
                                let details = get info "details"

                                let summaryFlag =
                                    if isNullish details then
                                        false
                                    else
                                        let s = get details "summary"
                                        not (isNullish s) && (s :?> bool)

                                agent = "compaction" || summaryFlag

                            if isCompaction then
                                None
                            else
                                Some
                                    { AssistantMessageID = str info "id"
                                      InputTokens = totalInput })

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
