namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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
/// DSL-state-combination: physical — StartCursor is a process-local resource coordinate fixed after the delegate session exists.
and internal SyncDelegateInvocation =
    {
        Owner: SessionId
        OwnerScope: ReuseScopeId
        Role: SyncDelegateRole
        Charge: string
        ExpectedToolCalls: int option
        PrepareProviderPrompt: unit -> Task<string>
        Batch: SyncDelegateBatch option
        Completion: TaskCompletionSource<Result<SyncDelegateInvocationResult, string>>
        /// EXEC-031: XTrace head (one-past last part, 0 when empty) captured at
        /// send. Inclusive start of the per-batch WorkRecord range; the
        /// exclusive end is the same head captured at completion.
        mutable StartCursor: int64 option
    }

type private PendingBatch =
    { ProviderRun: ProviderRunIdentity
      CallOrder: ToolCallId list
      Items: Dictionary<string, SyncDelegateInvocation> }

[<RequireQualifiedAccess>]
type internal SyncDelegateAdmission =
    | Waiting
    | Ready of SyncDelegateInvocation list
    | Rejected of string

/// EXEC-026/031: process-local SyncDelegateRuntime state. Semantic batches are
/// defined by ProviderRun + provider tool-call order, never scheduler timing.
type internal SyncDelegateCallStore() as this =
    let gate = obj ()
    // DSL-MUTABLE: resource — live calls by owner scope key
    let callsByOwnerScope = Dictionary<string, ResizeArray<SyncDelegateCall>>()
    // DSL-MUTABLE: resource — at most one live call per dedicated delegate session
    let callsByDelegate = Dictionary<string, SyncDelegateCall>()
    // DSL-MUTABLE: resource — retired Inspector ids staged between child and owner SessionDeleted
    let deletedInspectorsByOwnerScope = Dictionary<string, SessionId>()
    // DSL-MUTABLE: mailbox — incomplete semantic batches by (scope, role)
    let pendingBatches = Dictionary<string * SyncDelegateRole, PendingBatch>()
    // DSL-MUTABLE: reservation — complete/admitted batch through ordinary completion
    let activeBatches =
        Dictionary<string * SyncDelegateRole, SyncDelegateInvocation list>()

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId
    let scopeKey (scope: ReuseScopeId) = ReuseScopeId.value scope
    let keyOf (scope: ReuseScopeId) role = scopeKey scope, role
    let callKey (callId: ToolCallId) = ToolCallId.value callId

    let sameCallOrder left right =
        List.map callKey left = List.map callKey right

    let failInvocation (invocation: SyncDelegateInvocation) error =
        AsyncSupport.trySetResult invocation.Completion (Error error) |> ignore

    member _.TryPeekCallByDelegate(delegateSession: SessionId) : SyncDelegateCall option =
        lock gate (fun () ->
            match callsByDelegate.TryGetValue(sessionKey delegateSession) with
            | true, call -> Some call
            | false, _ -> None)

    member _.TryPopCallByDelegate(delegateSession: SessionId) : SyncDelegateCall option =
        lock gate (fun () ->
            let delegateKey = sessionKey delegateSession

            match callsByDelegate.TryGetValue delegateKey with
            | true, call ->
                callsByDelegate.Remove delegateKey |> ignore
                let ownerKey = scopeKey call.OwnerScope

                match callsByOwnerScope.TryGetValue ownerKey with
                | true, list ->
                    list.Remove call |> ignore

                    if list.Count = 0 then
                        callsByOwnerScope.Remove ownerKey |> ignore
                | false, _ -> ()

                Some call
            | false, _ -> None)

    member _.FailCall(call: SyncDelegateCall, error: string) =
        AsyncSupport.trySetResult call.Answer (Error error) |> ignore

    member _.Admit(invocation: SyncDelegateInvocation) : SyncDelegateAdmission =
        lock gate (fun () ->
            let key = keyOf invocation.OwnerScope invocation.Role

            if activeBatches.ContainsKey key then
                SyncDelegateAdmission.Rejected "sync delegate rejected: dedicated delegate already has an active batch"
            else
                match invocation.Batch with
                | None ->
                    if pendingBatches.ContainsKey key then
                        SyncDelegateAdmission.Rejected "sync delegate rejected: semantic batch already pending"
                    else
                        activeBatches.[key] <- [ invocation ]
                        SyncDelegateAdmission.Ready [ invocation ]
                | Some batch ->
                    let orderedKeys = batch.CallOrder |> List.map callKey
                    let currentKey = callKey batch.CurrentCall

                    if
                        List.isEmpty orderedKeys
                        || Set.count (Set.ofList orderedKeys) <> List.length orderedKeys
                        || not (List.contains currentKey orderedKeys)
                    then
                        SyncDelegateAdmission.Rejected "sync delegate rejected: invalid ProviderRun batch"
                    else
                        let pending =
                            match pendingBatches.TryGetValue key with
                            | true, existing when
                                ProviderRunIdentity.value existing.ProviderRun = ProviderRunIdentity.value
                                                                                     batch.ProviderRun
                                && sameCallOrder existing.CallOrder batch.CallOrder
                                ->
                                Ok existing
                            | true, _ -> Error "sync delegate rejected: another ProviderRun batch is pending"
                            | false, _ ->
                                let created =
                                    { ProviderRun = batch.ProviderRun
                                      CallOrder = batch.CallOrder
                                      Items = Dictionary<string, SyncDelegateInvocation>() }

                                pendingBatches.[key] <- created
                                Ok created

                        match pending with
                        | Error error -> SyncDelegateAdmission.Rejected error
                        | Ok batchState when batchState.Items.ContainsKey currentKey ->
                            SyncDelegateAdmission.Rejected "sync delegate rejected: duplicate ToolCallId in batch"
                        | Ok batchState ->
                            batchState.Items.[currentKey] <- invocation

                            if batchState.Items.Count <> batchState.CallOrder.Length then
                                SyncDelegateAdmission.Waiting
                            else
                                let ordered =
                                    batchState.CallOrder
                                    |> List.map (fun callId -> batchState.Items.[callKey callId])

                                pendingBatches.Remove key |> ignore
                                activeBatches.[key] <- ordered
                                SyncDelegateAdmission.Ready ordered)

    member _.ReleaseAdmission(ownerScope: ReuseScopeId, role: SyncDelegateRole) =
        lock gate (fun () -> activeBatches.Remove(keyOf ownerScope role) |> ignore)

    member _.CancelScope(scope: ReuseScopeId) =
        lock gate (fun () ->
            let sKey = scopeKey scope

            let pendingKeys =
                pendingBatches.Keys |> Seq.filter (fun (scope, _) -> scope = sKey) |> Seq.toList

            for key in pendingKeys do
                let pending = pendingBatches.[key]

                for invocation in pending.Items.Values do
                    failInvocation invocation "Sync delegate call was cancelled"

                pendingBatches.Remove key |> ignore

            let activeKeys =
                activeBatches.Keys |> Seq.filter (fun (scope, _) -> scope = sKey) |> Seq.toList

            for key in activeKeys do
                for invocation in activeBatches.[key] do
                    failInvocation invocation "Sync delegate call was cancelled"

                activeBatches.Remove key |> ignore

            match callsByOwnerScope.TryGetValue sKey with
            | true, list ->
                let calls = list |> Seq.toList
                callsByOwnerScope.Remove sKey |> ignore

                for call in calls do
                    callsByDelegate.Remove(sessionKey call.Delegate) |> ignore
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
        ) : Result<SyncDelegateCall * IDisposable, string> =
        let ownerKey = scopeKey ownerScope
        let delegateKey = sessionKey delegateSession
        let activeKey = keyOf ownerScope role

        lock gate (fun () ->
            if not (activeBatches.ContainsKey activeKey) then
                Error "sync delegate call was cancelled before dispatch"
            elif callsByDelegate.ContainsKey delegateKey then
                Error "sync delegate rejected: dedicated delegate already has an in-flight call"
            else
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

                let ownerCalls =
                    match callsByOwnerScope.TryGetValue ownerKey with
                    | true, list -> list
                    | false, _ ->
                        let list = ResizeArray<SyncDelegateCall>()
                        callsByOwnerScope.[ownerKey] <- list
                        list

                ownerCalls.Add call
                callsByDelegate.[delegateKey] <- call

                let registration =
                    { new IDisposable with
                        member _.Dispose() =
                            let stillOwned =
                                lock gate (fun () ->
                                    activeBatches.Remove activeKey |> ignore

                                    let removed =
                                        match callsByOwnerScope.TryGetValue ownerKey with
                                        | true, list ->
                                            let wasRemoved = list.Remove call

                                            if list.Count = 0 then
                                                callsByOwnerScope.Remove ownerKey |> ignore

                                            wasRemoved
                                        | false, _ -> false

                                    match callsByDelegate.TryGetValue delegateKey with
                                    | true, current when Object.ReferenceEquals(current.Answer, call.Answer) ->
                                        callsByDelegate.Remove delegateKey |> ignore
                                    | _ -> ()

                                    removed)

                            if stillOwned then
                                this.FailCall(call, "Sync delegate call scope disposed") }

                Ok(call, registration))

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

    /// Dispose path: fail every live/pending invocation and clear all indexes.
    member _.ClearAll() : SessionId list =
        lock gate (fun () ->
            for pending in pendingBatches.Values do
                for invocation in pending.Items.Values do
                    failInvocation invocation "SyncDelegate runtime disposed"

            for invocations in activeBatches.Values do
                for invocation in invocations do
                    failInvocation invocation "SyncDelegate runtime disposed"

            for list in callsByOwnerScope.Values do
                for call in list do
                    this.FailCall(call, "SyncDelegate runtime disposed")

            let retiredInspectors = deletedInspectorsByOwnerScope.Values |> Seq.toList

            pendingBatches.Clear()
            activeBatches.Clear()
            callsByOwnerScope.Clear()
            callsByDelegate.Clear()
            deletedInspectorsByOwnerScope.Clear()
            retiredInspectors)
