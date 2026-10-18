namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// ANCHOR addressing vocabulary (PLAN phase 3). A declaration names the next
/// turn's context view: keep the prefix up to one anchor, then re-add referenced
/// segments or plain prose. Values are process-live; a restart resets numbers.
[<RequireQualifiedAccess>]
type AnchorExtra =
    | Anchor of int
    | Prose of string

type AnchorViewDeclaration =
    { PrefixUntil: int option
      Extra: AnchorExtra list }
