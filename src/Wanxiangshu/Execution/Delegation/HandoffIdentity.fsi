namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Execution.Delegation.SyncDelegate

type DelegationHandoffRoute = private DelegationHandoffRoute of string

[<RequireQualifiedAccess>]
module DelegationHandoffRoute =
    val forkByname: byname: string -> DelegationHandoffRoute
    val syncRole: scope: ReuseScopeId -> role: SyncDelegateRole -> DelegationHandoffRoute
    val value: DelegationHandoffRoute -> string
