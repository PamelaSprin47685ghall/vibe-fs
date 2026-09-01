namespace Wanxiangshu.Sphinx

module Closure =
    val close: state: EpistemicState -> EpistemicState
    val absorbAndClose: state: EpistemicState -> observation: Observation -> EpistemicState
