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

/// Caller-supplied BIRTH candidate (WHAT institutional-learning-004/005).
/// Bilingual leaf contract per behavior-diagnosis 001/005: unique TipName,
/// complete English and zh-CN EnforcerText and MainText, an identifiable
/// trigger, and a negative/distinction guard against misdiagnosis.
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
    /// Durable birth receipt for one admitted candidate. LexicalOrder records
    /// the derived live-union position at commit time; the projection
    /// re-derives merge order from TipName and never treats this field as a
    /// competing sequence number (behavior-diagnosis 003).
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
