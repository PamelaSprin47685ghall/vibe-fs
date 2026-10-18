namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// ANCHOR addressing vocabulary (PLAN phase 3). A declaration states the next
/// turn's view: `PrefixUntil` is the protected cache-prefix boundary, and every
/// `ThenRemove` pair replaces one anchor's segments with a summary the model
/// writes itself. A replacement is permanent — the summary stands in for that
/// anchor for the rest of the session. Values are process-live; a restart
/// resets numbers and replacements.
type AnchorViewDeclaration =
    { PrefixUntil: int
      ThenRemove: (int * string) list }
