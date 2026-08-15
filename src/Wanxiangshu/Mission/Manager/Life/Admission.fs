namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Finality

/// Evidence that one physical message is the active HumanRoot itself, not merely
/// another user-shaped message observed while a HumanRoot run happens to exist.
type HumanRootOpeningEvidence = private HumanRootOpeningEvidence of PhysicalUserMessageId

module HumanRootOpeningEvidence =
    let messageId (HumanRootOpeningEvidence messageId) = messageId

/// Evidence that an AgentOwnerRoot Manager has never owned a Life before and may
/// materialize its one migration Life from the current XTrace on first ending.
type InitialAgentOwnerMigrationEvidence = private InitialAgentOwnerMigrationEvidence of XTraceProjectionState

module InitialAgentOwnerMigrationEvidence =
    let xTrace (InitialAgentOwnerMigrationEvidence xTrace) = xTrace

[<RequireQualifiedAccess>]
type EndingLifeAdmission =
    | ExistingLife of LifeProjection
    | InitialAgentOwnerMigration of InitialAgentOwnerMigrationEvidence
    | NoLife

/// FINALITY-022 / INTERACTION-AUTHORITY-009 admission owner.
///
/// This module owns only pure projection decisions. It never scans journal history,
/// never parses provider prose and never stores a program counter. Durable effects
/// remain in ManagerLifeWorkflow CE functions.
module ManagerLifeAdmission =

    let private isManagerAuthority kind (profile: PromptAuthority.AuthorityExecutionProfile) =
        profile.CanonicalRole = Role.Manager && profile.AuthorityKind = kind

    /// A HumanRoot opening is admitted only for the physical message whose id is
    /// the AuthorityRootUserMessageId in the active immutable authority profile.
    /// Session-level "a HumanRoot exists" is deliberately insufficient evidence.
    let tryHumanRootOpening
        (lifecycle: ManagerLifeProjection)
        (profile: PromptAuthority.AuthorityExecutionProfile option)
        (messageId: PhysicalUserMessageId)
        : HumanRootOpeningEvidence option =
        match lifecycle.CurrentLife, profile with
        | None, Some active when
            isManagerAuthority PromptAuthority.RootAuthorityKind.HumanRoot active
            && active.AuthorityRootUserMessageId = PhysicalUserMessageId.promoteToAuthorityRoot messageId
            ->
            Some(HumanRootOpeningEvidence messageId)
        | _ -> None

    /// Finality ending admission for Manager Life ownership.
    ///
    /// AgentOwnerRoot migration is a one-time bridge for a session with no Life
    /// history. Once any Life has completed, CurrentLife=None means terminally
    /// closed — it can never be interpreted as permission to rematerialize the
    /// same XTrace into another Life.
    let private initialAgentOwnerOrNone
        (profile: PromptAuthority.AuthorityExecutionProfile option)
        (xTrace: XTraceProjectionState option)
        : EndingLifeAdmission =
        match profile, xTrace with
        | Some active, Some trace when
            isManagerAuthority PromptAuthority.RootAuthorityKind.AgentOwnerRoot active
            && trace.Opening.IsSome
            ->
            EndingLifeAdmission.InitialAgentOwnerMigration(InitialAgentOwnerMigrationEvidence trace)
        | _ -> EndingLifeAdmission.NoLife

    let ending
        (lifecycle: ManagerLifeProjection)
        (profile: PromptAuthority.AuthorityExecutionProfile option)
        (xTrace: XTraceProjectionState option)
        : EndingLifeAdmission =
        match lifecycle.CurrentLife with
        | Some life -> EndingLifeAdmission.ExistingLife life
        | None when not (List.isEmpty lifecycle.CompletedLives) -> EndingLifeAdmission.NoLife
        | None -> initialAgentOwnerOrNone profile xTrace
