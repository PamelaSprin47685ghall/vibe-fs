namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel.Fact

/// Restart-safe Main tip Full/Identity delivery history (Rulebook §14–16).
/// Folded only from HostFact.TipGuidanceDelivered — never a private file ledger.
type TipDeliveryProjectionState =
    { /// TipNames that have received Full main.md in this Main session.
      FullDeliveredTips: Set<string> }

module TipDeliveryProjection =

    let empty: TipDeliveryProjectionState = { FullDeliveredTips = Set.empty }

    let hasFullDelivered (tipName: string) (state: TipDeliveryProjectionState) : bool =
        if isNull tipName then
            false
        else
            Set.contains tipName state.FullDeliveredTips

    /// Absorb one TipGuidanceDelivered. Full adds the tip; IdentityOnly is audit-only.
    let apply
        (tipName: string)
        (presentation: TipPresentation)
        (state: TipDeliveryProjectionState)
        : TipDeliveryProjectionState =
        if isNull tipName || tipName.Trim().Length = 0 then
            state
        else
            match presentation with
            | TipPresentation.Full ->
                { FullDeliveredTips = Set.add (tipName.Trim()) state.FullDeliveredTips }
            | TipPresentation.IdentityOnly -> state
