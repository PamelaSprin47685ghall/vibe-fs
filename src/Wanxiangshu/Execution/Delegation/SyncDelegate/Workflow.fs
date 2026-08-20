namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.OpenCode

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
          DescribeWait: SyncDelegateWait -> DiagnosticWait
          SubscribeFutureTerminal: SessionId -> TerminalCompletionListener -> System.IDisposable }

    let private completeError (invocations: SyncDelegateInvocation list) error =
        for invocation in invocations do
            AsyncSupport.trySetResult invocation.Completion (Error error) |> ignore

    let private completeMergedSiblings (siblings: SyncDelegateInvocation list) (canonicalCall: ToolCallId) =
        for sibling in siblings do
            AsyncSupport.trySetResult sibling.Completion (Ok(SyncDelegateInvocationResult.MergedInto canonicalCall))
            |> ignore

    let private completeUngroupedSiblings (siblings: SyncDelegateInvocation list) =
        for sibling in siblings do
            AsyncSupport.trySetResult
                sibling.Completion
                (Error "sync delegate protocol defect: ungrouped invocation has siblings")
            |> ignore

    let private completeCanonicalBatch
        (canonical: SyncDelegateInvocation)
        (siblings: SyncDelegateInvocation list)
        (workRecord: string)
        =
        AsyncSupport.trySetResult canonical.Completion (Ok(SyncDelegateInvocationResult.WorkRecord workRecord))
        |> ignore

        match canonical.Batch with
        | Some batch -> completeMergedSiblings siblings (List.head batch.CallOrder)
        | None -> completeUngroupedSiblings siblings

    let private completeBatch (invocations: SyncDelegateInvocation list) answered =
        match answered, invocations with
        | Error error, _ -> completeError invocations error
        | Ok _, [] -> ()
        | Ok workRecord, canonical :: siblings -> completeCanonicalBatch canonical siblings workRecord

    let private failAdmission
        (store: SyncDelegateCallStore)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (invocations: SyncDelegateInvocation list)
        (error: string)
        =
        store.ReleaseAdmission(ownerScope, role)
        completeError invocations error

    let private maybeCleanupInspector
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        =
        match role with
        | SyncDelegateRole.Inspector ->
            store.TryTakeDeletedInspector ownerScope
            |> Option.iter (fun sessionId -> deps.CleanupInspectorDraft(SessionId.value sessionId))
        | SyncDelegateRole.Coder -> ()

    let private sumExpectedToolCalls (invocations: SyncDelegateInvocation list) =
        match invocations |> List.choose (fun item -> item.ExpectedToolCalls) with
        | [] -> None
        | values -> Some(List.sum values)

    let private prepareOnePrompt (item: SyncDelegateInvocation) : Task<string> =
        task {
            try
                return! item.PrepareProviderPrompt()
            with _ ->
                return item.Charge
        }

    let private prepareAllPrompts (invocations: SyncDelegateInvocation list) : Task<string list> =
        task {
            // DSL-MUTABLE: algorithm-scratch — prompt result accumulator
            let results = ResizeArray<string>()

            for item in invocations do
                let! prompt = prepareOnePrompt item
                results.Add prompt

            return results |> Seq.toList
        }

    let private failPoppedCall (store: SyncDelegateCallStore) (delegateSession: SessionId) (message: string) =
        match store.TryPopCallByDelegate delegateSession with
        | Some call -> store.FailCall(call, message)
        | None -> ()

    let private onTerminalOutcome
        (store: SyncDelegateCallStore)
        (delegateSession: SessionId)
        (_sessionId: SessionId)
        (outcome: TerminalOutcome)
        =
        match outcome with
        | TerminalOutcome.Failed error ->
            failPoppedCall store delegateSession (sprintf "SyncDelegate run failed: %s" error)
        | TerminalOutcome.Aborted reason ->
            failPoppedCall store delegateSession (sprintf "SyncDelegate run aborted: %s" reason)
        | TerminalOutcome.Completed _ -> ()

    let private sendAndAwait
        (deps: Dependencies)
        (store: SyncDelegateCallStore)
        (role: SyncDelegateRole)
        (batchOwner: SessionId)
        (delegateSession: SessionId)
        (call: SyncDelegateCall)
        (request: SyncDelegatePromptRequest)
        : Task<Result<string, string>> =
        taskResult {
            use _terminalSub =
                deps.SubscribeFutureTerminal delegateSession (onTerminalOutcome store delegateSession)

            do! deps.SendPrompt call request

            if role = SyncDelegateRole.Inspector then
                deps.NoteInspectorPrompt (SessionId.value delegateSession) request.Charge

            return!
                CausalAwait.awaitTask
                    CausalWaitHub.observer
                    (deps.DescribeWait(DelegateCompletion(batchOwner, delegateSession, role)))
                    call.Answer.Task
        }

    let private dispatchBegunCall
        (deps: Dependencies)
        (store: SyncDelegateCallStore)
        (role: SyncDelegateRole)
        (batchOwner: SessionId)
        (delegateSession: SessionId)
        (invocations: SyncDelegateInvocation list)
        (combinedCharge: string)
        (combinedProviderPrompt: string)
        (begun: SyncDelegateCall * System.IDisposable)
        : Task<unit> =
        task {
            let call, registration = begun
            use _registration = registration

            let request =
                SyncDelegatePrompt.withProviderPrompt combinedCharge combinedProviderPrompt

            let! answered = sendAndAwait deps store role batchOwner delegateSession call request
            completeBatch invocations answered
        }

    let private beginAndDispatch
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (batchOwner: SessionId)
        (delegateSession: SessionId)
        (sendAgent: string)
        (invocations: SyncDelegateInvocation list)
        (combinedCharge: string)
        (combinedProviderPrompt: string)
        : Task<unit> =
        match store.BeginCall(batchOwner, ownerScope, role, delegateSession, sendAgent, invocations) with
        | Error error ->
            failAdmission store ownerScope role invocations error
            Task.FromResult(())
        | Ok begun ->
            dispatchBegunCall
                deps
                store
                role
                batchOwner
                delegateSession
                invocations
                combinedCharge
                combinedProviderPrompt
                begun

    let private afterAttached
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (batchOwner: SessionId)
        (invocations: SyncDelegateInvocation list)
        (delegateSession: SessionId)
        (attachedAgent: string)
        : Task<unit> =
        task {
            do! deps.ReplaceToolEstimate delegateSession (sumExpectedToolCalls invocations)
            let! preparedPrompts = prepareAllPrompts invocations

            let combinedCharge =
                invocations |> List.map (fun item -> item.Charge) |> String.concat "\n\n"

            let combinedProviderPrompt = preparedPrompts |> String.concat "\n\n"

            let sendAgent =
                deps.ResolveBoundAgent delegateSession |> Option.defaultValue attachedAgent

            do!
                beginAndDispatch
                    store
                    deps
                    ownerScope
                    role
                    batchOwner
                    delegateSession
                    sendAgent
                    invocations
                    combinedCharge
                    combinedProviderPrompt
        }

    let private runAttachedPair
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (batchOwner: SessionId)
        (invocations: SyncDelegateInvocation list)
        (attached: SessionId * string)
        : Task<unit> =
        let delegateSession, attachedAgent = attached
        afterAttached store deps ownerScope role batchOwner invocations delegateSession attachedAgent

    let private acquireAndRun
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (batchOwner: SessionId)
        (agentName: string)
        (invocations: SyncDelegateInvocation list)
        : Task<unit> =
        task {
            let! attached =
                deps.Attached.GetOrCreate(
                    batchOwner,
                    role,
                    agentName,
                    deps.Directory,
                    deps.CreateChild,
                    deps.OnDelegateReady
                )

            match attached with
            | Error error -> failAdmission store ownerScope role invocations error
            | Ok pair -> do! runAttachedPair store deps ownerScope role batchOwner invocations pair
        }

    let private runReadyWithTier
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (batchOwner: SessionId)
        (invocations: SyncDelegateInvocation list)
        (ownerTier: AgentTier)
        : Task<unit> =
        let tier = SyncDelegate.tierForOwner ownerTier
        let agentName = SyncDelegate.agentNameFor role tier

        task {
            try
                do! acquireAndRun store deps ownerScope role batchOwner agentName invocations
            with ex ->
                failAdmission store ownerScope role invocations ex.Message
        }

    let private runReadyBatch
        (store: SyncDelegateCallStore)
        (deps: Dependencies)
        (ownerScope: ReuseScopeId)
        (role: SyncDelegateRole)
        (invocations: SyncDelegateInvocation list)
        : Task<unit> =
        let first = List.head invocations
        let batchOwner = first.Owner
        maybeCleanupInspector store deps ownerScope role

        match deps.ResolveOwnerTier batchOwner with
        | None ->
            failAdmission store ownerScope role invocations "sync delegate rejected: owner tier unknown"
            Task.FromResult(())
        | Some ownerTier -> runReadyWithTier store deps ownerScope role batchOwner invocations ownerTier

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
            | SyncDelegateAdmission.Ready invocations -> do! runReadyBatch store deps ownerScope role invocations

            return!
                CausalAwait.awaitTask
                    CausalWaitHub.observer
                    (deps.DescribeWait(InvocationJoin(owner, role)))
                    completion.Task
        }
