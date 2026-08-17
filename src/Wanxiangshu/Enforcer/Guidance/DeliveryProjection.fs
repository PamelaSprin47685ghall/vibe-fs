namespace Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Composition.Durable.Fact

/// Restart-safe Main tip Full/Identity delivery history (Rulebook §14–16).
/// Folded only from HostFact.TipGuidanceDelivered — never a private file ledger.
type TipDeliveryProjectionState =
    {
        /// TipNames that have received Full main.md in this Main session.
        FullDeliveredTips: Set<string>
    }

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
        let normalizedTip =
            if isNull tipName || tipName.Trim().Length = 0 then
                None
            else
                Some(tipName.Trim())

        match normalizedTip, presentation with
        | None, _ -> state
        | Some tip, TipPresentation.Full -> { FullDeliveredTips = Set.add tip state.FullDeliveredTips }
        | Some _, TipPresentation.IdentityOnly -> state

    /// HOST-006: ContextReanchored voids Full history so Main re-emits main.md after compaction.
    /// Identity-only must not strand the post-reanchor transcript (FullDeliveredTips → empty).
    let applyReanchor (_state: TipDeliveryProjectionState) : TipDeliveryProjectionState = empty

    /// Explicit clear alias for callers that do not care about the reanchor name.
    let clear (state: TipDeliveryProjectionState) : TipDeliveryProjectionState = applyReanchor state
