namespace Wanxiangshu.Sphinx.V2.Hosts.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Sphinx.V2.Core

/// Borrows the original managed delegation owner. The composition must admit
/// the durable intent before calling this internal effect boundary.
type OpenCodeHostPort internal (runtime: SyncDelegateRuntime) =
    member _.Capabilities() : string list = DispatchPolicy.hostCapabilities

    /// `workspace` is the neutral workspace the host resolved for this inquiry.
    /// Sphinx v2 carries it for the omp adapter and does not read it yet.
    member internal _.Dispatch
        (owner: SessionId, request: DispatchRequestedBody, _workspace: Wanxiangshu.Host.Ports.WorkspacePath option)
        : SyncDelegateObservedExecution =
        match runtime.ValidateObservedOwner owner with
        | Error reason ->
            { Admission = Task.FromResult(SyncDelegateObservedAdmission.NotDispatched reason)
              Completion = Task.FromResult(Error reason) }
        | Ok() ->
            let publicPrompt = request.PublicEnvelope.CanonicalPayload

            runtime.InvokeObservedPrepared(
                owner,
                publicPrompt,
                (fun () -> Task.FromResult(LlmFacing.instruction publicPrompt))
            )

/// The host boundary of the Sphinx v2 OpenCode adapter: product admission and the
/// host effect in one place, so a body that is not a dispatch request never
/// reaches the host.
[<RequireQualifiedAccess>]
module HostPort =
    /// Product capabilities this host may advertise (the product decision lives in Core).
    let capabilities () : string list = DispatchPolicy.hostCapabilities

    /// Why a dispatch was refused before any host call.
    let refused (reason: string) : SyncDelegateObservedExecution =
        { Admission = Task.FromResult(SyncDelegateObservedAdmission.NotDispatched reason)
          Completion = Task.FromResult(Error reason) }

    /// Product admission + the host effect, in one boundary: a body that is not a
    /// dispatch request is refused here and never reaches the host.
    let startDispatch
        (runtime: SyncDelegateRuntime)
        (owner: SessionId)
        (body: Result<InquiryEventBody, string>)
        (workspace: Wanxiangshu.Host.Ports.WorkspacePath option)
        : SyncDelegateObservedExecution =
        match body with
        | Error reason -> refused reason
        | Ok body ->
            match DispatchPolicy.admit body with
            | Error reason -> refused reason
            | Ok request -> OpenCodeHostPort(runtime).Dispatch(owner, request, workspace)
