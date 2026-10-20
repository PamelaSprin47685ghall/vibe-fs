namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Wanxiangshu.Repository.Programming.Js
open Fable.Core
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
module AnchorSurface =
    [<Import("encode", "gpt-tokenizer/encoding/o200k_base")>]
    let private encodeTokens (text: string) : obj = jsNative

    [<Emit("$0.length")>]
    let private tokenCount (tokens: obj) : int = jsNative

    [<Emit("$0 === undefined || $0 === null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("$0 === null")>]
    let private isNullValue (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'number' && Number.isInteger($0) && $0 >= 0 && $0 < 2147483647")>]
    let private isAnchor (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'object' && $0 !== null && !Array.isArray($0)")>]
    let private isRecord (value: obj) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private getProperty (value: obj) (name: string) : obj = jsNative

    [<Emit("Object.keys($0)")>]
    let private keys (value: obj) : string[] = jsNative

    let countTokens text = tokenCount (encodeTokens text)

    let private invalid detail =
        Error(JsFailure.ViewDeclarationInvalid detail)

    let private pairOf (source: obj) (key: string) =
        match System.Int32.TryParse key with
        | true, anchor when anchor >= 0 && anchor < System.Int32.MaxValue && string anchor = key ->
            let value = getProperty source key

            if isNullValue value then
                Ok(anchor, None)
            elif isString value && not (System.String.IsNullOrWhiteSpace(unbox<string> value)) then
                Ok(anchor, Some(unbox<string> value))
            else
                invalid (sprintf "thenMap[%s] must be null or a non-blank summary string" key)
        | _ -> invalid (sprintf "thenMap key '%s' must be a non-negative anchor number" key)

    let parseDeclaration (value: obj) : Result<AnchorViewDeclaration, JsFailure> =
        if not (isRecord value) then
            invalid "view must contain prefixUntil and thenMap"
        elif keys value |> Array.exists (fun key -> key <> "prefixUntil" && key <> "thenMap") then
            invalid "view accepts only prefixUntil and thenMap"
        else
            let prefix = getProperty value "prefixUntil"
            let mapping = getProperty value "thenMap"

            if not (isAnchor prefix) then
                invalid "prefixUntil is required and must be a non-negative integer anchor number"
            elif not (isRecord mapping) then
                invalid "thenMap is required and must map anchor numbers to summaries or null"
            else
                keys mapping
                |> Array.toList
                |> List.map (pairOf mapping)
                |> List.fold
                    (fun result pair ->
                        match result, pair with
                        | Ok pairs, Ok entry -> Ok(entry :: pairs)
                        | Error failure, _
                        | _, Error failure -> Error failure)
                    (Ok [])
                |> Result.map (fun pairs ->
                    { PrefixUntil = int (unbox<float> prefix)
                      ThenMap = pairs |> List.sortBy fst })

    let parseReportMapping (mapping: obj) : Result<AnchorViewDeclaration, JsFailure> =
        parseDeclaration (createObj [ "prefixUntil" ==> 0; "thenMap" ==> mapping ])

    let reportMaterial (sessionId: string) (mapping: obj) : Result<(int * string) list, JsFailure> =
        parseReportMapping mapping
        |> Result.map (fun declaration ->
            let numberOf = AnchorLedger.numberMessage sessionId

            let messages =
                AnchorLedger.context sessionId
                |> List.map (fun (id, text) ->
                    { AnchorView.AnchorMessage.Id = id
                      AnchorView.AnchorMessage.Text = text
                      AnchorView.AnchorMessage.Raw = null })

            let entries, _ =
                AnchorView.generate numberOf (fun _ -> 0) (AnchorLedger.replacements sessionId) declaration messages

            AnchorView.material numberOf entries)

    let private declarationValue (declaration: AnchorViewDeclaration) =
        createObj
            [ "prefixUntil" ==> declaration.PrefixUntil
              "thenMap"
              ==> (declaration.ThenMap
                   |> List.map (fun (anchor, summary) ->
                       string anchor, (summary |> Option.map box |> Option.defaultValue null))
                   |> createObj) ]

    let numberMessage sessionId messageId =
        AnchorLedger.numberMessage sessionId messageId

    let anchors sessionId =
        AnchorLedger.anchors sessionId
        |> List.map (fun (anchor, messageId) ->
            box
                {| anchor = anchor
                   messageId = messageId |})
        |> List.toArray

    let parse (declaration: obj) : Result<obj, JsFailure> =
        parseDeclaration declaration |> Result.map (fun _ -> declaration)

    let setView (sessionId: string) (declaration: obj) : Result<unit, JsFailure> =
        if isNullish declaration then
            Ok()
        else
            parseDeclaration declaration |> Result.map (AnchorLedger.setView sessionId)

    /// Plain JS receipt for declaration capture; typed Host callers use setView.
    let captureView (sessionId: string) (declaration: obj) : obj =
        match setView sessionId declaration with
        | Ok() -> createObj [ "ok" ==> true ]
        | Error failure ->
            createObj
                [ "ok" ==> false
                  "code" ==> JsFailure.code failure
                  "reason" ==> JsFailure.reason failure ]

    let takeView sessionId =
        AnchorLedger.takeView sessionId
        |> Option.map declarationValue
        |> Option.defaultValue null

    let applyView sessionId (messages: obj array) =
        let inputs: AnchorView.AnchorMessage list =
            if isNullish (box messages) then
                []
            else
                messages
                |> Array.toList
                |> List.map (fun raw ->
                    { AnchorView.AnchorMessage.Id =
                        getProperty raw "id" |> fun id -> if isString id then Some(string id) else None
                      AnchorView.AnchorMessage.Text =
                        getProperty raw "text" |> fun text -> if isString text then string text else ""
                      AnchorView.AnchorMessage.Raw = raw })

        AnchorLedger.remember sessionId (inputs |> List.map (fun message -> message.Id, message.Text))

        for message in inputs do
            message.Id
            |> Option.iter (fun id -> AnchorLedger.numberMessage sessionId id |> ignore)

        let declaration =
            AnchorLedger.takeView sessionId
            |> Option.defaultValue AnchorViewDeclaration.empty

        let entries, report =
            AnchorView.apply
                (AnchorLedger.numberMessage sessionId)
                countTokens
                (AnchorLedger.replacements sessionId)
                declaration
                inputs

        createObj
            [ "entries"
              ==> (entries
                   |> List.map (function
                       | AnchorView.Kept message -> createObj [ "kind" ==> "kept"; "message" ==> message.Raw ]
                       | AnchorView.Summarized(message, summary) ->
                           createObj [ "kind" ==> "summarized"; "message" ==> message.Raw; "summary" ==> summary ]
                       | AnchorView.OpeningSummary summary ->
                           createObj [ "kind" ==> "summarized"; "anchor" ==> 0; "summary" ==> summary ])
                   |> List.toArray)
              "report"
              ==> createObj
                      [ "total" ==> report.Total
                        "used" ==> report.Used
                        "cached" ==> report.Cached
                        "kept" ==> report.Kept
                        "summarized" ==> report.Summarized
                        "folded" ==> report.Folded
                        "replaced" ==> (report.Replaced |> List.toArray) ] ]

    let clearSession sessionId = AnchorLedger.clearSession sessionId
    let clearAll () = AnchorLedger.clearAll ()
