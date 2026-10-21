namespace Wanxiangshu.Execution.Session.OpenCode

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Persistence.Journal

/// OpenCode adapter for horizon() (`HorizonRoster`): the host port binding,
/// the wire encoding and registration.
module HorizonTool =

    type HorizonRuntimeContext =
        { RuntimeFor: HostToolContext -> Result<HostForkRuntime, string>
          LogicalOwnerFor: SessionId -> SessionId
          Journal: AgentJournal option
          EnsureWorkDevOpsBound: SessionId -> Task<unit> }

    val admission: ToolAdmission
    val spec: runtimeCtx: HorizonRuntimeContext -> ToolSpec

    /// The same roster as data for `this.horizon()`: every horizon-visible
    /// participant with the status the prose line reports, its work state, its
    /// latest record body, and the four calibration numbers, plus the terminals
    /// still open. Provider prose is never rendered.
    val rosterReceipt: language: string -> runtimeCtx: HorizonRuntimeContext -> context: HostToolContext -> Task<obj>
