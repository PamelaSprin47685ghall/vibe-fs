namespace Wanxiangshu.Context.Prefix

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type PrefixPresentationHorizon =
    | Current
    | TentativeCold

type XWireReconciliationDecision =
    { Promoted: bool
      Cleared: bool
      KeptPlan: bool }

module XWire =

    val selectProbe:
        opportunity: RecoveryOpportunity ->
        candidate: Result<PrefixProbe, NoCandidateReason> ->
            Result<PrefixProbe, NoCandidateReason>

    val presentationHorizonForProbe: hasProbe: bool -> PrefixPresentationHorizon

    val reconciliationDecision:
        hasPlan: bool ->
        outcome: AttemptOutcome option ->
        hasPromotableProbe: bool ->
        probeEpochMatches: bool ->
            XWireReconciliationDecision

    val retryTransportRetirement: horizon: PrefixPresentationHorizon -> rawMessages: obj list -> Set<string>

    val replacePrefixByHostIds:
        rawMessages: obj list ->
        coveredHostMessageIds: string list ->
        openingHostMessageId: string option ->
        syntheticMessageId: string ->
        memory: string ->
            obj list

    val suppressHostMessagesByIds: rawMessages: obj list -> hostMessageIds: Set<string> -> obj list

    val applyTransform:
        snapshot: ISessionSnapshotPort option ->
        journal: AgentJournal option ->
        scope: PluginRuntimeScope ->
        output: obj ->
            Task<PrefixPresentationHorizon>

    val reconcileAttempt: journal: AgentJournal option -> scope: PluginRuntimeScope -> turn: ReconciledTurn -> Task
