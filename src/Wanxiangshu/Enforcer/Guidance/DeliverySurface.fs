namespace Wanxiangshu.Enforcer.Guidance

open Fable.Core.JsInterop
open Wanxiangshu.OpenCode.Host

/// JS-native owner boundary for the Main tip Full/Identity projection. The
/// durable fold keeps its typed set and TipPresentation private; callers see
/// only stable strings and arrays.
[<RequireQualifiedAccess>]
module DeliverySurface =

    let private isNullish (value: obj) : bool =
        isNull value || emitJsExpr value "$0 === undefined"

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private namesOf (state: TipDeliveryProjectionState) : string array =
        state.FullDeliveredTips |> Set.toArray

    let private stateToJs (state: TipDeliveryProjectionState) : obj =
        box {| fullDeliveredTips = namesOf state |}

    let private stateOfJs (value: obj) : TipDeliveryProjectionState =
        let names =
            if isNullish value?fullDeliveredTips then
                [||]
            else
                unbox<string array> value?fullDeliveredTips

        { FullDeliveredTips = names |> Array.toList |> Set.ofList }

    let private presentationOf (value: obj) : TipPresentation =
        match text value with
        | "IdentityOnly" -> TipPresentation.IdentityOnly
        | _ -> TipPresentation.Full

    let empty : obj = stateToJs TipDeliveryProjection.empty

    let hasFullDelivered (tipName: string) (state: obj) : bool =
        TipDeliveryProjection.hasFullDelivered tipName (stateOfJs state)

    let apply (tipName: string) (presentation: obj) (state: obj) : obj =
        TipDeliveryProjection.apply tipName (presentationOf presentation) (stateOfJs state)
        |> stateToJs

    let applyReanchor (state: obj) : obj =
        TipDeliveryProjection.applyReanchor (stateOfJs state) |> stateToJs

    let clear (state: obj) : obj = applyReanchor state
