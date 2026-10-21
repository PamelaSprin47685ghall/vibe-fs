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

    [<Emit("$0[$1]")>]
    let private getProperty (value: obj) (name: string) : obj = jsNative

    let countTokens text = tokenCount (encodeTokens text)

    /// The plain-JS failure receipt: the same shape `captureView` already
    /// returns, so every JS caller reads one vocabulary.
    let private failureReceipt (failure: JsFailure) : obj =
        createObj
            [ "ok" ==> false
              "code" ==> JsFailure.code failure
              "reason" ==> JsFailure.reason failure ]

    /// The declaration as plain data: the typed declaration stays inside this
    /// module, so no JS caller can read a union to learn whether the shape was
    /// legal.
    let private declarationValue (declaration: AnchorViewDeclaration) : obj =
        createObj
            [ "prefixUntil" ==> declaration.PrefixUntil
              "thenMap"
              ==> (declaration.ThenMap
                   |> List.map (fun (anchor, summary) ->
                       string anchor, (summary |> Option.map box |> Option.defaultValue null))
                   |> createObj) ]

    let private declarationReceipt (declaration: AnchorViewDeclaration) : obj =
        let value = declarationValue declaration
        let prefixUntil: int = value?prefixUntil

        createObj
            [ "ok" ==> true
              "prefixUntil" ==> prefixUntil
              "thenMap" ==> value?thenMap ]

    /// The plain-JS receipt of one typed decision.
    let private receiptOf (decision: Result<AnchorViewDeclaration, JsFailure>) : obj =
        match decision with
        | Ok declaration -> declarationReceipt declaration
        | Error failure -> failureReceipt failure

    /// The declaration shape rule, answered as plain JS. Shape rules belong to
    /// wanxiangli/compression; the host maps the first violation onto its
    /// failure code — the very same rule `AnchorDeclarationRule` gives F#
    /// callers.
    ///
    /// The public receipt is plain JS: `{ ok: true, prefixUntil, thenMap }` or
    /// `{ ok: false, code, reason }`.
    let parseDeclaration (value: obj) : obj =
        AnchorDeclarationRule.parse value |> receiptOf

    /// A report mapping is exactly a declaration with `prefixUntil = 0`, so it
    /// is judged by the same rule and answers with the same receipt shape.
    let parseReportMapping (mapping: obj) : obj =
        AnchorDeclarationRule.parseReportMapping mapping |> receiptOf

    /// The read-only projection of the caller's visible interval under a report
    /// mapping. The receipt is plain JS: `{ ok: true, material }` (an array of
    /// `{ anchor, text }`) or `{ ok: false, code, reason }`.
    let reportMaterial (sessionId: string) (mapping: obj) : obj =
        match AnchorDeclarationRule.parseReportMapping mapping with
        | Error failure -> failureReceipt failure
        | Ok declaration ->
            let numberOf = AnchorLedger.numberMessage sessionId

            let messages =
                AnchorLedger.context sessionId
                |> List.map (fun (id, text) ->
                    { AnchorView.AnchorMessage.Id = id
                      AnchorView.AnchorMessage.Text = text
                      AnchorView.AnchorMessage.Raw = null })

            let entries, _ =
                AnchorView.generate numberOf (fun _ -> 0) (AnchorLedger.replacements sessionId) declaration messages

            createObj
                [ "ok" ==> true
                  "material"
                  ==> (AnchorView.material numberOf entries
                      |> List.map (fun (anchor, text) ->
                          createObj [ "anchor" ==> anchor; "text" ==> text ])
                      |> List.toArray) ]

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
        AnchorDeclarationRule.parse declaration |> Result.map (fun _ -> declaration)

    let setView (sessionId: string) (declaration: obj) : Result<unit, JsFailure> =
        if isNullish declaration then
            Ok()
        else
            AnchorDeclarationRule.parse declaration |> Result.map (AnchorLedger.setView sessionId)

    /// Plain JS receipt for declaration capture; typed Host callers use setView.
    let captureView (sessionId: string) (declaration: obj) : obj =
        match setView sessionId declaration with
        | Ok() -> createObj [ "ok" ==> true ]
        | Error failure -> failureReceipt failure

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
