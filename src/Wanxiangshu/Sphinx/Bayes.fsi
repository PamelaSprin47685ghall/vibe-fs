namespace Wanxiangshu.Sphinx

module Bayes =
    val update: state: EpistemicState -> BayesianBelief option
    val likelihoodQualified: state: EpistemicState -> bool
    val posteriorFor: key: string -> state: EpistemicState -> float option
    val frozenInference: hypotheses: Hypothesis list -> evidence: Evidence list -> BayesianBelief option
