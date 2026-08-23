namespace Wanxiangshu.Participant.Provider.Attempt

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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

/// Pre-inference half of an AttemptPlan.
///
/// `experimental.chat.messages.transform` runs before the Host has created the
/// assistant message for the provider request, so ProviderRunIdentity cannot be
/// an input at this boundary. The remaining decision is nevertheless immutable:
/// authority/cursor/physical request identity/request kind/prefix choice are all
/// frozen here, then bound exactly once when a later Host observation supplies
/// the assistant run identity.
type PendingAttemptPlan =
    { Authority: PromptAuthority.AuthorityExecutionProfile
      Cursor: AgentPairCursor.FallbackCursor
      PhysicalUserMessageId: PhysicalUserMessageId
      Origin: PromptAuthority.PromptOrigin
      RequestKind: ProviderRequestKind
      ProjectionChoice: XProjectionChoice
      NoProbeReason: NoCandidateReason option }

[<RequireQualifiedAccess>]
module AttemptPlanner =

    let private chooseProjection
        (requestKind: ProviderRequestKind)
        (opportunity: RecoveryOpportunity)
        (selectProbe: unit -> Result<PrefixProbe, NoCandidateReason>)
        =
        let probe =
            match opportunity, ProviderRequestKind.mayCarryProbe requestKind with
            | RecoveryOpportunity.RecoveryAttempt, true -> Some(selectProbe ())
            | RecoveryOpportunity.OrdinaryAttempt, _
            | RecoveryOpportunity.RecoveryAttempt, false -> None

        match probe with
        | Some(Ok value) -> XProjectionChoice.UsePrefixProbe value, None
        | Some(Error reason) -> XProjectionChoice.UseCommittedEpoch, Some reason
        | None -> XProjectionChoice.UseCommittedEpoch, None

    /// Freeze every provider-request decision available before inference. The
    /// assistant run is deliberately absent: the Host has not created it yet.
    let freezePreInference
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (cursor: AgentPairCursor.FallbackCursor)
        (physicalUserMessageId: PhysicalUserMessageId)
        (origin: PromptAuthority.PromptOrigin)
        (requestKind: ProviderRequestKind)
        (opportunity: RecoveryOpportunity)
        (selectProbe: unit -> Result<PrefixProbe, NoCandidateReason>)
        : PendingAttemptPlan =
        let choice, noProbeReason = chooseProjection requestKind opportunity selectProbe

        { Authority = authority
          Cursor = cursor
          PhysicalUserMessageId = physicalUserMessageId
          Origin = origin
          RequestKind = requestKind
          ProjectionChoice = choice
          NoProbeReason = noProbeReason }

    /// Complete the immutable attempt profile once Host observation exposes the
    /// exact assistant run for the already-frozen physical request.
    let bindProviderRun (providerRun: ProviderRunIdentity) (pending: PendingAttemptPlan) : AttemptPlan =
        { Profile =
            PromptAuthority.buildAttemptExecutionProfile
                pending.Authority
                pending.Cursor
                pending.PhysicalUserMessageId
                providerRun
                pending.Origin
                pending.RequestKind
                pending.ProjectionChoice
          NoProbeReason = pending.NoProbeReason }

    let pendingProbeOf (pending: PendingAttemptPlan) =
        match pending.ProjectionChoice with
        | XProjectionChoice.UsePrefixProbe probe -> Some probe
        | XProjectionChoice.UseCommittedEpoch -> None

    /// PROMPT-008: build the profile for one attempt.
    ///
    /// `opportunity` says only whether the attempt is the primed slot reached by a
    /// real failure. Material is not a second boolean. WorkMain proves material by
    /// running `selectProbe`; `Error NoCoverage` is therefore an explicit ordinary
    /// no-probe result rather than an unreachable branch.
    let plan
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (cursor: AgentPairCursor.FallbackCursor)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        (origin: PromptAuthority.PromptOrigin)
        (requestKind: ProviderRequestKind)
        (opportunity: RecoveryOpportunity)
        (selectProbe: unit -> Result<PrefixProbe, NoCandidateReason>)
        : AttemptPlan =
        freezePreInference authority cursor physicalUserMessageId origin requestKind opportunity selectProbe
        |> bindProviderRun providerRun

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
