namespace Wanxiangshu.Mission.Manager.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Persistence.Journal

/// Provider-facing Birth/Reawakening rewrite from durable Life projection + raw messages.
module ManagerNarrativeTransform =

    /// GLORY-013 order (after X capture, before seal): open the Life and rewrite.
    ///
    /// Returns the rewritten message list when a Life was opened; `None` when
    /// nothing applies (non-Manager, no legal HumanRoot, Life already open,
    /// already injected, no journal).
    val tryTransform:
        journal: AgentJournal option ->
        sessionId: string option ->
        traceState: XTraceProjectionState option ->
        rawMessages: obj list ->
            Task<obj list option>
