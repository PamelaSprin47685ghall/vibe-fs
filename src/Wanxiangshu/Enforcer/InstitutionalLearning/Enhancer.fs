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

    let private absorbOrDiscard (rules: EnforcerRule list) (absorbedRule: string option) =
        match absorbedRule |> Option.map (fun rule -> rule.Trim()) with
        | Some name when rules |> List.exists (fun rule -> rule.Name = name) -> LearningDisposition.Absorb name
        | _ -> LearningDisposition.Discard "no-reusable-mechanism"

    /// Mechanical evaluation of caller-supplied candidates and absorb claims.
    /// It does not extract a mechanism from the experience (WHAT[003]).
    let evaluate
        (experience: string)
        (rules: EnforcerRule list)
        (candidate: BirthCandidate option)
        (absorbedRule: string option)
        : LearningDisposition =
        match candidate with
        | Some candidate when candidateAdmissible candidate rules -> LearningDisposition.Birth(candidate.TipName.Trim())
        | _ -> absorbOrDiscard rules absorbedRule

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
