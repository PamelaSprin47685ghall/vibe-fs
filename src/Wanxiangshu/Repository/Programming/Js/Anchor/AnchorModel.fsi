namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// Required prefix boundary and required keep-or-summary map.
type AnchorViewDeclaration =
    { PrefixUntil: int
      ThenMap: (int * string option) list }

type AnchorReplacement =
    { Anchor: int
      Until: int
      Summary: string }

/// One projected row; `Index` points back at the representative input message.
[<RequireQualifiedAccess>]
type AnchorProjectedRow =
    | Kept of index: int * cached: bool
    | Summarized of index: int * summary: string * cached: bool
    | Opening of summary: string * cached: bool

type AnchorProjection =
    { Rows: AnchorProjectedRow list
      Kept: int
      Summarized: int
      Folded: int
      Replaced: int list }

/// Host composer over wanxiangli/compression, the owner of the view coverage law.
[<RequireQualifiedAccess>]
module AnchorCoverage =
    /// Strict `{ prefixUntil, thenMap }` shape; Error carries the first violation verbatim.
    val parse: value: obj -> Result<AnchorViewDeclaration, string>
    val live: anchors: int list -> history: AnchorReplacement list -> int list

    val plan:
        anchors: int list ->
        history: AnchorReplacement list ->
        declaration: AnchorViewDeclaration ->
            AnchorReplacement list

    val project: positions: int list -> history: AnchorReplacement list -> prefixUntil: int -> AnchorProjection

[<RequireQualifiedAccess>]
module AnchorViewDeclaration =
    val empty: AnchorViewDeclaration
    val live: anchors: int list -> history: AnchorReplacement list -> int list

    val plan:
        anchors: int list ->
        history: AnchorReplacement list ->
        declaration: AnchorViewDeclaration ->
            AnchorReplacement list
