namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// JS-native owner boundary for one HostForkRunLifecycle pending run.
///
/// The Host lifecycle remains the sole owner of terminal admission. A caller
/// supplies plain outcome observations and receives an opaque run capability;
/// PendingHostRun, TerminalOutcome, AgentRunResult, the completion source, and
/// the pending-runs dictionary never cross this boundary.
type HostForkRunLifecycleHandle =
    private new:
        gate: obj * pendingRuns: Dictionary<string, PendingHostRun> * parentId: SessionId * run: PendingHostRun ->
            HostForkRunLifecycleHandle

    member Gate: obj
    member PendingRuns: Dictionary<string, PendingHostRun>
    member ParentId: SessionId
    member Run: PendingHostRun
    static member Create: agentId: string * childId: string * parentId: string -> HostForkRunLifecycleHandle

[<RequireQualifiedAccess>]
module HostForkRunLifecycleSurface =
    /// Create one pending run. All lifecycle state is retained by the opaque handle.
    val create: value: obj -> obj

    /// Feed one plain terminal observation through the production lifecycle owner.
    val complete: handle: obj -> outcome: obj -> Task

    /// Read only lifecycle facts needed by the semantic contract.
    val observe: handle: obj -> obj

    /// Await the owner completion cell; the result is a plain status observation.
    val completion: handle: obj -> Task<obj>
