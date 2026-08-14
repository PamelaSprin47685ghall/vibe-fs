namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider.Attempt.Fallback

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
open Wanxiangshu.Foundation

/// SSOT: `Role` lives in `Kernel/Roles.fs`. This module only provides
/// Host-wire parsing and the canonical durable role label.
module AgentRoleIdentity =

    let ofRole (role: Role) : Role = role

    /// Host composition passes `managed.Role`; Session never depends on OpenCode types.
    let ofManaged (role: Role) : Role = role

    let toRole (role: Role) : Role = role

    /// Host wire names (`fast-manager`) parse via Domain SSOT; bare role labels fall back to catalog.
    let roleOfString (value: string) : Role option =
        if String.IsNullOrWhiteSpace value then
            None
        else
            match PromptAuthority.parseAgentNameTyped value with
            | Ok parsed -> Some parsed.Role
            | Error _ -> ManagedAgentCatalog.tryParseRole (value.Trim().ToLowerInvariant())

    /// The canonical role label persisted in durable facts.
    ///
    /// Delegates to `ManagedAgentCatalog.roleLabel` rather than lowercasing
    /// `ToString()`. A DU-name spelling is a compiler artefact: renaming a case
    /// would silently change the durable string, and every `roleOfString` read of
    /// an older journal would then answer `None`.
    let roleName (role: Role) : string = ManagedAgentCatalog.roleLabel role
