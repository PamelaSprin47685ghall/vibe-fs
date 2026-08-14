namespace Wanxiangshu.Change.Host

open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
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

module OrchestratorSessionDirectories =
    let registerRestored
        (snapshot: ProjectionSet)
        (orchestratorId: SessionId)
        (worktrees: Dictionary<string, string>)
        (register: SessionId -> string -> unit)
        (registerReviewerTree: string -> GitTreePort -> unit)
        =
        match Map.tryFind orchestratorId snapshot.AgentProjections.Sessions with
        | Some session ->
            match session.Handles with
            | Some handles ->
                for record in HandleProjection.linkedChildren handles do
                    // `worktrees` is keyed by the runtime agent id, which for an agent
                    // child IS the handle's inner id. PTY and ManagerJob handles have
                    // no agent id and no worktree entry, so they are skipped rather
                    // than rendered into a lookup key.
                    match HandleId.tryAgent record.Handle with
                    | None -> ()
                    | Some agentHandle ->
                        match worktrees.TryGetValue(AgentHandleId.value agentHandle) with
                        | true, path ->
                            register record.ChildSessionId path

                            // CanonicalRole is the durable role the fork selected.
                            // The previous version consulted a separate `LinkedRoles`
                            // map, which could disagree with the handle it described.
                            // Typed comparison, not a case-insensitive string match:
                            // the role is a `Role`, so a spelling drift is a compile
                            // error rather than a reviewer tree that silently stops
                            // being registered.
                            match record.CanonicalRole with
                            | Role.Reviewer ->
                                registerReviewerTree (SessionId.value record.ChildSessionId) (GitTree.create path)
                            | _ -> ()
                        | false, _ -> ()
            | None -> ()
        | None -> ()
