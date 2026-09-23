namespace Wanxiangshu.Sphinx.V2.Plugins

module Pairwise =
    /// log(sigmoid(x)) = -softplus(-x), stable for large |x|.
    val logSigmoid: float -> float
    val logSumExp: float -> float -> float
    val directionalLogLikelihood: float -> float -> float -> float -> float
    val tieAwareLogLikelihood: float -> float -> float -> float -> float -> bool -> float
    val directionalGradient: float -> float -> float -> float -> float * float * float
