namespace Wanxiangshu.Participant.Provider.Attempt

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

type AttemptPlan =
    { Profile: PromptAuthority.AttemptExecutionProfile
      NoProbeReason: NoCandidateReason option }

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
    val ordinaryRequestKind: origin: PromptAuthority.PromptOrigin -> ProviderRequestKind

    val freezePreInference:
        authority: PromptAuthority.AuthorityExecutionProfile ->
        cursor: AgentPairCursor.FallbackCursor ->
        physicalUserMessageId: PhysicalUserMessageId ->
        origin: PromptAuthority.PromptOrigin ->
        requestKind: ProviderRequestKind ->
        opportunity: RecoveryOpportunity ->
        selectProbe: (unit -> Result<PrefixProbe, NoCandidateReason>) ->
            PendingAttemptPlan

    val freezeOrdinary:
        accepted: AcceptedChatExecutionEvidence ->
        requestKind: ProviderRequestKind ->
            Result<PendingAttemptPlan, string>

    val bindProviderRun: providerRun: ProviderRunIdentity -> pending: PendingAttemptPlan -> AttemptPlan
    val pendingProbeOf: pending: PendingAttemptPlan -> PrefixProbe option

    val plan:
        authority: PromptAuthority.AuthorityExecutionProfile ->
        cursor: AgentPairCursor.FallbackCursor ->
        physicalUserMessageId: PhysicalUserMessageId ->
        providerRun: ProviderRunIdentity ->
        origin: PromptAuthority.PromptOrigin ->
        requestKind: ProviderRequestKind ->
        opportunity: RecoveryOpportunity ->
        selectProbe: (unit -> Result<PrefixProbe, NoCandidateReason>) ->
            AttemptPlan

    val probeOf: plan: AttemptPlan -> PrefixProbe option
    val promotableProbe: plan: AttemptPlan -> outcome: AttemptOutcome -> PrefixProbe option
