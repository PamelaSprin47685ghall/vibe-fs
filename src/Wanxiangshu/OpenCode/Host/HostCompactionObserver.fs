namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
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
open Wanxiangshu.Foundation
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
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HOST-006: observe reconciled snapshots for compaction startup gate + reanchor.
module HostCompactionObserver =

    /// Observe every reconciled snapshot for compaction pseudo-runs and reanchor
    /// at most one per pass.
    ///
    /// Wired at the Scheduler rather than inside turn observation because a
    /// compaction pseudo-run belongs to no Logical Run of ours — a manual
    /// `/compact` produces one with no active root at all — so a turn-shaped
    /// callback would never see it.
    ///
    /// No journal means no durable epoch and nothing to reanchor. Silent rather
    /// than an error: a journal-less run has no PrefixEpoch to retire, so there is
    /// no state that could drift.
    let observe
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messages: SessionMessage list)
        : Task =
        task {
            // RECOVERY-FAMILY: family recovery before compaction probe effects.
            let! recovery = scope.EnsureRecoveryDone sessionId

            match recovery with
            | FamilyRecovery.FamilyBlocked _ -> return ()
            | FamilyRecovery.FamilyWaiting _
            | FamilyRecovery.FamilyReady _ -> ()

            // HOST-006 prevention layer's second half: the runtime probe.
            //
            // Judged before containment, and once per plugin instance. If the Host
            // compacts outside the configuration the plugin can reach, the correct
            // response is to refuse to run — not to reanchor and carry on, which would
            // hide the condition behind behaviour that looks correct.
            if scope.IsStartupProbeOpen then
                match HostCompactionGate.judgeStartup scope.CompactionSettingGap sessionId messages with
                | None -> () // Not a completed first turn yet; the probe stays armed.
                | Some verdict ->
                    if scope.TryClaimStartupProbe() then
                        match verdict with
                        | CompactionGateVerdict.Satisfied -> ()
                        | failed -> raise (InvalidOperationException(HostCompactionPolicy.describeVerdict failed))

            match journal with
            | None -> ()
            | Some durable ->
                let observed =
                    messages
                    |> List.filter (fun message -> HostCompactionPolicy.isContainableCompaction message.IsCompaction)
                    |> List.map (fun message -> ProviderRunIdentity.create message.Id)

                if List.isEmpty observed then
                    ()
                else
                    match! HostCompactionGate.reanchorObserved durable sessionId observed with
                    | Ok None
                    | Ok(Some _) -> ()
                    // A failed append here is not fatal to the turn that just
                    // completed. PERSIST-003's fail-closed path already owns a poisoned
                    // journal; what this must not do is throw inside the reconcile loop
                    // and leave `Running` set, which would stop every later pass for
                    // this session.
                    | Error reason ->
                        HostCompactionGate.logReanchorFailure sessionId reason
                        ()

        }
