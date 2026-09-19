import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const process = await import('../../../dist/Process/Surface.js')
const deadline = await import('../../../dist/Process/DeadlineSurface.js')
const START_MS = Date.parse('2000-01-01T00:00:00Z')

test('WHAT[time-capability-005] TIME_005_deadline_verdict_uses_injected_clock_view', () => {
  const clock = process.createVirtualClock()
  process.clockSet(clock, '2026-01-01T00:00:00Z')
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  assert.equal(deadline.isExpired(process.clockNowIso(clock), value), false)
  process.clockAdvanceMs(clock, 6000)
  assert.equal(deadline.isExpired(process.clockNowIso(clock), value), true)
  assert.equal(deadline.remainingMs(process.clockNowIso(clock), value), 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertOpaque } = await import("../../verification-system/tests/support/js-contract.mjs");

const deadline = await import('../../../dist/Process/DeadlineSurface.js')
const ISO_START = '2026-01-01T00:00:00Z'

test('WHAT[time-capability-005] TIME_005_verdict_follows_injected_clock_not_value', () => {
  const dl = deadline.create(ISO_START, 5000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:04Z', dl), false)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', dl), true)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', dl), 3000)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:08Z', dl), 0)

  assert.equal(deadline.isExpired('2026-01-01T00:00:10Z', dl), true)
  assert.equal(deadline.isExpired('2026-01-01T00:00:01Z', dl), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const process = await import('../../../dist/Process/Surface.js')
const settle = () => new Promise((resolve) => setImmediate(resolve))

test('WHAT[time-capability-005] TEMPORAL_virtual_clock_time_is_input_not_authority', async () => {
  const timer = process.createVirtualTimer()
  let fired = 0
  const handle = process.timerDelay(timer, 100)
  process.timerAwait(handle).then(() => {
    fired += 1
  })
  assert.equal(fired, 0, 'must not fire before advance')
  process.timerAdvance(timer, 99)
  await settle()
  assert.equal(fired, 0, '99ms of 100ms deadline must not fire')
  process.timerAdvance(timer, 1)
  await process.timerAwait(handle)
  assert.equal(fired, 1, 'advance past deadline fires exactly once')
  process.timerDispose(timer)
})
test('WHAT[time-capability-005] TEMPORAL_virtual_clock_cancel_and_dispose_yield_zero_callbacks', async () => {
  const timer = process.createVirtualTimer()
  let fired = 0
  const first = process.timerDelay(timer, 10)
  const second = process.timerDelay(timer, 20)
  process.timerAwait(first).then(() => {
    fired += 1
  })
  process.timerAwait(second).then(() => {
    fired += 1
  })
  process.timerCancel(first)
  process.timerAdvance(timer, 30)
  await settle()
  assert.equal(fired, 1, 'cancelled handle must not fire; other handle fires once')

  process.timerDispose(timer)
  const afterDispose = process.timerDelay(timer, 10)
  let firedAfterDispose = 0
  process.timerAwait(afterDispose).then(() => {
    firedAfterDispose += 1
  })
  process.timerAdvance(timer, 10)
  await settle()
  assert.equal(firedAfterDispose, 0)
})
}
