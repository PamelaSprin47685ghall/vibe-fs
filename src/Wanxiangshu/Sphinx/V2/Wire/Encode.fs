namespace Wanxiangshu.Sphinx.V2.Wire

open System
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core

/// The v2 wire encoder.
///
/// WHAT[sphinx-v2-020]: only plain JS values cross this boundary. No Fable map, list or
/// discriminated-union representation ever escapes, and a revision leaves as a decimal
/// string.
///
/// WHAT[sphinx-v2-012]: `awaiting_results` is a completed tool result whose content says
/// work is still outstanding. It is not a protocol-level request for user input, so a
/// caller must not treat it as "the inquiry asked me a question".

module Encode =

    /// A revision goes out as a decimal string: JavaScript cannot hold every 64-bit
    /// integer, and a silently rounded revision would compare equal to the wrong one.
    let revision (value: Revision) : string = string (Revision.value value)

    let attempt (value: Attempt) : string = string (Attempt.value value)

    /// Plain, sorted key/value records only.
    let record (entries: (string * obj) list) : obj =
        box(entries |> List.sortBy fst |> Map.ofList)

    /// A list is emitted as a plain array.
    let list (items: 'a list) (encode: 'a -> obj) : obj = items |> List.map encode |> List.toArray |> box

    /// The empty view. `None` in the domain is `null` on the wire.
    let option (value: 'a option) (encode: 'a -> obj) : obj =
        value |> Option.map encode |> Option.toObj

    /// A business status, kept separate from the tool result envelope so a transport
    /// facility never has to guess what a business state means.
    let statusOf (state: InquiryState) : string =
        match state.Status with
        | InquiryStatus.Active -> "active"
        | InquiryStatus.InputRequired _ -> "input-required"
        | InquiryStatus.Suspended _ -> "suspended"
        | InquiryStatus.Cancelling -> "cancelling"
        | InquiryStatus.StopReached _ -> "completed"
        | InquiryStatus.Failed _ -> "failed"
        | InquiryStatus.Cancelled _ -> "cancelled"

    /// `awaiting_results` is a content state, not a request for user input.
    [<Literal>]
    let awaitingResults = "awaiting_results"

    /// The semantic projection, as a plain object.
    let semanticView (state: InquiryState) : obj = Projection.semanticProjection state
