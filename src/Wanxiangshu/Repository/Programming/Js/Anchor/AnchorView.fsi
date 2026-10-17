namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// ANCHOR view application: pure selection over one outgoing message list.
/// The caller supplies identity numbering and token counting so this module
/// stays free of process interop.
[<RequireQualifiedAccess>]
module AnchorView =

    type AnchorMessage =
        { Id: string option
          Text: string
          Raw: obj }

    type AnchorViewReport =
        { Total: int
          Used: int
          Cached: int
          Kept: int
          Dropped: int
          ExtraProse: string list }

    /// Apply one declaration to a message list. Identified messages are kept
    /// when their anchor is at or below prefixUntil; dropped ones are re-added
    /// when their anchor appears in the extras; prose extras are returned to
    /// the caller for instruction injection. Unidentified messages are always
    /// kept — the view never silently drops what it cannot name.
    val apply:
        numberOf: (string -> int) ->
        countTokens: (string -> int) ->
        declaration: AnchorViewDeclaration ->
        messages: AnchorMessage list ->
            AnchorMessage list * AnchorViewReport
