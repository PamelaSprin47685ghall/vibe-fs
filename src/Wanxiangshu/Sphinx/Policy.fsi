namespace Wanxiangshu.Sphinx

module Policy =
    val canonicalAnswer: reason: string -> state: EpistemicState -> CanonicalAnswer
    val decide: state: EpistemicState -> EpistemicState * InquiryResult
    val start: question: string -> EpistemicState * InquiryResult
    val resume: state: EpistemicState -> observation: Observation -> EpistemicState * InquiryResult
