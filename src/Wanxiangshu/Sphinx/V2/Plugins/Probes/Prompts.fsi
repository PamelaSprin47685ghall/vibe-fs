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
    /// The shared prefix every protocol compiler assembles. A probe never rewrites the goal.
    val goalPrefix: string -> string -> string -> string list -> string
    val defaultTemplates: QuestionTemplate list
    val templateId: QuestionTemplate -> string

    /// The rendered question. `goalPrefixBody` is versioned text, not a seam for a
    /// probe to substitute its own target.
    val render: QuestionTemplate -> string -> string
