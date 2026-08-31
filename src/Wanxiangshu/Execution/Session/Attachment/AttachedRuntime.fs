namespace Wanxiangshu.Execution.Session.Attachment

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type AttachedChildObservation =
    | Missing
    | Matching of SessionId
    | Conflicting of SessionId list

[<RequireQualifiedAccess>]
type AttachedChildDecision =
    | Create
    | Adopt of SessionId
    | RejectConflict of SessionId list

[<RequireQualifiedAccess>]
module AttachedChildObservation =
    let decide =
        function
        | AttachedChildObservation.Missing -> AttachedChildDecision.Create
        | AttachedChildObservation.Matching childId -> AttachedChildDecision.Adopt childId
        | AttachedChildObservation.Conflicting children -> AttachedChildDecision.RejectConflict children

[<RequireQualifiedAccess>]
type private AttachedRuntimePlan =
    | Reuse of SessionId * string
    | Attach of AttachedChildDecision

type private ObserveAttachedChild =
    SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> Task<Result<AttachedChildObservation, string>>

type private CreateAttachedChild =
    SessionId -> ReuseScopeId -> SyncDelegateRole -> string -> string option -> Task<Result<SessionId, string>>

[<RequireQualifiedAccess>]
type private AttachedFlightClaim =
    | Follow of Task<Result<SessionId * string, string>>
    | Own of TaskCompletionSource<Result<SessionId * string, string>>

/// HOST-008 / EXEC-026: in-process bindings for Work+Attached SyncDelegate sessions.
/// Keyed by `(OwnerReuseScopeId, SyncDelegateRole)` — at most one live dedicated Session.
/// Does not use SatelliteRuntime / SatelliteKind (those remain Companion only).
type AttachedSessionRuntime(?registerParent: SessionId -> SessionId -> unit, ?isUsable: SessionId -> bool) =
    let gate = obj ()
    /// DSL-cross-callback-proof: physical resource — reusable dedicated child identity binding
    // DSL-MUTABLE: resource — attached session binding registry by scope+role
    let bindings = Dictionary<string, SessionId * string>()

    /// DSL-cross-callback-proof: physical single-flight — the entire Host
    /// observe/create/adopt/bind transaction is shared by exact scope+role.
    // DSL-MUTABLE: resource — in-flight attached reconciliation by scope+role
    let flights =
        Dictionary<string, TaskCompletionSource<Result<SessionId * string, string>>>()

    let register = defaultArg registerParent (fun _ _ -> ())
    let usable = defaultArg isUsable (fun _ -> true)

    let roleLabel = SyncDelegate.roleLabel

    let bindingKey (scope: ReuseScopeId) (role: SyncDelegateRole) =
        ReuseScopeId.value scope + "\u001f" + roleLabel role

    let tryGetLocked (scope: ReuseScopeId) (role: SyncDelegateRole) =
        match bindings.TryGetValue(bindingKey scope role) with
        | true, (sessionId, agent) when usable sessionId -> Some(sessionId, agent)
        | true, _ -> None
        | false, _ -> None

    let collectPlan
        (scope: ReuseScopeId)
        (role: SyncDelegateRole)
        (existing: (SessionId * string) option)
        (ownerSessionId: SessionId)
        (agentName: string)
        (observeChild: ObserveAttachedChild)
        =
        taskResult {
            match existing with
            | Some(sessionId, boundAgent) -> return AttachedRuntimePlan.Reuse(sessionId, boundAgent)
            | None ->
                let! observation = observeChild ownerSessionId scope role agentName
                return AttachedRuntimePlan.Attach(AttachedChildObservation.decide observation)
        }

    let finishFlight key (flight: TaskCompletionSource<Result<SessionId * string, string>>) =
        lock gate (fun () ->
            match flights.TryGetValue key with
            | true, current when obj.ReferenceEquals(current, flight) -> flights.Remove key |> ignore
            | _ -> ())

    let claimFlight key =
        lock gate (fun () ->
            match flights.TryGetValue key with
            | true, current -> AttachedFlightClaim.Follow current.Task
            | false, _ ->
                let created =
                    TaskCompletionSource<Result<SessionId * string, string>>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    )

                flights.Add(key, created)
                AttachedFlightClaim.Own created)

    let reconcile
        key
        scope
        role
        ownerSessionId
        agentName
        directory
        (observeChild: ObserveAttachedChild)
        (createChild: CreateAttachedChild)
        bindChild
        onReady
        =
        taskResult {
            let existing = lock gate (fun () -> tryGetLocked scope role)
            let! plan = collectPlan scope role existing ownerSessionId agentName observeChild

            let bindReady childId =
                register ownerSessionId childId
                bindChild ownerSessionId childId agentName
                onReady childId agentName
                lock gate (fun () -> bindings.[key] <- (childId, agentName))
                childId, agentName

            match plan with
            | AttachedRuntimePlan.Reuse(sessionId, boundAgent) -> return sessionId, boundAgent
            | AttachedRuntimePlan.Attach AttachedChildDecision.Create ->
                let! childId = createChild ownerSessionId scope role agentName directory
                return bindReady childId
            | AttachedRuntimePlan.Attach(AttachedChildDecision.Adopt childId) -> return bindReady childId
            | AttachedRuntimePlan.Attach(AttachedChildDecision.RejectConflict children) ->
                return!
                    children
                    |> List.map SessionId.value
                    |> String.concat ", "
                    |> sprintf "sync delegate child observation conflicted: %s"
                    |> Error
                    |> Task.FromResult
        }

    let completeOwnedFlight (flight: TaskCompletionSource<Result<SessionId * string, string>>) work =
        task {
            try
                let! result = work ()
                flight.SetResult result
                return result
            with ex ->
                flight.SetException ex
                return raise ex
        }

    let runOwnedFlight key (flight: TaskCompletionSource<Result<SessionId * string, string>>) work =
        task {
            try
                return! completeOwnedFlight flight work
            finally
                finishFlight key flight
        }

    member _.TryFind(ownerSessionId: SessionId, role: SyncDelegateRole) : SessionId option =
        let scope = ReuseScope.ofSession ownerSessionId
        lock gate (fun () -> tryGetLocked scope role |> Option.map fst)

    member _.TryFindByScope(scope: ReuseScopeId, role: SyncDelegateRole) : SessionId option =
        lock gate (fun () -> tryGetLocked scope role |> Option.map fst)

    member _.TryFindOwner(delegateSessionId: SessionId, role: SyncDelegateRole) : SessionId option =
        let suffix = "\u001f" + roleLabel role

        lock gate (fun () ->
            bindings
            |> Seq.tryPick (fun binding ->
                let delegateId, _ = binding.Value

                if
                    delegateId = delegateSessionId
                    && binding.Key.EndsWith(suffix, StringComparison.Ordinal)
                then
                    Some(SessionId.create (binding.Key.Substring(0, binding.Key.Length - suffix.Length)))
                else
                    None))

    member _.Remove(ownerSessionId: SessionId, role: SyncDelegateRole) : bool =
        let scope = ReuseScope.ofSession ownerSessionId
        lock gate (fun () -> bindings.Remove(bindingKey scope role))

    member _.RemoveByDelegateSession(delegateSessionId: SessionId) : bool =
        lock gate (fun () ->
            let doomed =
                bindings
                |> Seq.tryFind (fun kv -> fst kv.Value = delegateSessionId)
                |> Option.map (fun kv -> kv.Key)

            match doomed with
            | None -> false
            | Some key -> bindings.Remove key)

    /// Reuse an existing compatible binding, or create a Work child and bind it.
    /// `createChild ownerSessionId agentName directory` must CreateChildSession as a
    /// Work child with `Agent = Some agentName` (not a SatelliteKind leaf).
    ///
    /// On reuse the returned agent is the one stored at create time. A later
    /// owner-tier lookup must not overwrite a Deep child with Fast.
    member _.GetOrCreate
        (
            ownerSessionId: SessionId,
            role: SyncDelegateRole,
            agentName: string,
            directory: string option,
            observeChild: ObserveAttachedChild,
            createChild: CreateAttachedChild,
            bindChild: SessionId -> SessionId -> string -> unit,
            onReady: SessionId -> string -> unit
        ) : Task<Result<SessionId * string, string>> =
        let scope = ReuseScope.ofSession ownerSessionId
        let key = bindingKey scope role

        match claimFlight key with
        | AttachedFlightClaim.Follow flight -> flight
        | AttachedFlightClaim.Own flight ->
            runOwnedFlight key flight (fun () ->
                reconcile key scope role ownerSessionId agentName directory observeChild createChild bindChild onReady)

    member _.Clear() = lock gate (fun () -> bindings.Clear())
