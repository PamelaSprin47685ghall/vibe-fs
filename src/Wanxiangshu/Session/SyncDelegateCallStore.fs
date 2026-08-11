namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// In-flight sync delegate answer (Returned → Completion).
type internal SyncDelegateAnswer =
    { Answer: string
      ToolRun: ProviderRunIdentity }

type internal SyncDelegateCall =
    { Owner: SessionId
      OwnerScope: ReuseScopeId
      Role: SyncDelegateRole
      Delegate: SessionId
      Agent: string
      Returned: TaskCompletionSource<Result<SyncDelegateAnswer, string>>
      Completion: TaskCompletionSource<Result<unit, string>>
      Nudges: int }

/// TextComplete rewrite arm only — presence must not select HandleTurn branches.
type internal PendingSyncCompletionText =
    { Text: string
      ToolRun: ProviderRunIdentity }

/// EXEC-026/028: process-local SyncDelegateRuntime state. Gate-locked.
type internal SyncDelegateCallStore() as this =
    let gate = obj ()
    let callsByOwnerScope = Dictionary<string, SyncDelegateCall>()
    let callsByDelegate = Dictionary<string, SyncDelegateCall>()
    let pendingCompletionTexts = Dictionary<string, PendingSyncCompletionText>()
    // Retired Inspector ids staged between child and owner SessionDeleted.
    let deletedInspectorsByOwnerScope = Dictionary<string, SessionId>()
    let inFlightScopes = HashSet<string>()

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId
    let scopeKey (scope: ReuseScopeId) = ReuseScopeId.value scope

    member _.TryCallByOwnerScope(scope: ReuseScopeId) : SyncDelegateCall option =
        lock gate (fun () ->
            match callsByOwnerScope.TryGetValue(scopeKey scope) with
            | true, call -> Some call
            | false, _ -> None)

    member _.TryCallByDelegate(delegateSession: SessionId) : SyncDelegateCall option =
        lock gate (fun () ->
            match callsByDelegate.TryGetValue(sessionKey delegateSession) with
            | true, call -> Some call
            | false, _ -> None)

    member _.TryPendingText(sessionId: SessionId) : PendingSyncCompletionText option =
        lock gate (fun () ->
            match pendingCompletionTexts.TryGetValue(sessionKey sessionId) with
            | true, pending -> Some pending
            | false, _ -> None)

    member _.ArmPendingText(sessionId: SessionId, pending: PendingSyncCompletionText) =
        lock gate (fun () -> pendingCompletionTexts.[sessionKey sessionId] <- pending)

    member _.ClearPendingText(sessionId: SessionId) =
        lock gate (fun () -> pendingCompletionTexts.Remove(sessionKey sessionId) |> ignore)

    member _.FailCall(call: SyncDelegateCall, error: string) =
        AsyncSupport.trySetResult call.Returned (Error error) |> ignore
        AsyncSupport.trySetResult call.Completion (Error error) |> ignore

    member _.RemoveCall(call: SyncDelegateCall) =
        lock gate (fun () ->
            callsByOwnerScope.Remove(scopeKey call.OwnerScope) |> ignore
            callsByDelegate.Remove(sessionKey call.Delegate) |> ignore)

    member _.UpdateCall
        (ownerScope: ReuseScopeId, update: SyncDelegateCall -> SyncDelegateCall)
        : SyncDelegateCall option =
        lock gate (fun () ->
            let key = scopeKey ownerScope

            match callsByOwnerScope.TryGetValue key with
            | true, current ->
                let next = update current
                callsByOwnerScope.[key] <- next
                callsByDelegate.[sessionKey next.Delegate] <- next
                Some next
            | false, _ -> None)

    member _.TryAcquireFlight(scope: ReuseScopeId) : bool =
        lock gate (fun () ->
            let key = scopeKey scope

            if inFlightScopes.Contains key then
                false
            else
                inFlightScopes.Add key |> ignore
                true)

    member _.ReleaseFlight(scope: ReuseScopeId) =
        lock gate (fun () -> inFlightScopes.Remove(scopeKey scope) |> ignore)

    member _.BeginCall
        (owner: SessionId, ownerScope: ReuseScopeId, role: SyncDelegateRole, delegateSession: SessionId, agent: string) : SyncDelegateCall *
                                                                                                                          IDisposable
        =
        let returned =
            TaskCompletionSource<Result<SyncDelegateAnswer, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let completion =
            TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let call =
            { Owner = owner
              OwnerScope = ownerScope
              Role = role
              Delegate = delegateSession
              Agent = agent
              Returned = returned
              Completion = completion
              Nudges = 0 }

        let ownerKey = scopeKey ownerScope
        let delegateKey = sessionKey delegateSession

        lock gate (fun () ->
            callsByOwnerScope.[ownerKey] <- call
            callsByDelegate.[delegateKey] <- call)

        let registration =
            { new IDisposable with
                member _.Dispose() =
                    let stillOwned =
                        lock gate (fun () ->
                            match callsByOwnerScope.TryGetValue ownerKey with
                            | true, current when Object.ReferenceEquals(current.Returned, call.Returned) ->
                                callsByOwnerScope.Remove ownerKey |> ignore
                                callsByDelegate.Remove delegateKey |> ignore
                                true
                            | _ -> false)

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
            for call in callsByOwnerScope.Values |> Seq.toList do
                this.FailCall(call, "SyncDelegate runtime disposed")

            let retiredInspectors = deletedInspectorsByOwnerScope.Values |> Seq.toList

            callsByOwnerScope.Clear()
            callsByDelegate.Clear()
            pendingCompletionTexts.Clear()
            deletedInspectorsByOwnerScope.Clear()
            inFlightScopes.Clear()
            retiredInspectors)
