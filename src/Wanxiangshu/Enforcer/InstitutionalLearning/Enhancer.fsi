namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Enforcer

[<RequireQualifiedAccess>]
module InstitutionalEnhancer =
    val rulebookRevision: rules: EnforcerRule list -> string

    val evaluate:
        experience: string ->
        rules: EnforcerRule list ->
        candidate: BirthCandidate option ->
        absorbedRule: string option ->
            LearningDisposition

    val candidateAdmissible: candidate: BirthCandidate -> rules: EnforcerRule list -> bool

    type LearnOutcome =
        | LearnCommitted of disposition: LearningDisposition * revision: string * reevaluated: bool
        | LearnRevisionConflict of revision: string

    val commitDecision:
        experience: string ->
        candidate: BirthCandidate option ->
        absorbedRule: string option ->
        load: (unit -> EnforcerRule list) ->
            LearnOutcome
