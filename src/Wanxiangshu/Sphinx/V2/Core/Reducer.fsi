namespace Wanxiangshu.Sphinx.V2.Core

module Reducer =
    /// Fold one event. The same event input always yields the same state: no clock, no
    /// randomness, no network, no model call (WHAT[sphinx-v2-016]).
    val apply: InquiryState option -> InquiryEvent -> Result<InquiryState, CoreError>

    /// Fold a whole transition batch. Any failure leaves the previous state untouched.
    val foldBatch: InquiryEvent list -> Result<InquiryState, CoreError>

    val fold: InquiryEvent list -> Result<InquiryState, CoreError>
