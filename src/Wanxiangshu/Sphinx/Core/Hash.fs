namespace Wanxiangshu.Sphinx.Core

open Fable.Core
open Wanxiangshu.Foundation

type private NodeHash =
    abstract update: string -> NodeHash
    abstract digest: string -> string

module CoreHash =

    [<Import("createHash", "node:crypto")>]
    let private createHash (_algorithm: string) : NodeHash = jsNative

    let sha256Hex value =
        let hash = createHash "sha256"
        hash.update(value).digest("hex")

    let canonical value = CanonicalJson.canonicalJson value

    let canonicalSha256 value = value |> canonical |> sha256Hex

    let deriveEventId inquiryId revision body =
        {| inquiry = InquiryId.value inquiryId
           revision = revision
           body = body |}
        |> canonicalSha256
        |> fun digest -> EventId.create ("ev" + digest)
