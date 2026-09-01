namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

type HumanRootOpeningEvidence = private HumanRootOpeningEvidence of PhysicalUserMessageId

module HumanRootOpeningEvidence =
    val messageId: HumanRootOpeningEvidence -> PhysicalUserMessageId

type InitialAgentOwnerMigrationEvidence = private InitialAgentOwnerMigrationEvidence of XTraceOpeningEvidence

module InitialAgentOwnerMigrationEvidence =
    val opening: InitialAgentOwnerMigrationEvidence -> XTraceOpeningEvidence

[<RequireQualifiedAccess>]
type EndingLifeAdmission =
    | ExistingLife of LifeProjection
    | InitialAgentOwnerMigration of InitialAgentOwnerMigrationEvidence
    | NoLife

module ManagerLifeAdmission =
    val tryHumanRootOpening:
        lifecycle: ManagerLifeProjection ->
        profile: PromptAuthority.AuthorityExecutionProfile option ->
        messageId: PhysicalUserMessageId ->
            HumanRootOpeningEvidence option

    val ending:
        lifecycle: ManagerLifeProjection ->
        profile: PromptAuthority.AuthorityExecutionProfile option ->
        opening: XTraceOpeningEvidence option ->
            EndingLifeAdmission
