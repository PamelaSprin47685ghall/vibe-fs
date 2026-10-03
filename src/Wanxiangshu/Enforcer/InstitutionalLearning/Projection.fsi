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

type BornRule =
    { TipName: string
      EnforcerTextEn: string
      EnforcerTextZh: string
      MainTextEn: string
      MainTextZh: string
      Trigger: string
      Negative: string }

type InstitutionalLearningProjectionState =
    { BySession: Map<SessionId, Map<string, LearningRecord>>
      BornRules: BornRule list }

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
            Result<InstitutionalLearningProjectionState, string>
