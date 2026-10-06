namespace Wanxiangshu.Sphinx.V2.Hosts

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Sphinx.V2.Core

type OpenCodeHostPort internal (runtime: SyncDelegateRuntime) =
    member _.Capabilities() : string list = [ "dispatch" ]

    member internal _.Dispatch(owner: SessionId, request: DispatchRequestedBody) : SyncDelegateObservedExecution =
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
