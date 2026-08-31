namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Persistence.Journal

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
    let private raiseOnStartupFailure verdict =
        match verdict with
        | CompactionGateVerdict.Satisfied -> ()
        | failed -> raise (InvalidOperationException(HostCompactionPolicy.describeVerdict failed))

    let private applyStartupVerdict (scope: PluginRuntimeScope) verdict =
        if scope.TryClaimStartupProbe() then
            raiseOnStartupFailure verdict

    let private runStartupProbe
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (messages: SessionMessage list)
        : unit =
        match HostCompactionGate.judgeStartup scope.CompactionSettingGap sessionId messages with
        | None -> ()
        | Some verdict -> applyStartupVerdict scope verdict

    let private observeStartupProbe
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (messages: SessionMessage list)
        : unit =
        if scope.IsStartupProbeOpen then
            runStartupProbe scope sessionId messages

    let private observedCompactions messages =
        messages
        |> List.filter (fun message -> HostCompactionPolicy.isContainableCompaction message.IsCompaction)
        |> List.map (fun message -> ProviderRunIdentity.create message.Id)

    let private reanchorObserved durable sessionId observed : Task =
        task {
            match! HostCompactionGate.reanchorObserved durable sessionId observed with
            | Ok None
            | Ok(Some _) -> ()
            | Error reason -> HostCompactionGate.logReanchorFailure sessionId reason
        }

    let private observeDurable journal sessionId messages : Task =
        let observed = observedCompactions messages

        if List.isEmpty observed then
            task { return () }
        else
            reanchorObserved journal sessionId observed

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
            observeStartupProbe scope sessionId messages

            match journal with
            | None -> ()
            | Some durable -> do! observeDurable durable sessionId messages
        }
