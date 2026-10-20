// The engineering-loop base class: the parent of this.call(class extends this.Callable { ... }).
class Callable {
  // The execution ceiling of the whole engineering job (milliseconds, positive). Required.
  maxDuration = 0;

  // Milliseconds per time slice; 10000 by default.
  interval = 10000;

  // next(ctx): the host calls it once per round; return a command string, null (work is done), { spawn, name }, { signal, name }, { send: { name, input } } or { waitFor: { port, timeoutMs } }. ctx = { root, step }.
  next(ctx) {}

  // watch(ctx): the host calls it once per time slice; read the ctx deltas first, then rule. Return true to continue, false or { abort: reason } to veto, { park: payload } to suspend. ctx = { root, step, stdout, stderr, exited, exitCode, backgrounds, elapsedMs, remainingMs }.
  watch(ctx) {}

  // sanity(): the control-flow self-test; returns the two thunks { nextDone, watchVeto }, each of which only moves the instance into its target state and returns an argument array.
  sanity() {}

  // onExit(result): what to do when a child process exits; result = { root, exitCode, stdout, stderr, elapsedMs }.
  onExit(result) {}
}
