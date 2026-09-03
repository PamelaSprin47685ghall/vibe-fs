namespace Wanxiangshu.Sphinx.Core

module Reducer =
    val stateTag: WorkState -> string
    val apply: InquiryState option -> InquiryEvent -> Result<InquiryState, CoreError>
    val fold: InquiryEvent list -> Result<InquiryState, CoreError>
    val semanticView: InquiryState -> obj
    val semanticHash: InquiryState -> string
