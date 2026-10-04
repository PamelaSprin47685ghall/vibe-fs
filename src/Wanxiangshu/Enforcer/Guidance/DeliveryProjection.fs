namespace Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.Host

/// Restart-safe Main tip delivery history (WHAT guidance-delivery-001/002/003/005).
/// Folded only from HostFact.TipGuidanceDelivered — never a private file ledger.
type TipDeliveryProjectionState =
    {
        /// TipDeliveryFrontier: occurrences (RecentTip CycleId) that already
        /// received their first Full delivery in this Main session. Persistent
        /// and monotonic; reanchor never resets it.
        DeliveredOccurrences: Set<string>
        /// TipSemanticCoverage: TipNames whose full manual.md is recoverable in
        /// the current provider horizon. ContextReanchored clears it.
        CoveredTipNames: Set<string>
    }

module TipDeliveryProjection =

    let empty: TipDeliveryProjectionState =
        { DeliveredOccurrences = Set.empty
          CoveredTipNames = Set.empty }

    /// WHAT[003]: IdentityOnly only when this occurrence already has its first
    /// delivery AND the full text is still covered in the current horizon.
    let isIdentityOnly (occurrence: string) (tipName: string) (state: TipDeliveryProjectionState) : bool =
        (not (isNull occurrence))
        && occurrence.Trim().Length > 0
        && (not (isNull tipName))
        && Set.contains occurrence state.DeliveredOccurrences
        && Set.contains tipName state.CoveredTipNames

    /// Coverage query for the JS surface: is the full manual.md for this
    /// TipName recoverable in the current horizon?
    let hasFullDelivered (tipName: string) (state: TipDeliveryProjectionState) : bool =
        if isNull tipName then
            false
        else
            Set.contains tipName state.CoveredTipNames

    /// Absorb one TipGuidanceDelivered.
    ///
    /// Full records the occurrence frontier when the fact carries an occurrence
    /// identity and (re)covers the TipName; IdentityOnly is audit-only. Legacy
    /// facts without an occurrence identity can only restore coverage.
    let apply
        (tipName: string)
        (occurrence: string option)
        (presentation: TipPresentation)
        (state: TipDeliveryProjectionState)
        : TipDeliveryProjectionState =
        let normalizedTip =
            if isNull tipName || tipName.Trim().Length = 0 then
                None
            else
                Some(tipName.Trim())

        let normalizedOccurrence =
            match occurrence with
            | Some occ when not (isNull occ) && occ.Trim().Length > 0 -> Some(occ.Trim())
            | _ -> None

        let delivered =
            match normalizedOccurrence, presentation with
            | Some occ, TipPresentation.Full -> Set.add occ state.DeliveredOccurrences
            | _ -> state.DeliveredOccurrences

        match normalizedTip, presentation with
        | None, _ -> state
        | Some _, TipPresentation.IdentityOnly -> state
        | Some tip, TipPresentation.Full ->
            { DeliveredOccurrences = delivered
              CoveredTipNames = Set.add tip state.CoveredTipNames }

    /// WHAT[005]: ContextReanchored clears Coverage only; the occurrence
    /// frontier survives, so restoration adds no first-delivery fact.
    let applyReanchor (state: TipDeliveryProjectionState) : TipDeliveryProjectionState =
        { DeliveredOccurrences = state.DeliveredOccurrences
          CoveredTipNames = Set.empty }

    /// Explicit clear alias for callers that do not care about the reanchor name.
    let clear (state: TipDeliveryProjectionState) : TipDeliveryProjectionState = applyReanchor state
