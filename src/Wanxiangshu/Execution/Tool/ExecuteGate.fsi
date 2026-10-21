namespace Wanxiangshu.Execution.Tool

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host

/// The part of a product tool the execute gate reads: its product name and its
/// body. The admission is not carried here; the gate resolves it from the
/// product admission table by tool name. The argument carrier stays opaque so
/// any host adapter's decoded arguments pass through untouched.
type GatedTool<'args> =
    { Name: string
      Execute: 'args -> HostToolContext -> Task<string> }

/// capability-enforcement-005/010/025: the exact session facts the execute
/// gate reads. The host registry fills every field from its runtime scope or its
/// own callbacks; a stage never receives the whole scope.
type ToolGateFacts =
    {
        RoleFor: HostToolContext -> Role option
        EnsureRoleFor: HostToolContext -> Task<Role option>
        ManagerFactsFor: string -> Task<ManagerCapabilityFacts>

        IsRetirementFrozen: string -> Task<bool>
        IsWaitingForUser: string -> bool
        /// LC-017: the pending question's exact fence (provider run, tool call)
        /// for a session, when a question is waiting for the user. Ask replays
        /// carrying the same fence pass the waiting gate; everything else
        /// stays refused.
        WaitingQuestionFenceFor: string -> (ProviderRunIdentity * ToolCallId) option
        BeginToolExecution: string -> unit
        EndToolExecution: string -> unit
        /// Algorithm E1.1: the work already recorded a retirement for this
        /// call's exact fence, so a retried deliver replays its state instead
        /// of being refused for the tenure that retirement removed.
        RecordedDeliverReplay: HostToolContext -> Task<bool>
    }

/// capability-enforcement: the product's tool execute gate. A host adapter
/// wraps every registered tool body in `execute`; the admission decision, the
/// refusal prose and the stage order live here, never in the adapter.
module ToolExecuteGate =

    /// Provider-step boundary, accounting, ablation, then the admission the
    /// product table declares for this tool name (office Role + Manager facts,
    /// or private attachment), then the tool body.
    val execute:
        gate: ToolGateFacts ->
        spec: GatedTool<'args> ->
        managerPermission: ToolPermission option ->
        args: 'args ->
        ctx: HostToolContext ->
            Task<string>
