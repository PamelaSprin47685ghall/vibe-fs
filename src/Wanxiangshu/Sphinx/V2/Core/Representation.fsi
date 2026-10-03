namespace Wanxiangshu.Sphinx.V2.Core

/// Explicit native DTOs, shared by durable encoding and the complete state fingerprint.
module Representation =
    val body: InquiryEventBody -> obj
    val state: InquiryState -> obj
    val fingerprint: (string -> string) -> InquiryState -> string
