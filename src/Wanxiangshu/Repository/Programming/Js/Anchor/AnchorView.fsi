namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// ANCHOR view application: pure selection over one outgoing message list.
/// The caller supplies identity numbering, token counting and the accumulated
/// replacement map so this module stays free of process interop, and builds the
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

    type AnchorViewReport =
        { Total: int
          Used: int
          Cached: int
          Kept: int
          Summarized: int
          Folded: int
          Replaced: int list }

    /// Apply one declaration to a message list. A replaced anchor stops being
    /// an anchor: its row stands in as the summary and its span runs to the next
    /// anchor that still exists, folding in anything already replaced inside it.
    /// Unnamed rows are never dropped. `Cached` counts the kept anchors below
    /// `PrefixUntil`.
    val apply:
        numberOf: (string -> int) ->
        countTokens: (string -> int) ->
        replaced: Map<int, string> ->
        declaration: AnchorViewDeclaration ->
        messages: AnchorMessage list ->
            AnchorViewEntry list * AnchorViewReport
