namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// crash-reconciliation-020: which child a handle id names is durable evidence.
/// A restarted process starts with empty process tables, so every reuse/await
/// decision that used to read them must be able to answer from the parent's
/// handle projection instead — the tables are only a cache of what this process
/// currently drives.
module DurableChildLookup =

    /// Resolve one logical byname against the parent's durable linkage.
    let byByname (handles: AgentLinkageProjection) (byname: string) : (SessionId * Role * string) option =
        if System.String.IsNullOrWhiteSpace byname then
            None
        else
            HandleProjection.tryFindByByname (byname.Trim()) handles
            |> Option.map (fun record -> record.ChildSessionId, record.CanonicalRole, record.TargetAgent)

    /// Resolve one handle id against the parent's durable linkage: the child
    /// session, its role, and the execution agent the Host dispatches with.
    let byHandleId (handles: AgentLinkageProjection) (agentId: string) : (SessionId * Role * string) option =
        if System.String.IsNullOrWhiteSpace agentId then
            None
        else
            let handle = HandleId.Agent(AgentHandleId.create (agentId.Trim()))

            HandleProjection.tryFind handle handles
            |> Option.map (fun record -> record.ChildSessionId, record.CanonicalRole, record.TargetAgent)
