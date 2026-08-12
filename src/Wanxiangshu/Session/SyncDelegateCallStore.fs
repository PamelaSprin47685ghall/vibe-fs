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
      Answer: TaskCompletionSource<Result<string, string>> }

/// EXEC-026/031: process-local SyncDelegateRuntime state. Gate-locked.
type internal SyncDelegateCallStore() as this =
    let gate = obj ()
    let callsByOwnerScope = Dictionary<string, SyncDelegateCall>()
    let callsByDelegate = Dictionary<string, SyncDelegateCall>()
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

    member _.FailCall(call: SyncDelegateCall, error: string) =
        AsyncSupport.trySetResult call.Answer (Error error) |> ignore

    member _.RemoveCall(call: SyncDelegateCall) =
        lock gate (fun () ->
            callsByOwnerScope.Remove(scopeKey call.OwnerScope) |> ignore
            callsByDelegate.Remove(sessionKey call.Delegate) |> ignore)

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
        let answer =
            TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let call =
            { Owner = owner
              OwnerScope = ownerScope
              Role = role
              Delegate = delegateSession
              Agent = agent
              Answer = answer }

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
                            | true, current when Object.ReferenceEquals(current.Answer, call.Answer) ->
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
            deletedInspectorsByOwnerScope.Clear()
            inFlightScopes.Clear()
            retiredInspectors)
