namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// Required prefix boundary and required keep-or-summary map.
type AnchorViewDeclaration =
    { PrefixUntil: int
      ThenMap: (int * string option) list }

type AnchorReplacement =
    { Anchor: int
      Until: int
      Summary: string }

[<RequireQualifiedAccess>]
module AnchorViewDeclaration =
    val empty: AnchorViewDeclaration
    val live: anchors: int list -> history: AnchorReplacement list -> int list

    val plan:
        anchors: int list ->
        history: AnchorReplacement list ->
        declaration: AnchorViewDeclaration ->
            AnchorReplacement list
