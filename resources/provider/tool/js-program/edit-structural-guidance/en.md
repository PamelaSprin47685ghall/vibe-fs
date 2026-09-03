When Read is available, use file(matches) + text() + rewrite() for structural recomposition.

If you are about to calculate structural boundaries by hand, first ask whether file(matches) +
ordered anchors + text() already owns that boundary. If yes, use it. If a result later violates an
obvious invariant, treat that as a stop signal, not an invitation to keep guessing.
