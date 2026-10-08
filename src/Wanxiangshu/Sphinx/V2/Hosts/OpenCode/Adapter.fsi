namespace Wanxiangshu.Sphinx.V2.Hosts

open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Sphinx.V2.Core

/// Borrows the original managed delegation owner. The composition must admit
/// the durable intent before calling this internal effect boundary.
type OpenCodeHostPort =
    internal new: runtime: SyncDelegateRuntime -> OpenCodeHostPort
    member Capabilities: unit -> string list
    member internal Dispatch: owner: SessionId * request: DispatchRequestedBody -> SyncDelegateObservedExecution
