namespace Wanxiangshu.Sphinx

module Representation =
    val classKey: action: CognitiveAction -> string
    val dominates: left: CognitiveAction -> right: CognitiveAction -> bool
    val paretoFrontier: actions: CognitiveAction list -> CognitiveAction list
    val representative: actions: CognitiveAction list -> CognitiveAction option
    val optimize: state: EpistemicState -> EpistemicState
