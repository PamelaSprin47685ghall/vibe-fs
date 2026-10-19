The programming surface for the planning draft `P`. `P` is the only file this tool can read or write. The system binds its path. `file`, `edit`, and `rewrite` take no path argument; a path argument fails with `PATH_DENIED`.

This surface has no glob, no grep, and no access to repository files. For repository facts, use `devops`.

In each incumbency, `todowrite-for-plan` is strictly read-only until this incumbency calls `review`. An `edit` or `rewrite` before that call is refused and tells you to call `review` first. After `review`, writing is unlocked. A relayed successor incumbency is read-only again until its own `review`.

Inside a program, `await js.ask(question)` asks the user and pauses: the tool call returns a parked payload and the attempt is interrupted; when the user replies, resume with `{ unpark: { handle, value } }` and the await yields the injected value. Only the S1 stage may ask; other stages refuse with `ASK_REFUSED`.

The call also accepts an optional `view: { prefixUntil, extra }` (the same ANCHOR view declaration as the other programming tools). Declaring it records a durable handover checkpoint for the successor request: `extra` carries the prose or anchor references the next tenure must keep. Use it instead of a hand-written handover note.
