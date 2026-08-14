namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
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
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Foundation.Identity

/// AGENT-028 / FALLBACK-014: Authority Root resolve-once; child inherits owner.
/// Persona matrix lives in PersonaCatalog; this module only binds process-local facts.
[<RequireQualifiedAccess>]
module PersonaBinding =

    /// Authority Root: Role × initial SelectedTier → SessionPersona (bind-once).
    let ensureFromAuthority (profile: PromptAuthority.AuthorityExecutionProfile) : string =
        let persona = PersonaCatalog.persona profile.CanonicalRole profile.SelectedTier

        match SessionPersona.bindOnce profile.SessionId persona with
        | Ok bound -> bound
        | Error msg -> raise (InvalidOperationException msg)

    /// InternalLeaf / attached child: inherit owner SessionPersona; never re-read tier.
    let ensureInherited (ownerId: SessionId) (childId: SessionId) : string =
        let ownerPersona =
            match SessionPersona.tryGet ownerId with
            | Some persona -> persona
            | None ->
                raise (
                    InvalidOperationException(
                        sprintf
                            "owner %s has no SessionPersona; child %s cannot inherit (AGENT-028)"
                            (SessionId.value ownerId)
                            (SessionId.value childId)
                    )
                )

        match SessionPersona.inheritFromOwner ownerPersona childId with
        | Ok persona -> persona
        | Error msg -> raise (InvalidOperationException msg)
