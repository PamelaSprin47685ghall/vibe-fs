namespace Wanxiangshu.Sphinx.V2.Wire

open System
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core

module Encode =
    /// A revision goes out as a decimal string so no precision is lost in JavaScript.
    val revision: Revision -> string
    val attempt: Attempt -> string

    /// Plain, sorted key/value records only.
    val record: (string * obj) list -> obj

    /// A list is emitted as a plain array.
    val list: 'a list -> ('a -> obj) -> obj

    /// `None` in the domain is `null` on the wire.
    val option: 'a option -> ('a -> obj) -> obj

    val statusOf: InquiryState -> string

    /// `awaiting_results` is a content state, not a request for user input.
    [<Literal>]
    val awaitingResults: string = "awaiting_results"

    val semanticView: InquiryState -> obj
