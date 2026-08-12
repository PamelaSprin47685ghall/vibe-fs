namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// In-flight sync delegate answer (ordinary completion → WorkRecord).
type internal SyncDelegateCall =
    { Owner: SessionId
      OwnerScope: ReuseScopeId
      Role: SyncDelegateRole
      Delegate: SessionId
      Agent: string
      Invocations: SyncDelegateInvocation list
      Answer: TaskCompletionSource<Result<string, string>> }

/// A single caller's pending invocation to a sync delegate.
and internal SyncDelegateInvocation =
    { Owner: SessionId
      OwnerScope: ReuseScopeId
      Role: SyncDelegateRole
      Charge: string
      PrepareProviderPrompt: unit -> Task<string>
      Completion: TaskCompletionSource<Result<string, string>> }

/// EXEC-026/031: process-local SyncDelegateRuntime state. Gate-locked.
type internal SyncDelegateCallStore() as this =
    let gate = obj ()
    // DSL-MUTABLE: resource — live calls by owner scope key
    let callsByOwnerScope = Dictionary<string, ResizeArray<SyncDelegateCall>>()
    // DSL-MUTABLE: resource — live calls by delegate session key (FIFO queue)
    let callsByDelegate = Dictionary<string, Queue<SyncDelegateCall>>()
    // DSL-MUTABLE: resource — retired Inspector ids staged between child and owner SessionDeleted
    let deletedInspectorsByOwnerScope = Dictionary<string, SessionId>()
    // DSL-MUTABLE: mailbox — pending sync delegate invocations being batched by (scopeKey, role)
    let pendingBatches =
        Dictionary<string * SyncDelegateRole, ResizeArray<SyncDelegateInvocation>>()
    // DSL-MUTABLE: single-flight — active preparing batch keys
    let preparingBatches = HashSet<string * SyncDelegateRole>()

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId
    let scopeKey (scope: ReuseScopeId) = ReuseScopeId.value scope

    member _.TryPeekCallByDelegate(delegateSession: SessionId) : SyncDelegateCall option =
        lock gate (fun () ->
            let key = sessionKey delegateSession

            match callsByDelegate.TryGetValue key with
            | true, q when q.Count > 0 -> Some(q.Peek())
            | _ -> None)

    member _.TryPopCallByDelegate(delegateSession: SessionId) : SyncDelegateCall option =
        lock gate (fun () ->
            let key = sessionKey delegateSession

            match callsByDelegate.TryGetValue key with
            | true, q when q.Count > 0 ->
                let call = q.Dequeue()

                if q.Count = 0 then
                    callsByDelegate.Remove key |> ignore

                let ownerKey = scopeKey call.OwnerScope

                match callsByOwnerScope.TryGetValue ownerKey with
                | true, list ->
                    list.Remove call |> ignore

                    if list.Count = 0 then
                        callsByOwnerScope.Remove ownerKey |> ignore
                | false, _ -> ()

                Some call
            | _ -> None)

    member _.FailCall(call: SyncDelegateCall, error: string) =
        AsyncSupport.trySetResult call.Answer (Error error) |> ignore

        for inv in call.Invocations do
            AsyncSupport.trySetResult inv.Completion (Error error) |> ignore

    member _.EnqueueForBatch(invocation: SyncDelegateInvocation) : bool =
        lock gate (fun () ->
            let key = (scopeKey invocation.OwnerScope, invocation.Role)

            let list =
                match pendingBatches.TryGetValue key with
                | true, l -> l
                | false, _ ->
                    let l = ResizeArray<SyncDelegateInvocation>()
                    pendingBatches.[key] <- l
                    l

            list.Add invocation

            if preparingBatches.Contains key then
                false
            else
                preparingBatches.Add key |> ignore
                true)

    member _.DrainBatch(ownerScope: ReuseScopeId, role: SyncDelegateRole) : SyncDelegateInvocation list =
        lock gate (fun () ->
            let key = (scopeKey ownerScope, role)

            match pendingBatches.TryGetValue key with
            | true, list ->
                let items = list |> Seq.toList
                list.Clear()
                items
            | false, _ -> [])

    member _.CompleteBatchPreparation(ownerScope: ReuseScopeId, role: SyncDelegateRole) =
        lock gate (fun () ->
            let key = (scopeKey ownerScope, role)
            preparingBatches.Remove key |> ignore
            pendingBatches.Remove key |> ignore)

    member _.CancelScope(scope: ReuseScopeId) =
        lock gate (fun () ->
            let sKey = scopeKey scope

            let matchingBatchKeys =
                pendingBatches.Keys |> Seq.filter (fun (sk, _) -> sk = sKey) |> Seq.toList

            for key in matchingBatchKeys do
                match pendingBatches.TryGetValue key with
                | true, list ->
                    for item in list do
                        AsyncSupport.trySetResult item.Completion (Error "Sync delegate call was cancelled")
                        |> ignore

                    pendingBatches.Remove key |> ignore
                | false, _ -> ()

                preparingBatches.Remove key |> ignore

            match callsByOwnerScope.TryGetValue sKey with
            | true, list ->
                let calls = list |> Seq.toList
                callsByOwnerScope.Remove sKey |> ignore

                for call in calls do
                    let dKey = sessionKey call.Delegate

                    match callsByDelegate.TryGetValue dKey with
                    | true, q ->
                        let remaining =
                            q
                            |> Seq.filter (fun c -> not (Object.ReferenceEquals(c.Answer, call.Answer)))
                            |> Seq.toList

                        q.Clear()

                        for r in remaining do
                            q.Enqueue r

                        if q.Count = 0 then
                            callsByDelegate.Remove dKey |> ignore
                    | false, _ -> ()

                    this.FailCall(call, "Sync delegate call was cancelled")
            | false, _ -> ())

    member _.BeginCall
        (
            owner: SessionId,
            ownerScope: ReuseScopeId,
            role: SyncDelegateRole,
            delegateSession: SessionId,
            agent: string,
            invocations: SyncDelegateInvocation list
        ) : SyncDelegateCall * IDisposable =
        let answer =
            TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let call =
            { Owner = owner
              OwnerScope = ownerScope
              Role = role
              Delegate = delegateSession
              Agent = agent
              Invocations = invocations
              Answer = answer }

        let ownerKey = scopeKey ownerScope
        let delegateKey = sessionKey delegateSession

        lock gate (fun () ->
            let ownerCalls =
                match callsByOwnerScope.TryGetValue ownerKey with
                | true, list -> list
                | false, _ ->
                    let list = ResizeArray<SyncDelegateCall>()
                    callsByOwnerScope.[ownerKey] <- list
                    list

            ownerCalls.Add call

            let delegateQueue =
                match callsByDelegate.TryGetValue delegateKey with
                | true, q -> q
                | false, _ ->
                    let q = Queue<SyncDelegateCall>()
                    callsByDelegate.[delegateKey] <- q
                    q

            delegateQueue.Enqueue call)

        let registration =
            { new IDisposable with
                member _.Dispose() =
                    let stillOwned =
                        lock gate (fun () ->
                            match callsByOwnerScope.TryGetValue ownerKey with
                            | true, list ->
                                let removed = list.Remove call

                                if list.Count = 0 then
                                    callsByOwnerScope.Remove ownerKey |> ignore

                                match callsByDelegate.TryGetValue delegateKey with
                                | true, q ->
                                    let remaining =
                                        q
                                        |> Seq.filter (fun c -> not (Object.ReferenceEquals(c.Answer, call.Answer)))
                                        |> Seq.toList

                                    q.Clear()

                                    for r in remaining do
                                        q.Enqueue r

                                    if q.Count = 0 then
                                        callsByDelegate.Remove delegateKey |> ignore
                                | false, _ -> ()

                                removed
                            | false, _ -> false)

                    if stillOwned then
                        this.FailCall(call, "Sync delegate call scope disposed") }

        call, registration

    member _.TryTakeDeletedInspector(scope: ReuseScopeId) : SessionId option =
        lock gate (fun () ->
            let key = scopeKey scope

            match deletedInspectorsByOwnerScope.TryGetValue key with
            | true, sessionId ->
                deletedInspectorsByOwnerScope.Remove key |> ignore
                Some sessionId
            | false, _ -> None)

    member _.TryGetDeletedInspector(scope: ReuseScopeId) : SessionId option =
        lock gate (fun () ->
            match deletedInspectorsByOwnerScope.TryGetValue(scopeKey scope) with
            | true, sessionId -> Some sessionId
            | false, _ -> None)

    member _.PutDeletedInspector(scope: ReuseScopeId, inspectorSessionId: SessionId) : SessionId option =
        lock gate (fun () ->
            let key = scopeKey scope

            let previous =
                match deletedInspectorsByOwnerScope.TryGetValue key with
                | true, sessionId -> Some sessionId
                | false, _ -> None

            deletedInspectorsByOwnerScope.[key] <- inspectorSessionId
            previous)

    member _.ClearDeletedInspector(scope: ReuseScopeId) : SessionId option =
        lock gate (fun () ->
            let key = scopeKey scope

            match deletedInspectorsByOwnerScope.TryGetValue key with
            | true, inspectorId ->
                deletedInspectorsByOwnerScope.Remove key |> ignore
                Some inspectorId
            | false, _ -> None)

    /// Dispose path: fail every live call, clear all indexes; returns retired
    /// inspector ids so the owner can clean up their drafts.
    member _.ClearAll() : SessionId list =
        lock gate (fun () ->
            for list in pendingBatches.Values do
                for item in list do
                    AsyncSupport.trySetResult item.Completion (Error "SyncDelegate runtime disposed")
                    |> ignore

            pendingBatches.Clear()
            preparingBatches.Clear()

            for list in callsByOwnerScope.Values do
                for call in list do
                    this.FailCall(call, "SyncDelegate runtime disposed")

            let retiredInspectors = deletedInspectorsByOwnerScope.Values |> Seq.toList

            callsByOwnerScope.Clear()
            callsByDelegate.Clear()
            deletedInspectorsByOwnerScope.Clear()
            retiredInspectors)
