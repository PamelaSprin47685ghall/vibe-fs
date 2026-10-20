await js.call(class extends js.Callable { ... }) — the engineering member.

Call it on the global `js` object. Your program entry remains `async run()`.

The Host owns the loop. You write only the semantic callbacks. On each round the
Host calls `next(ctx)`, runs what you returned, supervises it once per interval
slice with `watch(ctx)`, dispatches the exit with `onExit(result)`, then calls
`next(ctx)` again — until it returns `null` or `maxDuration` expires. Your code
never runs between slices, and the slice clock, the child kill, and the outcome
stay Host-owned: a Callable cannot skip, stretch, or reorder a slice.

`next` is a loop callback, not a one-shot declaration. After every command exits
the Host calls `next` again; when the work is done it must return `null`, typically
`ctx.step === 0 ? "command" : null` for a fixed script, or a state read for
everything that reacts to what happened.

Required members; a missing or malformed one is `CALLABLE_SPEC_INCOMPLETE`:
- `maxDuration` — milliseconds, positive. The engineering bound for the whole run.
- `next(ctx)` — the next step, called once per loop round.
- `watch(ctx)` — the watchdog. Mandatory, and it runs once per slice.
- `sanity()` — the control-flow self-test (below).
Optional:
- `interval` — milliseconds per slice, default 10000, clamped to a minimum of 10.
- `onExit(result)` — dispatch for the process that just exited. `result` is
  `{ root, exitCode, stdout, stderr, elapsedMs }` where `stdout`/`stderr` carry
  that child's full transcript (bounded head+tail), not a slice delta.
- `outputBudgetBytes` — non-negative safe integer, default 65536. It covers the
  complete encoded result and each observation, including metadata, escaping
  and the truncation notice. The Host window can make the effective limit smaller.
- `worldLock` — boolean, default false. Set true for an expected large output;
  the shared permit stays held until the physical scope closes.

`sanity()`: prove you are not bluffing. The Host runs it once, before anything
else. It returns two thunks — the host drives them and mechanically checks the
results, so no assertion code is yours to write:
```
sanity() {
  return {
    nextDone: () => { this.index = this.files.length; return [{ root: "/", step: 99 }]; },
    watchVeto: () => { this.stalled = 6; this.current = this.files[0]; return [{ ...ctx }]; },
  };
}
```
Each thunk returns the argument array for its method. The Host calls actual
`next` and requires `null`, then calls actual `watch` and requires a veto
(`false` or `{ abort }`). A thunk that fails parks the whole `js.call` at the
call site with the diagnosis payload — fix the class in a new call, or unpark
with a value your program consumes as the js.call outcome. Writing the thunks is
designing the termination condition: if you cannot mock a state where next
returns null, the exit does not exist yet.

Two ways to drive `next`: round-driven (`ctx.step === 0 ? cmd : null`) for a
fixed script — spawn, waitFor, test, reap; and event-driven for everything that
reacts to what happened — `onExit` records exit codes into instance fields,
`watch` records what it observed, and `next` reads those fields and decides:
retry, skip, hold one slice, advance, or `null`. The worked examples show both:
`run-recipes` is the fixed script, `ultra-devops` is fully event-driven.

Process output in JS, not in shell pipes. The outcome carries the command's
full stdout (bounded head+tail, `truncated` tells the truth); piping through
`| head`, `| tail` or `grep` throws away exactly the bytes you may need — key
errors often sit at the front, and the tail is not guaranteed to contain
anything. Return the bare command and slice the transcript in JS:
`outcome.stdout.split("\n").filter((l) => l.includes("error"))` — the logic is
as rich as JS, and the original stays in hand for a fallback when the first
slice misses.

`next(ctx)` returns exactly one of:
- `"command"` — a shell command, foreground, runs to exit.
- `null` — nothing left to do; the run ends.
- `{ spawn, name }` — start a background process and continue immediately; `spawn`
  is a shell command string, the same form as the foreground.
- `{ signal, name }` — send `SIGTERM`, `SIGKILL`, `SIGINT` or `SIGHUP` to that background;
  if it stays alive for 2 s, SIGKILL follows.
- `{ send: { name, input } }` — write `input` to that background's stdin (a trailing
  newline is added when missing); its incremental output arrives in the next `watch`
  slice. Nested is canonical; the flat `{ send: input, name }` spelling is accepted
  as the same thing. This is the interactive form: spawn a REPL/server, send a line,
  read the delta, send the next line.
- `{ waitFor: { port, timeoutMs } }` — wait until that TCP port accepts; `watch`
  keeps firing (at the probe pace, about 100 ms) while the port is polled, so a crash
  can still be observed and aborted.

`watch(ctx)` returns:
- nothing, or `true` — continue.
- `false`, or `{ abort: reason }` — veto the run now. `false` is a real veto, not
  a synonym for continue.
- `{ park: payload }` — suspend through the same channel as `js.park`. Resuming it
  with the string 'abort' aborts; any other unparked value continues.

Observe before ruling. `watch` should read the ctx deltas, keep what matters on
instance fields (`this.lastSize`, `this.stalled`), and then decide. A `watch` that
uses `return true` without looking at anything is a watchdog that is not watching — the
Host will not stop you, but nothing is being supervised. A `next` or `watch` that
throws fails the whole program.

`ctx` for `next(ctx)` is `{ root, step }` (`step` starts at 0 and counts rounds).
`ctx` for `watch(ctx)` is
`{ root, step, stdout, stderr, exited, exitCode, signal, scopeClosed, backgrounds, elapsedMs, remainingMs }`,
where `backgrounds` contains each child's name and physical state; `stdout` and
`stderr` are this slice's deltas — the foreground command's and every background
child's merged into one observation window — not the transcript.

The outcome the await hands back is
`{ ok: true, status: 'completed' | 'aborted' | 'timeout' | 'failed', reason, exitCode, signal, scopeClosed, endUnconfirmed, notStarted, stopRequested, outputComplete, fault, stdout, stderr, truncated, outputNotice, bytes, durationMs, steps }`.
The exit fields describe the latest foreground. An unknown native end stays
unknown; a stop request does not prove exit or scope closure. A capture or
background supervision fault remains explicit, even when an exit is known.
`reason` is null on completion and carries the abort/timeout explanation otherwise;
`steps` counts the loop rounds the Host executed.

Failure modes, stated once:
- `sanity()` parked with a `sanity.failure` payload: a thunk failed to show its
  exit — `next` never returned `null`, or `watch` never vetoed. The payload
  carries the failure and what the thunk returned; fix the class in a new call.
- `status: 'timeout'` with the command output repeated once per round means `next`
  never returned `null` — the Host re-ran what it returned every round until
  `maxDuration`. Check `steps`: many rounds around one short command is the signature.
- A flat `{ send, name, input }` object does not name a child: the nested
  `{ send: { name, input } }` is canonical (the flat `{ send: input, name }` spelling
  is accepted). A missing or invalid name fails with `CALL_COMMAND_UNSUPPORTED`;
  a valid name with no live child fails with `CALL_CHILD_UNKNOWN`.
- `this.run` is your program entry. The engineering loop is `js.call` only.

Consequences, stated once:
- Abort SIGKILLs the foreground process; `maxDuration` expiry does the same and
  reports status 'timeout'.
- Final cleanup requests TERM for all remaining children, then KILL after the
  grace period. An unconfirmed end remains explicit and retains its owner lease.
- Disk state is never rolled back: a partial download stays partial. Write each step
  so that running it again continues the work instead of restarting it.
- Each stream has a 256 KiB presentation buffer. The complete encoding budget
  can retain less; `truncated` and the data field `outputNotice` state that the
  middle is missing. `bytes` counts raw bytes actually received.
- A budget below the required encoding is refused before process creation or
  input, with `minimumOutputBudgetBytes`. Names that cannot fit in the background
  observation are refused before that background starts.

Stable failure codes: `CALLABLE_SPEC_INCOMPLETE`, `CALL_COMMAND_UNSUPPORTED`,
`CALL_WAIT_TIMEOUT`, `CALL_CHILD_UNKNOWN`, `CALL_PARK_UNSUPPORTED`,
`CALL_OUTPUT_BUDGET_INVALID`, `CALL_OUTPUT_BUDGET_TOO_SMALL`, `CALL_WORLD_LOCK_INVALID`,
`CALL_HARD_LIMIT_INVALID`, `CALL_CANCELLED_BEFORE_START`, `CALL_DEADLINE_BEFORE_START`,
`CALL_SANITY_TIMEOUT`, `CALL_INPUT_UNCONFIRMED`, `CALL_CHILD_NAME_CONFLICT`,
`CALL_HOST_UNAVAILABLE`.

Mechanical rules belong in the callbacks; semantic forks — including a rejected
sanity thunk — belong in parked payloads.

More worked examples: `run-recipes` (a minutes-long compile; an npm server + frontend test).
