namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel.Identity

/// PROMPT-008 / CTX-006 / CTX-010: everything one provider request needs, decided once.
///
/// This is the single call site of `buildAttemptExecutionProfile`. Before it existed
/// the constructor had none at all — every send path read `ActiveLogicalRun` and
/// assembled its own fields, which is exactly what PROMPT-008 forbids, and the
/// `single-constructor` gate could not see it because a function nobody calls has
/// nothing bypassing it.
///
/// The plan bundles the profile with the prefix decision because the two are one
/// choice: CTX-010 makes the probe part of the immutable profile, so a caller that
/// received them separately could send a request whose profile says
/// `UsePrefixProbe` while its message list carries the committed prefix.
type AttemptPlan =
    {
        Profile: PromptAuthority.AttemptExecutionProfile
        /// `None` when this slot built no probe. CTX-011 lists five ordinary reasons for
        /// that, and the caller treats them alike — it is kept so a diagnostic can say
        /// which one happened (HOST-007).
        NoProbeReason: NoCandidateReason option
    }

[<RequireQualifiedAccess>]
module AttemptPlanner =

    /// PROMPT-008: build the profile for one attempt.
    ///
    /// `armingDecision` is the CTX-006 answer computed by `RecoverySlot.mayRecover` —
    /// passed in rather than recomputed, because arming is a control-flow fact of the
    /// caller's recovery sequence (FALLBACK-012) and this module has no way to observe
    /// it. Handing it a cursor and letting it decide would reintroduce the
    /// parked-cursor bug.
    ///
    /// `selectProbe` is deferred: it only runs when the slot may actually recover, so a
    /// non-recovery request never pays for a digest recomputation or a blob read.
    let plan
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (cursor: AgentPairCursor.FallbackCursor)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        (origin: PromptAuthority.PromptOrigin)
        (requestKind: ProviderRequestKind)
        (mayRecover: bool)
        (selectProbe: unit -> Result<PrefixProbe, NoCandidateReason>)
        : AttemptPlan =
        // CTX-010: only a work main request substitutes a prefix. Asking for the probe
        // at all is gated on that here, so a Companion request cannot spend a digest
        // recomputation deciding something it may not do.
        let probe =
            if mayRecover && ProviderRequestKind.mayCarryProbe requestKind then
                Some(selectProbe ())
            else
                None

        let choice, noProbeReason =
            match probe with
            | Some(Ok value) -> XProjectionChoice.UsePrefixProbe value, None
            | Some(Error reason) -> XProjectionChoice.UseCommittedEpoch, Some reason
            | None -> XProjectionChoice.UseCommittedEpoch, None

        { Profile =
            PromptAuthority.buildAttemptExecutionProfile
                authority
                cursor
                physicalUserMessageId
                providerRun
                origin
                requestKind
                choice
          NoProbeReason = noProbeReason }

    /// CTX-010: the probe this attempt carries, if any.
    let probeOf (plan: AttemptPlan) =
        match plan.Profile.ProjectionChoice with
        | XProjectionChoice.UsePrefixProbe probe -> Some probe
        | XProjectionChoice.UseCommittedEpoch -> None

    /// CTX-012: may this attempt's outcome promote a prefix.
    ///
    /// Two conditions. The attempt must carry a probe, and the terminal must be usable
    /// (CTX-004). Everything else CTX-012 lists as non-promotable — a transport
    /// receipt, `PhysicalAccepted`, the provider starting to stream — is not an
    /// `AttemptOutcome` at all, so it cannot reach this function: those are states of
    /// the send, and only a reconciled snapshot produces an outcome.
    let promotableProbe (plan: AttemptPlan) (outcome: AttemptOutcome) =
        match outcome with
        | AttemptOutcome.Completed -> probeOf plan
        | AttemptOutcome.CompletedInvalid
        | AttemptOutcome.Failed
        | AttemptOutcome.Aborted -> None
