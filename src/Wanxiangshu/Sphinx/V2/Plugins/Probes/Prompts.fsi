namespace Wanxiangshu.Sphinx.V2.Plugins

/// The eight question primitives, as template ids. Each one asks for a relation,
/// and each relation has exactly one legal way to enter its likelihood.
[<RequireQualifiedAccess>]
type QuestionTemplate =
    | PlanContributionPair
    | SmallSetRanking
    | RankingReversal
    | CombinationAndSequence
    | InvestigationValue
    | ImprovementMagnitude
    | CandidateRelations
    | AnswerNowVersusInvestigate

module Prompts =

    /// The rendered question. `goalPrefixBody` is versioned text, not a seam for a
    /// probe to substitute its own target.
    val render: QuestionTemplate -> string -> string
