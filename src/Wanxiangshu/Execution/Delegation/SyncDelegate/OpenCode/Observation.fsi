namespace Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode

open Wanxiangshu.Execution.Delegation.SyncDelegate

[<RequireQualifiedAccess>]
module SyncDelegateHostObservation =
    val observe: runtime: SyncDelegateRuntime option -> rawInput: obj -> unit
