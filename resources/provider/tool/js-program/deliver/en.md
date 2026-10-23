deliver(extra = []) — deliver this work and end the program. Later statements do not run.

**`deliver` is the child→parent channel.** Close the work in your own words in ordinary assistant text, then call `this.deliver()` (or `this.deliver([])`) when no older context is needed. The parent receives that current turn plus whatever `extra` adds. There is no report schema; say what the work itself suggests, plain and clear. A few short FYI notes are welcome if they genuinely help; keep them brief.

`extra` is optional member background, default `[]`. It is **not** the top-level tool `carry` (layout for the next request). An integer copies a visible anchor's full history interval; a string supplies literal text. Items appear in array order. For example: `this.deliver([6, "…"])`. There is no member-level prefix parameter. The host formats the material as readable TOML.

A plain `return` does not deliver. A child that can still deliver remains active and keeps `join` waiting; a child whose delivery has become impossible is settled and reported once as `voided`. An invalid `extra` returns `{ ok: false, code: "VIEW_DECLARATION_INVALID", reason }` and leaves the program running. Correct it and deliver again.

Catching the internal delivery sentinel cancels that delivery and lets the program continue.
