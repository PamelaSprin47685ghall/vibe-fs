namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

/// HOST-006: observe reconciled snapshots for compaction startup gate + reanchor.
module HostCompactionObserver =

    val observe:
        scope: PluginRuntimeScope ->
        journal: AgentJournal option ->
        sessionId: SessionId ->
        messages: SessionMessage list ->
            Task
