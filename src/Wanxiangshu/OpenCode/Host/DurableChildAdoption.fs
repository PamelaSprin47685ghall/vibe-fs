namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity

/// crash-reconciliation-018/020: `/continue` re-enlists surviving children
/// process-locally so a later LLM reuse can address them. A plain restart has no
/// such command, and the child registry of the fork runtime starts empty — so a
/// manager reuse of an existing byname answered `person-unavailable`
/// ("此人目前无法再接下另一项托付") for every durable child, including the fixed
/// DevOps and any forked Engineer child.
///
/// Re-enlisting is bookkeeping, not a resume: no prompt is sent, no command is
/// replayed, and reuse itself stays the manager's explicit action. This module
/// only decides which durable handles are addressable that way.
module DurableChildAdoption =

    let private isParentVisible (record: HandleRecord) : bool =
        record.Ownership = HandleOwnership.DurableParentHandle

    let private isAdoptable (record: HandleRecord) : bool =
        match record.Lifecycle with
        | HandleLifecycle.Active
        | HandleLifecycle.CompletedAwaitingJoin _ -> true
        | HandleLifecycle.Abandoned _
        | HandleLifecycle.Retired -> false

    /// Durable children a restarted process may address again, with the parent
    /// session that owns each handle. Abandoned and retired tombstones stay
    /// tombstones; Host-owned hidden leaves stay parent-invisible.
    let adoptableChildren (projections: AgentProjectionSet) : (SessionId * HandleRecord) list =
        projections.Sessions
        |> Map.toList
        |> List.collect (fun (parentSessionId, session) ->
            match session.Handles with
            | None -> []
            | Some handles ->
                handles.Handles
                |> Map.toList
                |> List.choose (fun (_, record) ->
                    if isParentVisible record && isAdoptable record then
                        Some(parentSessionId, record)
                    else
                        None))
