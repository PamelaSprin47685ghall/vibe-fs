namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

type CompactionProbe =
    { TryClaimStartupProbe: unit -> bool
      ReadCompactionSettingGap: unit -> CompactionSetting option
      IsStartupProbeOpen: unit -> bool }

/// HOST-006: observe reconciled snapshots for compaction startup gate + reanchor.
module HostCompactionObserver =

    val observe:
        probe: CompactionProbe ->
        journal: AgentJournal option ->
        sessionId: SessionId ->
        messages: SessionMessage list ->
            Task
