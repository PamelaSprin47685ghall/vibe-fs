namespace Wanxiangshu.Sphinx.Core

open Wanxiangshu.Foundation

module CoreHash =

    let canonical value = CanonicalJson.canonicalJson value

    let canonicalSha256 value =
        value |> canonical |> Wanxiangshu.Host.HostDigest.sha256Hex

    let deriveEventId inquiryId revision body =
        {| inquiry = InquiryId.value inquiryId
           revision = revision
           body = body |}
        |> canonicalSha256
        |> fun digest -> EventId.create ("ev" + digest)
