namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

/// HOST-006: observe reconciled snapshots for compaction startup gate + reanchor.
type CompactionProbe =
    { TryClaimStartupProbe: unit -> bool
      ReadCompactionSettingGap: unit -> CompactionSetting option
      IsStartupProbeOpen: unit -> bool }

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

    let private applyStartupVerdict (probe: CompactionProbe) verdict =
        if probe.TryClaimStartupProbe() then
            raiseOnStartupFailure verdict

    let private runStartupProbe (probe: CompactionProbe) (sessionId: SessionId) (messages: SessionMessage list) : unit =
        match HostCompactionGate.judgeStartup (probe.ReadCompactionSettingGap()) sessionId messages with
        | None -> ()
        | Some verdict -> applyStartupVerdict probe verdict

    let private observeStartupProbe
        (probe: CompactionProbe)
        (sessionId: SessionId)
        (messages: SessionMessage list)
        : unit =
        if probe.IsStartupProbeOpen() then
            runStartupProbe probe sessionId messages

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
        (probe: CompactionProbe)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (messages: SessionMessage list)
        : Task =
        task {
            // Current-process compaction observation proceeds from its exact facts.
            // No durable-family gate is fabricated here, and Waiting is never
            // treated as Ready.
            // HOST-006 prevention layer's second half: the runtime probe.
            observeStartupProbe probe sessionId messages

            match journal with
            | None -> ()
            | Some durable -> do! observeDurable durable sessionId messages
        }
