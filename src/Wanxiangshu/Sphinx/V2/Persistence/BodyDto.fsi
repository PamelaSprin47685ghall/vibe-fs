namespace Wanxiangshu.Sphinx.V2.Persistence

open Thoth.Json
open Wanxiangshu.Sphinx.V2.Core

module internal BodyDto =
    val exact: string list -> Decoder<'value> -> Decoder<'value>
    val nonBlank: Decoder<string>
    val hash: Decoder<string>
    val revision: Decoder<Revision>
    val decoder: Decoder<InquiryEventBody>
