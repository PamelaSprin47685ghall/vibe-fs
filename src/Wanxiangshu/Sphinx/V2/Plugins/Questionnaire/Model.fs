namespace Wanxiangshu.Sphinx.V2.Plugins

/// The question vocabulary and its response types.
///
/// WHAT[sphinx-v2-022]: a response is not a number. `abstain`, `tie` and `conditional`
/// are first-class outcomes with their own handling, and none of them is a half point.
/// The proposal channel is separate from the judgment channel, so a model that dislikes
/// both options cannot quietly smuggle in a third one and vote for it.

[<RequireQualifiedAccess>]
type Judgment =
    | PreferLeft
    | PreferRight
    | Tie
    | Abstain of reason: string
    | Conditional of conditionRef: string

type PairwiseResponse =
    { Judgment: Judgment
      Rationale: string
      SourceLabels: string list
      /// Local references to plan cards attached to this response. The Runtime assigns
      /// canonical ids only after it accepts the response; a worker never invents one.
      ProposedAlternatives: ProposedAlternative list }

and ProposedAlternative =
    { LocalId: string
      Description: string
      ExpectedContribution: string }

type RankingResponse =
    { PresentedSet: string list
      RankedTiers: string list list
      Best: string option
      Worst: string option
      Unjudged: string list }

type MaxDiffObservation =
    { PresentedSet: string list
      Best: string
      Worst: string }

/// A recorded measurement, replayable as the model produced it (WHAT[sphinx-v2-021]).
type Observation =
    { ObservationId: string
      ScopeId: string
      SnapshotId: string
      /// measurement | intervention.
      Purpose: string
      QuestionId: string
      QuestionHash: string
      TemplateRef: string
      ClusterId: string
      /// The exact bytes the model saw.
      VisibleBytesHash: string
      CanonicalResult: string }

module QuestionnaireModel =

    /// `abstain` and `conditional` never enter the directional likelihood. They are real
    /// results — they carry information about what is missing — but they are not votes.
    let isDirectional (judgment: Judgment) : bool =
        match judgment with
        | Judgment.PreferLeft
        | Judgment.PreferRight
        | Judgment.Tie -> true
        | _ -> false

    /// A tie is its own event with its own mechanism, not the average of two preferences.
    let isTie (judgment: Judgment) : bool =
        match judgment with
        | Judgment.Tie -> true
        | _ -> false

    let isAbstention (judgment: Judgment) : bool =
        match judgment with
        | Judgment.Abstain _ -> true
        | _ -> false

    let isConditional (judgment: Judgment) : bool =
        match judgment with
        | Judgment.Conditional _ -> true
        | _ -> false

    /// Labels a response may cite: exactly the labels its own ticket showed it. A label
    /// from outside the presented set is refused rather than silently mapped to a real
    /// candidate (WHAT[sphinx-v2-023]).
    let labelsWithin (presented: Set<string>) (response: PairwiseResponse) : Result<unit, string> =
        let unknown =
            response.SourceLabels |> List.filter (fun label -> not (Set.contains label presented))

        if List.isEmpty unknown then
            Ok()
        else
            Error(sprintf "response cites labels outside the presented set: %s" (String.concat ", " unknown))

    /// Local ids must be unique inside one response, otherwise two proposals collapse
    /// into one when the Runtime assigns canonical ids.
    let uniqueLocalIds (response: PairwiseResponse) : Result<unit, string> =
        let ids = response.ProposedAlternatives |> List.map (fun alternative -> alternative.LocalId)
        let distinct = ids |> Set.ofList |> Set.count

        if distinct = List.length ids then
            Ok()
        else
            Error "proposed alternatives repeat a local id"
