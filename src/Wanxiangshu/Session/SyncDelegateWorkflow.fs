/// Composition-internal CE module: referenced only by SyncDelegateRuntime.
module internal SyncDelegateWorkflow

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open SyncDelegateWait

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
      SendPrompt: SyncDelegateCall -> SyncDelegatePromptRequest -> Task<Result<unit, string>>
      DescribeWait: SyncDelegateWait -> DiagnosticWait }

/// EXEC-026 / EXEC-031: reusable SyncDelegate CE with concurrent batch coalescing and direct in-flight dispatch.
let invoke
    (store: SyncDelegateCallStore)
    (deps: Dependencies)
    (ownerSessionKey: string)
    (role: SyncDelegateRole)
    (charge: string)
    (prepareProviderPrompt: unit -> Task<string>)
    : Task<Result<string, string>> =
    task {
        let owner = SessionId.create ownerSessionKey
        let ownerScope = ReuseScope.ofSession owner
        let completion =
            TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let invocation: SyncDelegateInvocation =
            { Owner = owner
              OwnerScope = ownerScope
              Role = role
              Charge = charge
              PrepareProviderPrompt = prepareProviderPrompt
              Completion = completion }

        let isLeader = store.EnqueueForBatch invocation

        if isLeader then
            let initialBatch = store.DrainBatch(ownerScope, role)

            if List.isEmpty initialBatch then
                store.CompleteBatchPreparation(ownerScope, role)
            else
                let first = List.head initialBatch
                let batchOwner = first.Owner

                if role = SyncDelegateRole.Inspector then
                    let staleDeletedInspector = store.TryTakeDeletedInspector ownerScope

                    staleDeletedInspector
                    |> Option.iter (fun sessionId -> deps.CleanupInspectorDraft(SessionId.value sessionId))

                match deps.ResolveOwnerTier batchOwner with
                | None ->
                    store.CompleteBatchPreparation(ownerScope, role)
                    for item in initialBatch do
                        AsyncSupport.trySetResult item.Completion (Error "sync delegate rejected: owner tier unknown")
                        |> ignore
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
                            store.CompleteBatchPreparation(ownerScope, role)
                            for item in initialBatch do
                                AsyncSupport.trySetResult item.Completion (Error error) |> ignore
                        | Ok delegateSession ->
                            let extraBatch = store.DrainBatch(ownerScope, role)
                            let batch = initialBatch @ extraBatch

                            let! preparedPrompts =
                                batch
                                |> List.map (fun item ->
                                    task {
                                        try
                                            return! item.PrepareProviderPrompt ()
                                        with _ ->
                                            return item.Charge
                                    })
                                |> Task.WhenAll

                            let extraBatch2 = store.DrainBatch(ownerScope, role)
                            let fullBatch = batch @ extraBatch2

                            let fullPrompts =
                                (preparedPrompts |> Array.toList)
                                @ (extraBatch2 |> List.map (fun i -> i.Charge))

                            let combinedCharge =
                                fullBatch
                                |> List.map (fun i -> i.Charge)
                                |> String.concat "\n\n"

                            let combinedProviderPrompt =
                                fullPrompts
                                |> String.concat "\n\n"

                            let call, registration =
                                store.BeginCall(batchOwner, ownerScope, role, delegateSession, agentName, fullBatch)

                            use _registration = registration

                            store.CompleteBatchPreparation(ownerScope, role)

                            let request =
                                SyncDelegatePrompt.withProviderPrompt combinedCharge combinedProviderPrompt

                            match! deps.SendPrompt call request with
                            | Error error ->
                                store.FailCall(call, error)
                            | Ok _ ->
                                if role = SyncDelegateRole.Inspector then
                                    deps.NoteInspectorPrompt (SessionId.value delegateSession) request.Charge

                                let! answered =
                                    CausalAwait.awaitTask
                                        CausalWaitHub.observer
                                        (deps.DescribeWait(DelegateCompletion(batchOwner, delegateSession, role)))
                                        call.Answer.Task

                                for item in fullBatch do
                                    AsyncSupport.trySetResult item.Completion answered |> ignore
                    with ex ->
                        store.CompleteBatchPreparation(ownerScope, role)
                        for item in initialBatch do
                            AsyncSupport.trySetResult item.Completion (Error ex.Message) |> ignore

        return! completion.Task
    }
