namespace Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.Host

type TipDeliveryProjectionState =
    { DeliveredOccurrences: Set<string>
      CoveredTipNames: Set<string> }

module TipDeliveryProjection =
    val empty: TipDeliveryProjectionState

    val isIdentityOnly: occurrence: string -> tipName: string -> state: TipDeliveryProjectionState -> bool
    val hasFullDelivered: tipName: string -> state: TipDeliveryProjectionState -> bool

    val apply:
        tipName: string ->
        occurrence: string option ->
        presentation: TipPresentation ->
        state: TipDeliveryProjectionState ->
            TipDeliveryProjectionState

    val applyReanchor: TipDeliveryProjectionState -> TipDeliveryProjectionState
    val clear: TipDeliveryProjectionState -> TipDeliveryProjectionState
