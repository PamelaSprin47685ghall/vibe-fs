namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

module MessageOriginDecoder =

    let isSyntheticPart (partObj: obj) : bool =
        if isNull partObj then
            false
        else
            let synthetic = partObj?synthetic
            not (isNull synthetic) && unbox<bool> synthetic

    let isCompactionPart (partObj: obj) : bool =
        if isNull partObj then
            false
        else
            let pType = partObj?``type``
            not (isNull pType) && unbox<string> pType = "compaction"

    let tryExtractPromptKey (partObj: obj) : PromptKeyRef option =
        if isNull partObj then
            None
        else
            let meta = partObj?metadata

            if isNull meta then
                None
            else
                let keyStr = meta?wanxiangshu_prompt_key

                if not (isNull keyStr) then
                    Some(PromptKeyRef.create (unbox<string> keyStr))
                else
                    let keyStr2 = meta?promptKey

                    if not (isNull keyStr2) then
                        Some(PromptKeyRef.create (unbox<string> keyStr2))
                    else
                        None

    let decodeUserMessageOrigin (userMsg: OpencodeUserMessage) : MessageOrigin =
        let parts = userMsg.parts
        let hasCompaction = parts |> List.exists isCompactionPart
        let pluginKeyOpt = parts |> List.tryPick tryExtractPromptKey

        match pluginKeyOpt with
        | Some keyRef -> PluginGenerated keyRef
        | None when hasCompaction -> HostInternal
        | None ->
            let allSynthetic = parts.Length > 0 && (parts |> List.forall isSyntheticPart)

            if allSynthetic then
                HostInternal
            else
                Human(TurnId.create userMsg.id)

    let isCompactionAssistant (msg: OpencodeAssistantMessage) : bool =
        match msg.summary with
        | Some true -> true
        | _ ->
            match msg.agent with
            | Some "compaction" -> true
            | _ -> false

    let isAbortedError (errorObj: obj option) : bool =
        match errorObj with
        | None -> false
        | Some err ->
            if isNull err then
                false
            else
                let name = err?name

                not (isNull name)
                && (unbox<string> name = "MessageAbortedError" || unbox<string> name = "AbortError")

    [<Emit("typeof $0 === 'string' ? $0 : null")>]
    let jsString (value: obj) : string = jsNative

    let asString value =
        if isNull value then
            None
        else
            let text = jsString value
            if isNull text then None else Some text

    let firstString values = values |> List.tryPick asString

    let asObjects (value: obj) =
        if isNull value then
            []
        else
            try
                unbox<obj array> value |> Array.toList
            with _ ->
                try
                    unbox<obj list> value
                with _ ->
                    []

    let partText (part: obj) =
        if isNull part then
            None
        else
            let partType = asString part?``type``

            if partType |> Option.exists (fun value -> value <> "text") then
                None
            else
                asString part?text
                |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))

    let textFromParts (value: obj) =
        let text = asObjects value |> List.choose partText |> String.concat ""
        if String.IsNullOrWhiteSpace text then None else Some text

    let assistantText (properties: obj) (eventObj: obj) (eventType: string) =
        let message: obj = properties?message

        let role =
            (if not (isNull message) then asString message?role else None)
            |> Option.orElse (asString properties?role)
            |> Option.map (fun value -> value.ToLowerInvariant())

        let isAssistant =
            role = Some "assistant"
            || (role.IsNone && eventType.Contains("assistant"))
            || (role.IsNone
                && eventType.StartsWith("message.part")
                && not (isNull properties?part))

        if not isAssistant then
            None
        else
            match
                if not (isNull message) then
                    textFromParts message?parts
                else
                    None
            with
            | Some text -> Some text
            | None ->
                match textFromParts properties?parts with
                | Some text -> Some text
                | None ->
                    match asString properties?text with
                    | Some text when not (String.IsNullOrWhiteSpace text) -> Some text
                    | _ ->
                        match asString properties?delta with
                        | Some text when not (String.IsNullOrWhiteSpace text) -> Some text
                        | _ ->
                            if not (isNull properties?part) then
                                asString properties?part?text
                                |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))
                            else
                                None

    let sessionIdOf properties eventObj =
        [ properties?sessionID
          properties?sessionId
          eventObj?sessionID
          eventObj?sessionId ]
        |> firstString
        |> Option.map SessionId.create

    let messageIdOf properties eventObj =
        let propertyMessage = properties?message
        let propertyInfo = properties?info
        let eventMessage = eventObj?message

        [ properties?messageID
          properties?messageId
          if not (isNull propertyMessage) then
              propertyMessage?id
          else
              null
          if not (isNull propertyInfo) then propertyInfo?id else null
          eventObj?messageID
          eventObj?messageId
          if not (isNull eventMessage) then eventMessage?id else null ]
        |> firstString
        |> Option.map MessageId.create

    let errorText properties eventObj =
        let propertyError = properties?error
        let eventError = eventObj?error

        [ if not (isNull propertyError) then
              propertyError?name
          else
              null
          if not (isNull propertyError) then
              propertyError?message
          else
              null
          propertyError
          if not (isNull eventError) then eventError?name else null
          if not (isNull eventError) then eventError?message else null ]
        |> firstString
