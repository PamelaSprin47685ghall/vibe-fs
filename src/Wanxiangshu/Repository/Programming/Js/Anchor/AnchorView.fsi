namespace Wanxiangshu.Repository.Programming.Js.Anchor

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

    val apply:
        numberOf: (string -> int) ->
        countTokens: (string -> int) ->
        history: AnchorReplacement list ->
        declaration: AnchorViewDeclaration ->
        messages: AnchorMessage list ->
            AnchorViewEntry list * AnchorViewReport

    /// Project a declaration without changing the caller's permanent state.
    val generate:
        numberOf: (string -> int) ->
        countTokens: (string -> int) ->
        history: AnchorReplacement list ->
        declaration: AnchorViewDeclaration ->
        messages: AnchorMessage list ->
            AnchorViewEntry list * AnchorViewReport

    val material: numberOf: (string -> int) -> entries: AnchorViewEntry list -> (int * string) list

    val materialKey: anchor: int -> text: string -> string
