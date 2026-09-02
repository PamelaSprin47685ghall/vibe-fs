namespace Wanxiangshu.Repository.Knowledge.Casebook.OpenCode

open Wanxiangshu.OpenCode

/// CASE-009: assembles the conditional Casebook tool specs. This module is
/// the only place that names the EventStore for tool registration, keeping
/// ToolRegistry / PluginHostInterop / SpikePlugin free of the dual-write
/// token pair (AgentJournal + IEventStore in one file is forbidden).
module CasebookTools =

    val buildSpecs: factory: HostToolFactory -> workspaceRoot: string -> ToolSpec list
