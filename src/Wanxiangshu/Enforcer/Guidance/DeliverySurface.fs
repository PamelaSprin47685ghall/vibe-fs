namespace Wanxiangshu.Enforcer.Guidance

open Fable.Core.JsInterop

/// JS-native owner boundary for the Main tip delivery projection. The durable
/// fold keeps its typed frontier/coverage sets and TipPresentation private;
/// callers see only stable strings and arrays.
[<RequireQualifiedAccess>]
module DeliverySurface =

    let private isNullish (value: obj) : bool =
        isNull value || emitJsExpr value "$0 === undefined"

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private stateToJs (state: TipDeliveryProjectionState) : obj =
        box
            {| deliveredOccurrences = state.DeliveredOccurrences |> Set.toArray
               coveredTipNames = state.CoveredTipNames |> Set.toArray |}

    let private stateOfJs (value: obj) : TipDeliveryProjectionState =
        let occurrences =
            if isNullish value?deliveredOccurrences then
                [||]
            else
                unbox<string array> value?deliveredOccurrences

        let names =
            if isNullish value?coveredTipNames then
                [||]
            else
                unbox<string array> value?coveredTipNames

        { DeliveredOccurrences = occurrences |> Array.toList |> Set.ofList
          CoveredTipNames = names |> Array.toList |> Set.ofList }

    let private presentationOf (value: obj) : Wanxiangshu.Host.TipPresentation =
        match text value with
        | "IdentityOnly" -> Wanxiangshu.Host.TipPresentation.IdentityOnly
        | _ -> Wanxiangshu.Host.TipPresentation.Full

    let empty: obj = stateToJs TipDeliveryProjection.empty

    let hasFullDelivered (tipName: string) (state: obj) : bool =
        TipDeliveryProjection.hasFullDelivered tipName (stateOfJs state)

    /// `occurrence` is optional on the JS side: omitting it folds the delivery
    /// without an occurrence identity (coverage only, no frontier advance).
    let apply (tipName: string) (presentation: obj) (state: obj) (occurrence: obj) : obj =
        TipDeliveryProjection.apply tipName (optionalText occurrence) (presentationOf presentation) (stateOfJs state)
        |> stateToJs

    let applyReanchor (state: obj) : obj =
        TipDeliveryProjection.applyReanchor (stateOfJs state) |> stateToJs

    let clear (state: obj) : obj = applyReanchor state
