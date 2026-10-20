namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// ANCHOR view application: pure selection over one outgoing message list.
/// The caller supplies identity numbering, token counting and the accumulated
/// replacement history so this module stays free of process interop, and builds the
/// replacement messages itself.
[<RequireQualifiedAccess>]
module AnchorView =

    type AnchorMessage =
        { Id: string option
          Text: string
          Raw: obj }

    type AnchorViewEntry =
        | Kept of AnchorMessage
        | Summarized of message: AnchorMessage * summary: string
        | OpeningSummary of summary: string

    type AnchorViewReport =
        { Total: int
          Used: int
          Cached: int
          Kept: int
          Summarized: int
          Folded: int
          Replaced: int list }

    /// Replay captured intervals in order. Anonymous rows belong to the preceding
    /// anchor, or to zero before the first anchor. Cached counts all surviving
    /// material below PrefixUntil, including earlier summaries.
    val apply:
        numberOf: (string -> int) ->
        countTokens: (string -> int) ->
        history: AnchorReplacement list ->
        declaration: AnchorViewDeclaration ->
        messages: AnchorMessage list ->
            AnchorViewEntry list * AnchorViewReport

    val generate:
        numberOf: (string -> int) ->
        countTokens: (string -> int) ->
        history: AnchorReplacement list ->
        declaration: AnchorViewDeclaration ->
        messages: AnchorMessage list ->
            AnchorViewEntry list * AnchorViewReport

    val material: numberOf: (string -> int) -> entries: AnchorViewEntry list -> (int * string) list
    val materialKey: anchor: int -> text: string -> string
