module Wanxiangshu.Shell.OpencodeContextBudgetObservation

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ContextBudgetUsageCodec

[<Literal>]
let reserveTokens = 5000

let private positiveInt (value: obj) =
    match value with
    | :? int as number when number > 0 -> Some number
    | :? float as number when number > 0.0 -> Some(int number)
    | _ -> None

let private lastNativeUserModel (messages: Message<obj> list) =
    messages
    |> List.rev
    |> List.tryPick (fun message ->
        if message.source <> Native || message.info.role <> User || isNullish message.raw then
            None
        else
            let model = get message.raw "model"

            if isNullish model then
                None
            else
                let providerID = str model "providerID"

                let modelID =
                    let mId = str model "modelID"
                    if mId = "" then str model "id" else mId

                if providerID = "" || modelID = "" then
                    None
                else
                    Some(providerID, modelID))

let private modelLimitFromProvider (response: obj) (providerID: string) (modelID: string) =
    // SDK: client.provider.list({ query: { directory } })
    // Response: { data: { all: [{ id, models: { [id]: { id, limit: { context, output } } } }], default, connected } }
    if isNullish response then
        None
    else
        let payload = get response "data"

        if isNullish payload then
            None
        else
            let all = get payload "all"

            if isNullish all || not (isArray all) then
                None
            else
                unbox<obj array> all
                |> Array.tryFind (fun provider ->
                    let pId = str provider "id"
                    pId.ToLowerInvariant() = providerID.ToLowerInvariant())
                |> Option.bind (fun provider ->
                    let models = get provider "models"

                    if isNullish models then
                        None
                    else
                        let model = get models modelID

                        if isNullish model then
                            None
                        else
                            let limit = get model "limit"

                            if isNullish limit then
                                None
                            else
                                positiveInt (get limit "context"))

let tryEffectiveLimit (client: obj) directory messages : JS.Promise<int option> =
    promise {
        match lastNativeUserModel messages with
        | None -> return None
        | Some(providerID, modelID) ->
            let provider = get client "provider"

            if isNullish provider || isNullish (get provider "list") then
                printfn "DEBUG: provider list not available on client"
                return None
            else
                try
                    let args = createObj [ "query", box (createObj [ "directory", box directory ]) ]

                    let! response = unbox<JS.Promise<obj>> (provider?list args)

                    return
                        modelLimitFromProvider response providerID modelID
                        |> Option.filter (fun limit -> limit > reserveTokens)
                        |> Option.map (fun limit -> limit - reserveTokens)
                with ex ->
                    printfn "DEBUG: provider.list failed: %s" ex.Message
                    return None
    }

let private positiveInt64 (value: obj) : int64 option =
    match value with
    | :? int as n when n > 0 -> Some(int64 n)
    | :? int64 as n when n > 0L -> Some n
    | :? float as f when f > 0.0 && not (System.Double.IsNaN f) && not (System.Double.IsInfinity f) -> Some(int64 f)
    | _ -> None

let private lastAssistantTokenUsage (data: obj) : int option =
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
                        Some(int (input + cacheRead)))

let tryCurrentUsage (client: obj) sessionID (_encoded: obj array) : JS.Promise<int option> =
    promise {
        let session = get client "session"

        if isNullish session || isNullish (get session "messages") then
            printfn
                "DEBUG: session messages not available on client. session is null: %b, messages is null: %b"
                (isNullish session)
                (if isNullish session then
                     true
                 else
                     isNullish (get session "messages"))

            if not (isNullish session) then
                printfn "DEBUG: session keys: %A" (JS.Object.keys session)

            return None
        else
            try
                let arg =
                    createObj
                        [ "path", box (createObj [ "id", box sessionID ])
                          "query", box (createObj [ "directory", box "" ]) ]

                let! response = unbox<JS.Promise<obj>> (session?messages arg)

                if isNullish response then
                    return None
                else
                    let data = get response "data"
                    return lastAssistantTokenUsage data
            with ex ->
                printfn "DEBUG: session.messages failed: %s" ex.Message
                return None
    }
