// tests/unit/verdict-feed.mjs — which node:test events prove the suite moved (VERIFY-004).
//
// The clause's discriminator: 「该事件是否证明被测因果链前进了一步」, and it disqualifies
// 「任何『有字节在动』的证据」. Applied to the unit suite, that rules out the granularity W4 was
// originally chartered for and rules in the one above it.
//
// ── why not assertions ──────────────────────────────────────────────────────
//
// An assertion runs AFTER the function under test returns, so for a pure fold it is a downstream
// observation of a finished computation, never a checkpoint inside a pending one. Feeding it would
// measure only "the test file's statements are advancing", which is the disqualified category
// verbatim. It is also actively worse than no heartbeat:
//
//   for (const c of cases) assert(f(c));   await neverResolves();     renews 300 times
//   while (true) assert(f(x));                                        renews forever
//
// That is the clause's own 「反复重连的 SSE 读者能永久续期一个错误的 watchdog」, reached from the
// unit side. Choosing it would have added a degradation rather than removing one.
//
// ── why a verdict ───────────────────────────────────────────────────────────
//
// A verdict is emitted by the runtime, not by the test author, and a hung test cannot produce one.
// It is also the first thing in this suite that gives `blocking` real work: stdout and diagnostics
// are genuine progress reports from a lane that must be RECORDED and must NOT renew, which is
// exactly the distinction `Watchdog.advance` already draws for canary sidecars.

/** Events that prove one test reached a verdict. */
const BLOCKING = new Set(['test:pass', 'test:fail', 'test:complete']);

/**
 * Events that prove bytes moved and nothing else.
 *
 * `test:stdout` is the load-bearing member: a fixture that hangs while printing is the exact shape
 * that turns a verdict feed back into a wall-clock timer, and `gate-unit-runner-cases.mjs` uses one.
 */
const BACKGROUND = new Set(['test:stdout', 'test:stderr', 'test:diagnostic']);

/**
 * Classify one event, or `null` when it is neither.
 *
 * `null` rather than a background default: `test:enqueue` and `test:dequeue` fire per test before
 * anything has happened, so treating unknown events as background would fill the dump's "last
 * background progress" line with scheduling noise and point the reader at the wrong lane.
 */
export function classifyVerdict(event) {
  const type = event?.type;
  if (typeof type !== 'string') return null;

  const name = typeof event?.data?.name === 'string' ? event.data.name : '(unnamed)';
  const file = typeof event?.data?.file === 'string' ? event.data.file : '(no file)';

  if (BLOCKING.has(type)) return { blocking: true, reason: `${type}:${name}`, lane: file };
  if (BACKGROUND.has(type)) return { blocking: false, reason: type, lane: file };
  return null;
}
