namespace Wanxiangshu.Sphinx.V2.Hosts.OpenCode

open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Sphinx.V2.Core

/// Borrows the original managed delegation owner. The composition must admit
/// the durable intent before calling this internal effect boundary.
type OpenCodeHostPort =
    internal new: runtime: SyncDelegateRuntime -> OpenCodeHostPort
    member Capabilities: unit -> string list

    member internal Dispatch:
        owner: SessionId * request: DispatchRequestedBody * workspace: Wanxiangshu.Host.Ports.WorkspacePath option ->
            SyncDelegateObservedExecution

/// The host boundary of the Sphinx v2 OpenCode adapter: product admission and the
/// host effect in one place, so a body that is not a dispatch request never
/// reaches the host.
[<RequireQualifiedAccess>]
module HostPort =
    /// Product capabilities this host may advertise (the product decision lives in Core).
    val capabilities: unit -> string list

    /// Why a dispatch was refused before any host call.
    val refused: reason: string -> SyncDelegateObservedExecution

    /// Product admission + the host effect, in one boundary.
    val startDispatch:
        runtime: SyncDelegateRuntime ->
        owner: SessionId ->
        body: Result<InquiryEventBody, string> ->
        workspace: Wanxiangshu.Host.Ports.WorkspacePath option ->
            SyncDelegateObservedExecution
