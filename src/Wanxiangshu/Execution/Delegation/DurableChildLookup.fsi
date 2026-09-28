namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Durable evidence for which child a handle id names, independent of any
/// in-process registration.
module DurableChildLookup =

    val byByname: handles: AgentLinkageProjection -> byname: string -> (SessionId * Role * string) option

    val byHandleId: handles: AgentLinkageProjection -> agentId: string -> (SessionId * Role * string) option
