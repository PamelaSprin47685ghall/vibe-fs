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

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("structuredClone($0)")>]
    let private copy (value: obj) : obj = jsNative

    let countTokens text = tokenCount (encodeTokens text)

    let private failureReceipt (failure: JsFailure) : obj =
        createObj
            [ "ok" ==> false
              "code" ==> JsFailure.code failure
              "reason" ==> JsFailure.reason failure ]

    let private declarationValue (declaration: AnchorViewDeclaration) : obj =
        createObj
            [ "prefixCount" ==> declaration.PrefixCount
              "carry" ==> AnchorCarry.toValue declaration.Carry ]

    let parseDeclaration (value: obj) : obj =
        match AnchorDeclarationRule.parse value with
        | Ok declaration ->
            createObj
                [ "ok" ==> true
                  "prefixCount" ==> declaration.PrefixCount
                  "carry" ==> AnchorCarry.toValue declaration.Carry ]
        | Error failure -> failureReceipt failure

    let parseCarry (value: obj) : obj =
        match AnchorDeclarationRule.parseCarry value with
        | Ok carry -> createObj [ "ok" ==> true; "carry" ==> AnchorCarry.toValue carry ]
        | Error failure -> failureReceipt failure

    let carryMaterial (sessionId: string) (value: obj) : obj =
        let result =
            AnchorDeclarationRule.parseCarry value
            |> Result.bind (AnchorHistory.select (AnchorLedger.visible sessionId))

        match result with
        | Error failure -> failureReceipt failure
        | Ok material ->
            createObj
                [ "ok" ==> true
                  "material"
                  ==> (material
                       |> List.map (fun (anchor, text) ->
                           createObj
                               [ "anchor" ==> (anchor |> Option.map box |> Option.defaultValue null)
                                 "text" ==> text ])
                       |> List.toArray) ]

    let numberMessage sessionId messageId =
        AnchorLedger.numberMessage sessionId messageId

    let anchors sessionId =
        AnchorLedger.anchors sessionId
        |> List.mapi (fun index (anchor, messageId) ->
            createObj
                [ "anchor" ==> anchor
                  "messageId" ==> messageId
                  "prefixCount" ==> (index + 1) ])
        |> List.toArray

    let parse (declaration: obj) : Result<obj, JsFailure> =
        AnchorDeclarationRule.parse declaration |> Result.map declarationValue

    let validateView (sessionId: string) (declaration: obj) =
        if isNullish declaration then
            Ok()
        else
            AnchorDeclarationRule.parse declaration
            |> Result.bind (AnchorLedger.validate sessionId)

    let setView (sessionId: string) (callIdentity: (string * string) option) (declaration: obj) =
        if isNullish declaration then
            Ok()
        else
            AnchorDeclarationRule.parse declaration
            |> Result.bind (AnchorLedger.setView sessionId callIdentity)

    let captureView (sessionId: string) (declaration: obj) =
        match setView sessionId None declaration with
        | Ok() -> createObj [ "ok" ==> true ]
        | Error failure -> failureReceipt failure

    let takeView sessionId =
        AnchorLedger.takeView sessionId
        |> Option.map declarationValue
        |> Option.defaultValue null

    let applyView sessionId (messages: obj array) =
        let inputs: AnchorMessage list =
            if isNullish (box messages) then
                []
            else
                messages
                |> Array.toList
                |> List.map (fun raw ->
                    { Id = if isString raw?id then Some(unbox<string> raw?id) else None
                      Text = if isString raw?text then unbox<string> raw?text else ""
                      Raw = copy raw })

        let suffix =
            match AnchorLedger.checkpointSource sessionId with
            | None -> inputs
            | Some previous -> inputs |> List.skip (min previous.Length inputs.Length)

        let segments = AnchorLedger.project sessionId suffix
        AnchorLedger.remember sessionId inputs segments
        AnchorLedger.takeView sessionId |> ignore

        let report =
            AnchorView.report countTokens inputs (AnchorLedger.protectedPrefix sessionId) segments

        createObj
            [ "entries"
              ==> (segments
                   |> List.collect (fun segment ->
                       segment.Messages
                       |> List.map (fun message ->
                           if isNull message.Raw then
                               createObj
                                   [ "kind" ==> "literal"; "anchor" ==> segment.Anchor; "text" ==> message.Text ]
                           else
                               createObj
                                   [ "kind" ==> "kept"
                                     "anchor" ==> segment.Anchor
                                     "message" ==> copy message.Raw ]))
                   |> List.toArray)
              "report"
              ==> createObj
                      [ "total" ==> report.Total
                        "used" ==> report.Used
                        "cached" ==> report.Cached
                        "kept" ==> report.Kept
                        "inserted" ==> report.Inserted
                        "dropped" ==> report.Dropped
                        "prefixCount" ==> report.PrefixCount ] ]

    let clearSession sessionId = AnchorLedger.clearSession sessionId
    let clearAll () = AnchorLedger.clearAll ()
