namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Foundation.Identity

type LearningRecord =
    { OccurrenceId: string
      Kind: ExperienceKind
      Experience: string
      RulebookRevision: string
      Disposition: LearningDisposition
      FrozenResult: string
      ResurfacedDeferredWorkIds: string list }

type InstitutionalLearningProjectionState =
    { BySession: Map<SessionId, Map<string, LearningRecord>> }

[<RequireQualifiedAccess>]
module InstitutionalLearningProjection =
    val empty: InstitutionalLearningProjectionState

    val tryFind:
        sessionId: SessionId ->
        occurrenceId: string ->
        state: InstitutionalLearningProjectionState ->
            LearningRecord option

    val apply:
        fact: InstitutionalLearningFactCases ->
        state: InstitutionalLearningProjectionState ->
            InstitutionalLearningProjectionState
