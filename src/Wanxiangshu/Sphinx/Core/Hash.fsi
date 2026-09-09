namespace Wanxiangshu.Sphinx.Core

module CoreHash =
    val canonical<'a> : 'a -> string
    val canonicalSha256<'a> : 'a -> string
    val deriveEventId<'a, 'b> : InquiryId -> 'a -> 'b -> EventId
