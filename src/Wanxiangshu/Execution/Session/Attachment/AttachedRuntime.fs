namespace Wanxiangshu.Execution.Session.Attachment

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HOST-008 / EXEC-026: in-process bindings for Work+Attached SyncDelegate sessions.
/// Keyed by `(OwnerReuseScopeId, SyncDelegateRole)` — at most one live dedicated Session.
/// Does not use SatelliteRuntime / SatelliteKind (those remain Companion/Teacher only).
type AttachedSessionRuntime(?registerParent: SessionId -> SessionId -> unit, ?isUsable: SessionId -> bool) =
    let gate = obj ()
    let bindings = Dictionary<string, SessionId * string>()
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

    member _.TryFind(ownerSessionId: SessionId, role: SyncDelegateRole) : SessionId option =
        let scope = ReuseScope.ofSession ownerSessionId
        lock gate (fun () -> tryGetLocked scope role |> Option.map fst)

    member _.TryFindByScope(scope: ReuseScopeId, role: SyncDelegateRole) : SessionId option =
        lock gate (fun () -> tryGetLocked scope role |> Option.map fst)

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
            createChild: SessionId -> string -> string option -> Task<Result<SessionId, string>>,
            onReady: SessionId -> string -> unit
        ) : Task<Result<SessionId * string, string>> =
        task {
            let scope = ReuseScope.ofSession ownerSessionId
            let key = bindingKey scope role

            let existing = lock gate (fun () -> tryGetLocked scope role)

            match existing with
            | Some(sessionId, boundAgent) -> return Ok(sessionId, boundAgent)
            | None ->
                match! createChild ownerSessionId agentName directory with
                | Error error -> return Error error
                | Ok childId ->
                    register ownerSessionId childId
                    onReady childId agentName
                    lock gate (fun () -> bindings.[key] <- (childId, agentName))
                    return Ok(childId, agentName)
        }

    member _.Clear() = lock gate (fun () -> bindings.Clear())
