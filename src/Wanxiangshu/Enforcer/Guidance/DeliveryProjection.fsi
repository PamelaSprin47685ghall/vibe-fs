namespace Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.Host

type TipDeliveryProjectionState = { FullDeliveredTips: Set<string> }

module TipDeliveryProjection =
    val empty: TipDeliveryProjectionState
    val hasFullDelivered: tipName: string -> state: TipDeliveryProjectionState -> bool

    val apply:
        tipName: string ->
        presentation: TipPresentation ->
        state: TipDeliveryProjectionState ->
            TipDeliveryProjectionState

    val applyReanchor: TipDeliveryProjectionState -> TipDeliveryProjectionState
    val clear: TipDeliveryProjectionState -> TipDeliveryProjectionState
