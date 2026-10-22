deliver(carry = []) — deliver this work and end the program. Later statements do not run.

**Use this pattern: write the final report in ordinary assistant text, then call `this.deliver()` immediately.** The current turn, including that report, is returned automatically. It needs no anchor and no copy in the argument. State what was done, the evidence, and what remains open.

Fill `carry` only when the caller also needs older context. An integer copies a visible anchor's full history interval; a string supplies literal text. Items appear in array order. For example: `this.deliver([6, "…"])`. There is no prefix parameter. An empty or omitted array returns the current turn alone. The host formats the material as readable TOML.

A plain `return` does not deliver. A child that can still deliver remains active and keeps `join` waiting; a child whose delivery has become impossible is settled and reported once as `voided`. An invalid `carry` returns `{ ok: false, code: "VIEW_DECLARATION_INVALID", reason }` and leaves the program running. Correct it and deliver again.

Catching the internal delivery sentinel cancels that delivery and lets the program continue.

