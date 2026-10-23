**Top-level layout (required every call):** `prefixUntil` and `carry`.

These two fields shape the **next request's visible history**. They are not arguments of `fork` / `resume` / `deliver` / `advisor` / `devops`.

- `prefixUntil` (number, required): keep every visible interval whose anchor is **strictly less than** this value. Cut-off is exclusive. At `[ANCHOR: 7]`, set `prefixUntil: 7` to keep everything before anchor 7, or a larger number if you also need 7 and later still-visible anchors.
- `carry` (array, required): material appended **after** that prefix, in order. Integers copy a visible anchor's full interval; strings are literal text. Use `[]` when you need no extra layout material.
- Omitting either field is an error. There is no implicit "keep everything" by silence.
- The current turn is kept automatically. Write new conclusions in ordinary text; do not put them only in top-level `carry` to "pass them to yourself".

**Member background is a different slot:** `extra` (optional, default `[]`) on `fork` / `resume` / `commission` / `devops` / `dag` / `advisor` / `deliver`. That array goes to the child or the delivery cell. Do not put parent→child background into top-level `carry`.

The host removes top-level `carry` from the retained call record so literals appear only once.
