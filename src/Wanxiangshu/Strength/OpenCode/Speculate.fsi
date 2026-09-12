namespace Wanxiangshu.Strength.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength.Persistence

/// STRENGTH-002/006/009/010: Host boundary for one owner speculation opportunity.
/// All policy math stays in Domain; this adapter only freezes Host evidence,
/// invokes the decision-local Replica, publishes Prepared, and applies the
/// insertion intent after publication succeeds.
[<RequireQualifiedAccess>]
module StrengthSpeculate =

    val tryApply:
        snapshotPort: ISessionSnapshotPort option ->
        journal: AgentJournal option ->
        strengthDurability: StrengthDurabilityPort option ->
        strengthScope: PluginStrengthScope ->
        tryAttemptPlan: (SessionId -> ProviderRunIdentity -> AttemptPlan option) ->
        syncDelegateRuntime: SyncDelegateRuntime option ->
        output: obj ->
            Task<unit>
