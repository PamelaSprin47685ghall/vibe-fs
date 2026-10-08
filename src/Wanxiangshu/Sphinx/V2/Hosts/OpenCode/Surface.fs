namespace Wanxiangshu.Sphinx.V2.Hosts.OpenCode

open Wanxiangshu.Sphinx.V2.Hosts
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Persistence

/// WHAT[sphinx-v2-034]：借用原 SyncDelegate owner，观察实际发送、接纳与终态。
module Surface =
    let capabilities () : string array =
        OpenCodeHostPort(Unchecked.defaultof<SyncDelegateRuntime>).Capabilities()
        |> List.toArray

    type private DispatchProbe = { Harness: obj; Owner: SessionId }

    let createDispatchProbe (directory: string) (owners: obj) (owner: string) : Task<obj> =
        task {
            let! harness = SyncDelegateSurface.createForHostRecording directory owners

            return
                box
                    { Harness = harness
                      Owner = SessionId.create owner }
        }

    let startDispatch (probe: obj) (_inquiry: string) (request: obj) : obj =
        let probe = unbox<DispatchProbe> probe

        let refused reason : SyncDelegateObservedExecution =
            { Admission = Task.FromResult(SyncDelegateObservedAdmission.NotDispatched reason)
              Completion = Task.FromResult(Error reason) }

        match Codec.decodeBody request with
        | Ok(InquiryEventBody.DispatchRequested request) ->
            SyncDelegateSurface.withHostRuntime probe.Harness (fun runtime _ ->
                OpenCodeHostPort(runtime).Dispatch(probe.Owner, request))
            |> box
        | Ok _ -> refused "expected DispatchRequested" |> box
        | Error fault -> refused fault.Message |> box

    let dispatchAdmission (execution: obj) =
        SyncDelegateSurface.observedAdmission execution

    let dispatchCompletion (execution: obj) =
        SyncDelegateSurface.observedCompletion execution

    let hostRecording (probe: obj) =
        SyncDelegateSurface.recordingSnapshot (unbox<DispatchProbe> probe).Harness

    let awaitHostPrompts (probe: obj) count =
        SyncDelegateSurface.awaitRecordingPromptCount (unbox<DispatchProbe> probe).Harness count

    let returnHostOutcome (probe: obj) session index (outcome: obj) =
        SyncDelegateSurface.returnRecordingPromptOutcome (unbox<DispatchProbe> probe).Harness session index outcome

    let disposeDispatchProbe (probe: obj) =
        SyncDelegateSurface.dispose (unbox<DispatchProbe> probe).Harness

    let confirmHostPhysical (probe: obj) index physical =
        let probe = unbox<DispatchProbe> probe

        SyncDelegateSurface.confirmManagedPromptPhysical
            probe.Harness
            (SessionId.value probe.Owner)
            "Engineer"
            index
            physical

    let settleHostTerminal (probe: obj) session physical root providerRun text =
        SyncDelegateSurface.settleExactTerminal
            (unbox<DispatchProbe> probe).Harness
            session
            physical
            root
            providerRun
            text
            ""
