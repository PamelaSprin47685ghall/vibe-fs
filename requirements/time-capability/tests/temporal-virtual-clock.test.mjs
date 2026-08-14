// Split from tests/unit/temporal/fallback-aabb-confluence.test.mjs (cutover Wave 2a); owner: time-capability
//
// TIME-005 — "One World / Pure Time": time is input, never authority
// (the temporal harness's VirtualClock is the proof-side evidence of this rule;
// the underlying ITimerPort semantics are also pinned by timer-port.test.mjs).
// The fallback theorems moved to provider-attempt-recovery; the other harness
// primitives' tests moved to verification-system (temporal-harness.test.mjs).

import assert from 'node:assert/strict'
import test from 'node:test'
import { createVirtualClock } from '../../verification-system/tests/support/temporal-harness.mjs'

// ── VirtualClock is time as input ──────────────────────────────────────────

test('TEMPORAL_virtual_clock_time_is_input_not_authority', async () => {
  const vt = createVirtualClock()
  let fired = 0
  const handle = vt.port.delay(100)
  handle.delay().then(() => {
    fired += 1
  })
  assert.equal(fired, 0, 'must not fire before advance')
  vt.advance(99)
  await new Promise((r) => setImmediate(r))
  assert.equal(fired, 0, '99ms of 100ms deadline must not fire')
  vt.advance(1)
  await handle.delay()
  assert.equal(fired, 1, 'advance past deadline fires exactly once')
  vt.port.dispose()
})

test('TEMPORAL_virtual_clock_cancel_and_dispose_yield_zero_callbacks', async () => {
  const vt = createVirtualClock()
  let fired = 0
  const a = vt.port.delay(10)
  const b = vt.port.delay(20)
  a.delay().then(() => {
    fired += 1
  })
  b.delay().then(() => {
    fired += 1
  })
  a.cancel()
  vt.advance(30)
  await new Promise((r) => setImmediate(r))
  assert.equal(fired, 1, 'cancelled handle must not fire; other handle fires once')
  vt.port.dispose()
  const c = vt.port.delay(10)
  let firedAfterDispose = 0
  // Dispose clears pending; new handles after dispose still enqueue but advance is no-op per PtyTiming.fs
  c.delay().then(() => {
    firedAfterDispose += 1
  })
  vt.advance(10)
  await new Promise((r) => setImmediate(r))
  // After port dispose, Advance is a no-op — so c must not fire.
  assert.equal(firedAfterDispose, 0)
})
