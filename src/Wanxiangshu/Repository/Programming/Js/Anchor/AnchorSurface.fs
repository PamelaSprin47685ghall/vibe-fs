namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Fable.Core
open Fable.Core.JsInterop

/// JS-native ANCHOR boundary for semantic tests and the transform pipeline.
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

    [<Emit("typeof $0 === 'number'")>]
    let private isNumber (value: obj) : bool = jsNative

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private getProperty (value: obj) (name: string) : obj = jsNative

    [<Emit("$0[$1]")>]
    let private getIndex (value: obj) (index: int) : obj = jsNative

    [<Emit("$0.length")>]
    let private length (value: obj) : int = jsNative

    let countTokens (text: string) : int = tokenCount (encodeTokens text)

    let private declarationOf (value: obj) : AnchorViewDeclaration option =
        if isNullish value then
            None
        else
            let prefixUntil =
                let raw = getProperty value "prefixUntil"

                if isNumber raw then Some(int (unbox<float> raw)) else None

            let extra =
                let raw = getProperty value "extra"

                if isArray raw then
                    [ 0 .. length raw - 1 ]
                    |> List.choose (fun index ->
                        let item = getIndex raw index

                        if isString item then
                            Some(AnchorExtra.Prose(string item))
                        elif isNumber item then
                            Some(AnchorExtra.Anchor(int (unbox<float> item)))
                        else
                            None)
                else
                    []

            if prefixUntil.IsNone && List.isEmpty extra then
                None
            else
                Some
                    { PrefixUntil = prefixUntil
                      Extra = extra }

    let private declarationValue (declaration: AnchorViewDeclaration) : obj =
        let extra =
            declaration.Extra
            |> List.map (function
                | AnchorExtra.Anchor anchor -> box anchor
                | AnchorExtra.Prose text -> box text)
            |> Array.ofList

        createObj
            [ "prefixUntil"
              ==> (match declaration.PrefixUntil with
                   | Some value -> box value
                   | None -> null)
              "extra" ==> extra ]

    let numberMessage (sessionId: string) (messageId: string) : int =
        AnchorLedger.numberMessage sessionId messageId

    let anchors (sessionId: string) : obj array =
        AnchorLedger.anchors sessionId
        |> List.map (fun (anchor, messageId) ->
            box
                {| anchor = anchor
                   messageId = messageId |})
        |> Array.ofList

    let setView (sessionId: string) (declaration: obj) : unit =
        declarationOf declaration |> Option.iter (AnchorLedger.setView sessionId)

    let takeView (sessionId: string) : obj =
        match AnchorLedger.takeView sessionId with
        | Some declaration -> declarationValue declaration
        | None -> null

    let applyView (sessionId: string) (messages: obj array) : obj =
        let declaration =
            AnchorLedger.takeView sessionId
            |> Option.defaultValue { PrefixUntil = None; Extra = [] }

        let inputs =
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

        let survivors, report =
            AnchorView.apply (AnchorLedger.numberMessage sessionId) countTokens declaration inputs

        createObj
            [ "messages"
              ==> (survivors |> List.map (fun message -> message.Raw) |> Array.ofList)
              "prose" ==> (report.ExtraProse |> Array.ofList)
              "report"
              ==> createObj
                      [ "total" ==> report.Total
                        "used" ==> report.Used
                        "cached" ==> report.Cached
                        "kept" ==> report.Kept
                        "dropped" ==> report.Dropped ] ]

    let clearSession (sessionId: string) : unit = AnchorLedger.clearSession sessionId

    let clearAll () : unit = AnchorLedger.clearAll ()
