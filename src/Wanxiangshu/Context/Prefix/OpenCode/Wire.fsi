namespace Wanxiangshu.Context.Prefix.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Participant.Provider.Attempt

type AttemptPlanCapability =
    { TryAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option
      TryBindAttemptPlan: SessionId -> PhysicalUserMessageId -> ProviderRunIdentity -> AttemptPlan option
      ConsumeAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option
      FreezePendingAttemptPlan: SessionId -> PhysicalUserMessageId -> PendingAttemptPlan -> PendingAttemptPlanAdmission
      TryPendingAttemptPlan: SessionId -> PhysicalUserMessageId -> PendingAttemptPlan option }

module XWire =

    val suppressHostMessagesByIds: rawMessages: obj list -> hostMessageIds: Set<string> -> obj list

    val applyTransform:
        snapshot: ISessionSnapshotPort option ->

        port: WireJournalPort option ->
        attempts: AttemptPlanCapability ->
        output: obj ->
            Task

    val reconcileAttempt:
        port: WireJournalPort option -> attempts: AttemptPlanCapability -> turn: ReconciledTurn -> Task
