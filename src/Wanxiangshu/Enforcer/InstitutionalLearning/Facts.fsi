namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type ExperienceKind =
    | Celebrate
    | Regret

[<RequireQualifiedAccess>]
type LearningDisposition =
    | Absorb of existingRule: string
    | Birth of candidateTip: string
    | Discard of reason: string

type BirthCandidate =
    { TipName: string
      EnforcerTextEn: string
      EnforcerTextZh: string
      MainTextEn: string
      MainTextZh: string
      Trigger: string
      Negative: string }

type InstitutionalLearningFactCases =
    | LearningDispositionCommitted of
        {| SessionId: SessionId
           OccurrenceId: string
           Kind: ExperienceKind
           Experience: string
           RulebookRevision: string
           Disposition: LearningDisposition
           FrozenResult: string
           ResurfacedDeferredWorkIds: string list |}
    | InstitutionalRuleBorn of
        {| SessionId: SessionId
           OccurrenceId: string
           TipName: string
           EnforcerTextEn: string
           EnforcerTextZh: string
           MainTextEn: string
           MainTextZh: string
           Trigger: string
           Negative: string
           LexicalOrder: int |}
