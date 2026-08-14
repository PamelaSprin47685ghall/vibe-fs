namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait

/// Composition-internal CE module: referenced only by SyncDelegateRuntime.
module internal SyncDelegateWorkflow =

    /// Everything SyncDelegateRuntime.Invoke needs from its host, so the workflow
    /// runs against the store + dependencies without touching runtime fields.
    type Dependencies =
        { Attached: AttachedSessionRuntime
          ResolveOwnerTier: SessionId -> AgentTier option
          CreateChild: SessionId -> string -> string option -> Task<Result<SessionId, string>>
          OnDelegateReady: SessionId -> string -> unit
          NoteInspectorPrompt: string -> string -> unit
          CleanupInspectorDraft: string -> unit
          Directory: string option
          ReplaceToolEstimate: SessionId -> int option -> Task<unit>
          SendPrompt: SyncDelegateCall -> SyncDelegatePromptRequest -> Task<Result<unit, string>>
          ResolveBoundAgent: SessionId -> string option
          DescribeWait: SyncDelegateWait -> DiagnosticWait }

    let private completeError (invocations: SyncDelegateInvocation list) error =
        for invocation in invocations do
            AsyncSupport.trySetResult invocation.Completion (Error error) |> ignore

    let private completeBatch (invocations: SyncDelegateInvocation list) answered =
        match answered, invocations with
        | Error error, _ -> completeError invocations error
        | Ok _, [] -> ()
        | Ok workRecord, canonical :: siblings ->
            AsyncSupport.trySetResult canonical.Completion (Ok(SyncDelegateInvocationResult.WorkRecord workRecord))
            |> ignore

            match canonical.Batch with
            | Some batch ->
                let canonicalCall = List.head batch.CallOrder

                for sibling in siblings do
                    AsyncSupport.trySetResult
                        sibling.Completion
                        (Ok(SyncDelegateInvocationResult.MergedInto canonicalCall))
                    |> ignore
            | None ->
                for sibling in siblings do
                    AsyncSupport.trySetResult
                        sibling.Completion
                        (Error "sync delegate protocol defect: ungrouped invocation has siblings")
                    |> ignore

    /// EXEC-026 / EXEC-031: reusable SyncDelegate CE. A semantic batch is fixed by
    /// ProviderRun tool-call membership before dispatch; scheduler timing never
    /// changes which invocations are concatenated.
    let invoke
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerSessionKey: string)
        (role: SyncDelegateRole)
        (charge: string)
        (expectedToolCalls: int option)
        (batch: SyncDelegateBatch option)
        (prepareProviderPrompt: unit -> Task<string>)
        : Task<Result<SyncDelegateInvocationResult, string>> =
        task {
            let owner = SessionId.create ownerSessionKey
            let ownerScope = ReuseScope.ofSession owner

            let completion =
                TaskCompletionSource<Result<SyncDelegateInvocationResult, string>>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                )

            let invocation: SyncDelegateInvocation =
                { Owner = owner
                  OwnerScope = ownerScope
                  Role = role
                  Charge = charge
                  ExpectedToolCalls = expectedToolCalls
                  PrepareProviderPrompt = prepareProviderPrompt
                  Batch = batch
                  Completion = completion
                  StartCursor = None }

            match store.Admit invocation with
            | SyncDelegateAdmission.Rejected error -> AsyncSupport.trySetResult completion (Error error) |> ignore
            | SyncDelegateAdmission.Waiting -> ()
            | SyncDelegateAdmission.Ready invocations ->
                let first = List.head invocations
                let batchOwner = first.Owner

                if role = SyncDelegateRole.Inspector then
                    store.TryTakeDeletedInspector ownerScope
                    |> Option.iter (fun sessionId -> deps.CleanupInspectorDraft(SessionId.value sessionId))

                match deps.ResolveOwnerTier batchOwner with
                | None ->
                    store.ReleaseAdmission(ownerScope, role)
                    completeError invocations "sync delegate rejected: owner tier unknown"
                | Some ownerTier ->
                    let tier = SyncDelegate.tierForOwner ownerTier
                    let agentName = SyncDelegate.agentNameFor role tier

                    try
                        match!
                            deps.Attached.GetOrCreate(
                                batchOwner,
                                role,
                                agentName,
                                deps.Directory,
                                deps.CreateChild,
                                deps.OnDelegateReady
                            )
                        with
                        | Error error ->
                            store.ReleaseAdmission(ownerScope, role)
                            completeError invocations error
                        | Ok(delegateSession, attachedAgent) ->
                            let combinedExpectedToolCalls =
                                invocations
                                |> List.choose (fun item -> item.ExpectedToolCalls)
                                |> function
                                    | [] -> None
                                    | values -> Some(List.sum values)

                            do! deps.ReplaceToolEstimate delegateSession combinedExpectedToolCalls

                            let! preparedPrompts =
                                task {
                                    let results = ResizeArray<string>()

                                    for item in invocations do
                                        try
                                            let! prompt = item.PrepareProviderPrompt()
                                            results.Add prompt
                                        with _ ->
                                            results.Add item.Charge

                                    return results |> Seq.toList
                                }

                            let combinedCharge =
                                invocations |> List.map (fun item -> item.Charge) |> String.concat "\n\n"

                            let combinedProviderPrompt = preparedPrompts |> String.concat "\n\n"

                            let sendAgent =
                                deps.ResolveBoundAgent delegateSession |> Option.defaultValue attachedAgent

                            match
                                store.BeginCall(batchOwner, ownerScope, role, delegateSession, sendAgent, invocations)
                            with
                            | Error error ->
                                store.ReleaseAdmission(ownerScope, role)
                                completeError invocations error
                            | Ok(call, registration) ->
                                use _registration = registration

                                let request =
                                    SyncDelegatePrompt.withProviderPrompt combinedCharge combinedProviderPrompt

                                let! answered =
                                    task {
                                        match! deps.SendPrompt call request with
                                        | Error error -> return Error error
                                        | Ok _ ->
                                            if role = SyncDelegateRole.Inspector then
                                                deps.NoteInspectorPrompt
                                                    (SessionId.value delegateSession)
                                                    request.Charge

                                            return!
                                                CausalAwait.awaitTask
                                                    CausalWaitHub.observer
                                                    (deps.DescribeWait(
                                                        DelegateCompletion(batchOwner, delegateSession, role)
                                                    ))
                                                    call.Answer.Task
                                    }

                                completeBatch invocations answered
                    with ex ->
                        store.ReleaseAdmission(ownerScope, role)
                        completeError invocations ex.Message

            return! completion.Task
        }
