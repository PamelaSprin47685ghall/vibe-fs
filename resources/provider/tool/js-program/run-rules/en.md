await js.run(class extends js.Runnable { ... }) — the engineering member.

Call it on the global `js` object: `this.run` is YOUR program entry, never this
member — the names collide on purpose, so the engineering run never lands on the
base class.

The Host owns the loop. You write only the semantic callbacks. On each step the Host
calls `next(ctx)`, runs what you returned, supervises it once per interval slice with
`check(ctx)`, dispatches the exit with `onExit(result)`, then calls `next(ctx)` again —
until it returns `null` or `maxDuration` expires. Your code never runs between slices,
and the slice clock, the child kill, and the outcome stay Host-owned: a Runnable cannot
skip, stretch, or reorder a slice.

`next` is a loop callback, not a one-shot declaration. After every command exits the
Host calls `next` again; when the work is done it must return `null`, typically
`ctx.step === 0 ? "command" : null`.

Required members; a missing or malformed one is `RUNNABLE_SPEC_INCOMPLETE`:
- `maxDuration` — milliseconds, positive. The engineering bound for the whole run.
- `next(ctx)` — the next step, called once per loop round.
- `check(ctx)` — the watchdog. Mandatory, and it runs once per slice.
Optional:
- `interval` — milliseconds per slice, default 10000, clamped to a minimum of 10.
- `onExit(result)` — dispatch for the process that just exited. `result` is
  `{ root, exitCode, stdout, stderr, elapsedMs }` where `stdout`/`stderr` carry that
  child's full transcript (bounded head+tail), not a slice delta.
- `onTimeout` — handler for `maxDuration` expiry; default: abort.

`next(ctx)` returns exactly one of:
- `"command"` — a shell command, foreground, runs to exit.
- `null` — nothing left to do; the run ends.
- `{ spawn, name }` — start a background process and continue immediately; `spawn`
  is a shell command string, the same form as the foreground.
- `{ signal, name }` — send the named signal (default `SIGTERM`) to that background;
  if it stays alive for 2 s, SIGKILL follows.
- `{ send: { name, input } }` — write `input` to that background's stdin (a trailing
  newline is added when missing); its incremental output arrives in the next `check`
  slice. Nested is canonical; the flat `{ send: input, name }` spelling is accepted
  as the same thing. This is the interactive form: spawn a REPL/server, send a line,
  read the delta, send the next line.
- `{ waitFor: { port, timeoutMs } }` — wait until that TCP port accepts; `check`
  keeps firing (at the probe pace, about 100 ms) while the port is polled, so a crash
  can still be observed and aborted.

`check(ctx)` returns:
- nothing, or `true` — continue.
- `{ abort: reason }` — abort the run now.
- `{ park: payload }` — suspend through the same channel as `js.park`. Resuming it
  with the string 'abort' aborts; any other unparked value continues.

Observe before ruling. `check` should read the ctx deltas, keep what matters on
instance fields (`this.lastSize`, `this.stalled`), and then decide. A `check` that
returns `true` without looking at anything is a watchdog that is not watching — the
Host will not stop you, but nothing is being supervised. A `next` or `check` that
throws fails the whole program.

`ctx` for `next(ctx)` is `{ root, step }` (`step` starts at 0 and counts rounds).
`ctx` for `check(ctx)` is
`{ root, stdout, stderr, exited, exitCode, backgrounds, elapsedMs, remainingMs }`,
where `backgrounds` is an array of `{ name, exited, exitCode }`; `stdout` and
`stderr` are this slice's deltas — the foreground command's and every background
child's merged into one observation window — not the transcript.

The outcome the await hands back is
`{ ok: true, status: 'completed' | 'aborted' | 'timeout', reason, exitCode, stdout, stderr, truncated, bytes, durationMs, steps }`.
`reason` is null on completion and carries the abort/timeout explanation otherwise;
`steps` counts the loop rounds the Host executed.

Failure modes, stated once:
- `status: 'timeout'` with the command output repeated once per round means `next`
  never returned `null` — the Host re-ran what it returned every round until
  `maxDuration`. Check `steps`: many rounds around one short command is the signature.
- A flat `{ send, name, input }` object does not name a child: the nested
  `{ send: { name, input } }` is canonical (the flat `{ send: input, name }` spelling
  is accepted; a `send` value that resolves to no name fails with `RUN_CHILD_UNKNOWN`).
- `this.run` is your program entry. The engineering loop is `js.run` only.

Consequences, stated once:
- Abort SIGKILLs the foreground process; `maxDuration` expiry does the same and
  reports status 'timeout'.
- Every background process is killed when the run ends.
- Disk state is never rolled back: a partial download stays partial. Write each step
  so that running it again continues the work instead of restarting it.
- Each stream keeps at most 256 KiB — the first 128 KiB plus the last 128 KiB — sets
  `truncated: true`, and still reports the true total as `bytes`.

Stable failure codes: `RUNNABLE_SPEC_INCOMPLETE`, `RUN_COMMAND_UNSUPPORTED`,
`RUN_WAIT_TIMEOUT`, `RUN_CHILD_UNKNOWN`, `RUN_PARK_UNSUPPORTED`.

Mechanical rules belong in the callbacks; semantic forks belong in parked payloads.

More worked examples: `run-recipes` (a minutes-long compile; an npm server + frontend test).
