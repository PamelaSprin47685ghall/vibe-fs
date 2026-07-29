namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks

/// Dispatch operations for child agent runs: create, nudge, cancel.
///
/// Extracted from HostForkChildDispatch to isolate child lifecycle logic
/// from the HostForkRuntime bridge. These functions operate on a ForkRuntime
/// directly and do not depend on Host sessions or journal.
module ChildDispatch =

    /// Fork a new child run. Creates a ChildRun in the runtime and starts
    /// the work. Returns:
    ///   - Created if the agentId is brand-new to this runtime
    ///   - Nudged if the agent already exists (busy or idle)
    ///   - NotFound if the runtime is cancelled
    let forkNew
        (runtime: ForkRuntime)
        (agentId: string)
        (role: AgentRole)
        (prompt: string)
        : ForkResult =
        runtime.Fork(agentId, role, prompt = prompt)

    /// Fork with a custom work function (used for test injection and
    /// PendingHostRun completion bridging).
    let forkWithWork
        (runtime: ForkRuntime)
        (agentId: string)
        (role: AgentRole)
        (work: unit -> Task<AgentCompletionOutcome>)
        (agent: string option)
        : ForkResult =
        runtime.Fork(agentId, role, runWork = work, ?agent = agent)

    /// Nudge an existing agent with a prompt (fire-and-forget).
    /// This only succeeds if the agent already exists in the runtime.
    /// If the agent is busy, returns Nudged; if idle, starts a new run
    /// and returns Nudged; if unknown, returns NotFound.
    let nudgeExisting
        (runtime: ForkRuntime)
        (agentId: string)
        (role: AgentRole)
        (prompt: string)
        : ForkResult =
        runtime.Fork(agentId, role, prompt = prompt)

    /// Try to cancel a specific child run by agentId. Returns true if the
    /// agent was found and cancelled, false otherwise.
    let tryCancel
        (runtime: ForkRuntime)
        (agentId: string)
        : bool =
        let (agentList, _) = runtime.List()

        match agentList |> List.tryFind (fun a -> a.AgentId = agentId) with
        | Some _ ->
            // We can't cancel individual agents through the current ForkRuntime
            // API; only Cancel() cancels all. For individual cancellation we
            // currently rely on the Host aborting the child session.
            // This function is a placeholder for P6.
            false
        | None -> false

    /// Return true if the runtime has any active (busy) agents.
    let hasActiveAgents (runtime: ForkRuntime) : bool =
        runtime.ActiveRunCount > 0

    /// Return true if the runtime has pending completions ready for Join().
    let hasPendingCompletions (runtime: ForkRuntime) : bool =
        runtime.PendingCompletionCount > 0

    /// Await the next completion.
    let join (runtime: ForkRuntime) : Task<Result<RunCompletion, ForkError>> =
        runtime.Join()

    /// Cancel all runs and reset the runtime.
    let cancelAll (runtime: ForkRuntime) : unit =
        runtime.Cancel()

    /// Restore a previously-linked agent into an idle state.
    let restoreAgent
        (runtime: ForkRuntime)
        (agentId: string)
        (role: AgentRole)
        (agent: string option)
        : unit =
        runtime.Restore(agentId, role, ?agent = agent)

    /// Mark an agent as interrupted (failed recovery).
    let markInterrupted
        (runtime: ForkRuntime)
        (agentId: string)
        (reason: string)
        : unit =
        runtime.MarkInterrupted(agentId, reason)

    /// Bind a child session ID to an existing agent record.
    let bindChildSession
        (runtime: ForkRuntime)
        (agentId: string)
        (childSessionId: string)
        : unit =
        runtime.BindChildSession(agentId, childSessionId)

    /// List all agents and PTYs currently tracked.
    let list (runtime: ForkRuntime) : AgentRecord list * PtyRecord list =
        runtime.List()
