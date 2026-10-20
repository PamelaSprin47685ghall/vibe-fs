The planning draft `P` uses the shared programming surface. `PLAN_PATH` is the only permitted file path: read with `this.cat(PLAN_PATH)`, locate spans with `this.where` / `this.whereAll`, and write with `this.write(PLAN_PATH, content)`. Every other path is refused with `PATH_DENIED`.

This surface has no glob, no grep, and no access to repository files. For repository facts, use `devops`.

In each incumbency, `todowrite-for-plan` is strictly read-only until this incumbency calls `review`. A `write` before that call is refused and tells you to call `review` first. After `review`, writing is unlocked. A relayed successor incumbency is read-only again until its own `review`.

Inside a program, `await this.ask(question)` asks the user and pauses: the tool call returns a parked payload and the attempt is interrupted; when the user replies, resume with `{ unpark: { handle, value } }` and the await yields the injected value. Only the S1 stage may ask; other stages refuse with `ASK_REFUSED`.

The call also accepts optional `view: { prefixUntil, thenMap }`. If supplied, both fields are required and follow the shared anchor-interval rules. Use summaries and null decisions in thenMap to retain the handover material; the declaration records a handover checkpoint. prefixUntil protects the current prefix, never restores compressed text and is not a delivery boundary.
