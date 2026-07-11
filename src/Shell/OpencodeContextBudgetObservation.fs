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

let private providerLimit (response: obj) (providerID: string) (modelID: string) =
    if isNullish response then
        None
    else
        let all = get response "all"

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
                return None
            else
                try
                    let args = createObj [ "location", box (createObj [ "directory", box directory ]) ]
                    let! response = unbox<JS.Promise<obj>> (provider?list args)

                    return
                        providerLimit response providerID modelID
                        |> Option.filter (fun limit -> limit > reserveTokens)
                        |> Option.map (fun limit -> limit - reserveTokens)
                with _ ->
                    return None
    }

let private estimatedTokens usageTokens usageBytes currentBytes =
    if usageBytes <= 0 || currentBytes < 0 then
        None
    else
        let estimated = (float usageTokens * float currentBytes) / float usageBytes

        if
            System.Double.IsNaN estimated
            || System.Double.IsInfinity estimated
            || estimated <= 0.0
            || estimated > float System.Int32.MaxValue
        then
            None
        else
            Some(int estimated)

let tryCurrentUsage (client: obj) sessionID (encoded: obj array) : JS.Promise<int option> =
    promise {
        let session = get client "session"

        if isNullish session || isNullish (get session "context") then
            return None
        else
            try
                let! response = unbox<JS.Promise<obj>> (session?context (createObj [ "sessionID", box sessionID ]))
                let data = get response "data"

                if isNullish data || not (isArray data) then
                    return None
                else
                    let messages = unbox<obj array> data

                    let anchor =
                        messages
                        |> Array.mapi (fun index message -> index, message)
                        |> Array.rev
                        |> Array.tryPick (fun (index, message) ->
                            if str message "role" <> "assistant" then
                                None
                            else
                                positiveInt (get (get message "tokens") "input")
                                |> Option.map (fun tokens -> index, tokens))

                    match anchor with
                    | None -> return None
                    | Some(index, tokens) ->
                        let prefix = messages.[0..index]
                        return estimatedTokens tokens (utf8JsonBytes (box prefix)) (utf8JsonBytes (box encoded))
            with _ ->
                return None
    }
