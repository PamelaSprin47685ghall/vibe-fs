namespace Wanxiangshu.Sphinx

module Value =
    val currentAnswerLoss: state: EpistemicState -> float
    val stopUtility: state: EpistemicState -> float
    val actionDelta: state: EpistemicState -> action: CognitiveAction -> float
    val actionUtility: state: EpistemicState -> action: CognitiveAction -> float
    val revalueActions: state: EpistemicState -> EpistemicState
    val bestOpenAction: state: EpistemicState -> CognitiveAction option
    val stopDominates: state: EpistemicState -> bool
