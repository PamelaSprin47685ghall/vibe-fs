namespace Wanxiangshu.Infrastructure

open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.OpenCode

/// CASE-009: assembles the conditional Casebook tool specs. This module is
/// the only place that names the EventStore for tool registration, keeping
/// ToolRegistry / PluginHostInterop / SpikePlugin free of the dual-write
/// token pair (AgentJournal + IEventStore in one file is forbidden).
module CasebookTools =

    /// Marker + EventStore availability → fetch + js-bookkeeper, or none.
    /// Acquire failure degrades the surface instead of failing the plugin —
    /// the schema gate and the execution gate stay in agreement.
    let buildSpecs (factory: HostToolFactory) (workspaceRoot: string) : ToolSpec list =
        if not (CasebookFeature.isEnabled workspaceRoot) then
            []
        else
            try
                let store = WorkspaceEventStore.acquire (RuntimePath.gitCommonDir workspaceRoot)

                [ FetchTool.spec factory workspaceRoot store
                  JsBookkeeperTool.spec factory ]
            with _ ->
                []
