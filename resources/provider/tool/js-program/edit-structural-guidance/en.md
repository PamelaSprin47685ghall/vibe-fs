When Read is available, use {{fileMatches}} + text() + rewrite() for structural recomposition.

{{fileCall}} reads the plan artifact as an immutable UTF-8 snapshot and resolves **ordered anchors**; it returns an immutable FileView.

matches is Array<[beginAnchor, endAnchor, pattern]> where pattern is a non-empty string or a RegExp. Anchors are position names, not matched text. Every FileView has built-in anchors ^ (start) and $ (end); do not declare them.

Ordered matching: each pattern is searched from the current cursor; after a match, cursor = match.end. Duplicate source text does not need to be globally unique. Caller RegExp g/y flags and lastIndex are ignored.

text(from, to) — default text("^", "$") — returns the exact original substring between two resolved anchors; reverse slices fail. from/to may be a declared name, ^, $, or a temporary shift `name+N / name-N` (for example h1+200, $+0). N is a JS string index delta — the same unit as String.length / slice — **not a line number** and not a UTF-8 byte count. Shifts are not stored; the resulting caret is clipped to [0, file_len] inclusive.

Recommended workflow:
1. Declare the minimal begin/end anchors needed to locate spans (read or edit).
2. Let Host resolve those positions.
3. Read with text(from, to). Adjacent headers make a body slice: text("h1end", "h2").
4. For edits, build the complete resulting file from text(...) slices plus new content.
5. Near matches are diagnostics, never write authority.

If you are about to calculate structural boundaries by hand, first ask whether {{fileMatches}} + ordered anchors + text() already owns that boundary. If yes, use it. If a result later violates an obvious invariant, treat that as a stop signal, not an invitation to keep guessing.
