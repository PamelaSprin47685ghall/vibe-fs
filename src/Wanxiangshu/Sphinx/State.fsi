namespace Wanxiangshu.Sphinx

module State =
    val normalizeDistribution: distribution: Map<'key, float> -> Map<'key, float> when 'key: comparison
    val contractForForm: QuestionForm -> AnswerContract
    val deriveRootContract: assessment: SemanticAssessment -> RootContract
    val emptyRepresentation: RepresentationState
    val create: question: string -> EpistemicState
    val withYield: request: Request -> state: EpistemicState -> EpistemicState
    val clearPending: state: EpistemicState -> EpistemicState
    val hasEvidenceSemanticKey: semanticKey: string -> state: EpistemicState -> bool
    val remainingYieldBudget: state: EpistemicState -> int
    val remainingCostBudget: state: EpistemicState -> float
    val withinBudget: state: EpistemicState -> bool
    val markActionResolved: actionKey: string -> state: EpistemicState -> EpistemicState

    val addDependency:
        dependencyKey: string ->
        semanticKey: string ->
        dependencies: Map<string, Set<string>> ->
            Map<string, Set<string>>
