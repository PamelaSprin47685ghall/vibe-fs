namespace Wanxiangshu.Sphinx

module Methodology =
    type MethodDefinition =
        { Name: string
          FormWeights: Map<QuestionForm, float>
          FacetWeights: Map<string, float>
          BaseCost: float
          CombinesExistingKnowledge: bool }

    val library: MethodDefinition list
    val phase0Names: Set<string>
    val utility: state: EpistemicState -> definition: MethodDefinition -> float
    val activate: state: EpistemicState -> string list
    val generationMethods: state: EpistemicState -> string list
    val synthesisAvailable: state: EpistemicState -> bool
