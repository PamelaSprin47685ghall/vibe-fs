await js.run(class extends js.Runnable { ... }) — the engineering member.

The Host owns the loop. You write only the semantic callbacks. On each step the Host
calls `next(ctx)`, runs what you returned, supervises it once per interval slice with
`check(ctx)`, dispatches the exit with `onExit(result)`, then calls `next(ctx)` again —
until it returns `null` or `maxDuration` expires. Your code never runs between slices,
and the slice clock, the child kill, and the outcome stay Host-owned: a Runnable cannot
skip, stretch, or reorder a slice.

Required members; a missing or malformed one is `RUNNABLE_SPEC_INCOMPLETE`:
- `maxDuration` — milliseconds, positive. The engineering bound for the whole run.
- `next(ctx)` — the next step.
- `check(ctx)` — the watchdog. Mandatory, and it runs once per slice.
Optional:
- `interval` — milliseconds per slice, default 10000.
- `onExit(result)` — dispatch for the process that just exited.
- `onTimeout` — handler for `maxDuration` expiry; default: abort.

`next(ctx)` returns exactly one of:
- `"command"` — a shell command, foreground, runs to exit.
- `null` — nothing left to do; the run ends.
- `{ spawn, name }` — start a background process and continue immediately.
- `{ signal, name }` — send the named signal (default `SIGTERM`) to that background;
  if it stays alive for 2 s, SIGKILL follows.
- `{ send, name, input }` — write `input` to that background's stdin (a trailing
  newline is added when missing); its incremental output arrives in the next
  `check` slice. This is the interactive form: spawn a REPL/server, send a line,
  read the delta, send the next line.
- `{ waitFor: { port, timeoutMs } }` — wait until that TCP port accepts.

`check(ctx)` returns:
- nothing, or `true` — continue.
- `{ abort: reason }` — abort the run now.
- `{ park: payload }` — suspend through the same channel as `js.park`. Resuming it with
  the string 'abort' aborts; any other unparked value continues.

`ctx` for `next(ctx)` is `{ root, step }`.
`ctx` for `check(ctx)` and `onExit(result)` is
`{ root, stdout, stderr, exited, exitCode, backgrounds, elapsedMs, remainingMs }`,
where `backgrounds` is an array of `{ name, exited, exitCode }`; `stdout` and `stderr`
are this slice's deltas, not the transcript.

The outcome the await hands back is
`{ ok: true, status: 'completed' | 'aborted' | 'timeout', exitCode, stdout, stderr, truncated, bytes, durationMs }`.

Consequences, stated once:
- Abort SIGKILLs the foreground process; `maxDuration` expiry does the same and reports
  status 'timeout'.
- Every background process is killed when the run ends.
- Disk state is never rolled back: a partial download stays partial. Write each step so
  that running it again continues the work instead of restarting it.
- Each stream keeps at most 256 KiB — the first 128 KiB plus the last 128 KiB — sets
  `truncated: true`, and still reports the true total as `bytes`.

Stable failure codes: `RUNNABLE_SPEC_INCOMPLETE`, `RUN_COMMAND_UNSUPPORTED`,
`RUN_WAIT_TIMEOUT`, `RUN_CHILD_UNKNOWN`, `RUN_PARK_UNSUPPORTED`.

Mechanical rules belong in the callbacks; semantic forks belong in parked payloads.
