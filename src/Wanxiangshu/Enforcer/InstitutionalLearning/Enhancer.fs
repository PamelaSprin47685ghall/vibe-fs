namespace Wanxiangshu.Enforcer.InstitutionalLearning

open System
open Wanxiangshu.Enforcer
open Wanxiangshu.Host

[<RequireQualifiedAccess>]
module InstitutionalEnhancer =

    let rulebookRevision (rules: EnforcerRule list) =
        rules
        |> List.sortBy _.LexicalOrder
        |> List.map (fun rule -> rule.Name + "\u001f" + rule.EnforcerText + "\u001f" + rule.MainText)
        |> String.concat "\u001e"
        |> HostDigest.sha256Hex

    let private nonEmpty (value: string) =
        not (isNull value) && value.Trim().Length > 0

    /// Mechanical admission boundary for a BIRTH candidate: unique TipName in
    /// the live rule set and complete bilingual leaf text with trigger and
    /// negative/distinction. Semantic abstraction (novelty, long-term value)
    /// stays with the caller; this check only refuses mechanically
    /// inadmissible candidates.
    let candidateAdmissible (candidate: BirthCandidate) (rules: EnforcerRule list) =
        nonEmpty candidate.TipName
        && nonEmpty candidate.EnforcerTextEn
        && nonEmpty candidate.EnforcerTextZh
        && nonEmpty candidate.MainTextEn
        && nonEmpty candidate.MainTextZh
        && nonEmpty candidate.Trigger
        && nonEmpty candidate.Negative
        && not (rules |> List.exists (fun rule -> rule.Name = candidate.TipName.Trim()))

    /// One bounded evaluation (WHAT institutional-learning-003). The caller
    /// owns the semantic judgment: an admissible candidate concludes BIRTH, an
    /// explicit absorbedRule naming a rule of the supplied canonical live
    /// rulebook concludes ABSORB, and everything else discards. The evaluator
    /// never guesses coverage from the experience text — substring matching
    /// is not an abstraction oracle. The experience and the live rulebook
    /// are the only inputs; no network, repository or provider access.
    /// Pure.
    let evaluate
        (experience: string)
        (rules: EnforcerRule list)
        (candidate: BirthCandidate option)
        (absorbedRule: string option)
        : LearningDisposition =
        let claimedRule = absorbedRule |> Option.map (fun rule -> rule.Trim())

        match candidate with
        | Some candidate when candidateAdmissible candidate rules -> LearningDisposition.Birth(candidate.TipName.Trim())
        | _ ->
            match claimedRule with
            | Some name when rules |> List.exists (fun rule -> rule.Name = name) -> LearningDisposition.Absorb name
            | _ -> LearningDisposition.Discard "no-reusable-mechanism"

    /// WHAT institutional-learning-002 revision contract around one
    /// evaluation. Before committing a BIRTH the live revision is re-read; on
    /// drift the evaluation runs once more against the latest live rulebook;
    /// a second conflict fails explicitly with zero commits.
    type LearnOutcome =
        | LearnCommitted of disposition: LearningDisposition * revision: string * reevaluated: bool
        | LearnRevisionConflict of revision: string

    let private confirmReevaluatedBirth load revision tip =
        let finalRevision = load () |> rulebookRevision

        if finalRevision = revision then
            LearnCommitted(LearningDisposition.Birth tip, finalRevision, true)
        else
            LearnRevisionConflict finalRevision

    let private reevaluateBirth experience candidate absorbedRule load fresh revision =
        match evaluate experience fresh candidate absorbedRule with
        | LearningDisposition.Birth tip -> confirmReevaluatedBirth load revision tip
        | other -> LearnCommitted(other, revision, true)

    let private confirmBirth experience candidate absorbedRule load revision tip =
        let fresh = load ()
        let freshRevision = rulebookRevision fresh

        if freshRevision = revision then
            LearnCommitted(LearningDisposition.Birth tip, revision, false)
        else
            reevaluateBirth experience candidate absorbedRule load fresh freshRevision

    let commitDecision
        (experience: string)
        (candidate: BirthCandidate option)
        (absorbedRule: string option)
        (load: unit -> EnforcerRule list)
        : LearnOutcome =
        let rules = load ()
        let revision = rulebookRevision rules

        match evaluate experience rules candidate absorbedRule with
        | LearningDisposition.Birth tip -> confirmBirth experience candidate absorbedRule load revision tip
        | other -> LearnCommitted(other, revision, false)
