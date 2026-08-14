// Split from tests/unit/temporal/until-signal-or-deadline.test.mjs (cutover Wave 2a); owner: causal-wait
//
// G4R-CE S1 — CausalAwait.untilSignalOrDeadline vocabulary, causal-wait half
// (CAUSAL-005): event-driven 优先 polling. Production CE + virtual IDeadlineHandle.
// No wall clock, no 25ms slice poll. The WaitTimedOut deadline-escape assertion
// moved to time-capability (TIME-006).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  causalAwait,
  causalWait,
  CausalWaitRegistry,
  caseOf,
  listItems,
  timerPort,
} from '../../verification-system/tests/support/domain.mjs'

const owner = () => causalWait.owner('test-workflow', [['id', 'until-signal']])

const descriptor = () =>
  causalWait.create({
    waitKind: 'until-signal-or-deadline',
    owner: owner(),
    subject: [['name', 'coverage']],
    producer: causalWait.externalProducer('journal', [['rev', 'n']]),
    escapes: [causalWait.escape.openEndedExternal()],
    source: 'until-signal-or-deadline.test',
  })

const activeCount = (registry) => listItems(registry.Snapshot().Active).length

test('THEOREM_untilSignalOrDeadline_returns_immediately_when_tryRead_ready', async () => {
  const registry = new CausalWaitRegistry()
  const { rawPort } = timerPort.createVirtual()
  const handle = rawPort.Delay(10_000)
  const result = await causalAwait.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => 42,
    () => new Promise(() => {}),
  )
  assert.equal(caseOf(result), 'Ok')
  assert.equal(result.fields[0], 42)
  assert.equal(activeCount(registry), 0)
})

test('THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline', async () => {
  const registry = new CausalWaitRegistry()
  const { rawPort, advance } = timerPort.createVirtual()
  const handle = rawPort.Delay(5_000)
  let ready = false
  const waiters = []
  const pending = causalAwait.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => (ready ? 'material' : null),
    () =>
      new Promise((resolve) => {
        waiters.push(resolve)
      }),
  )
  // Let the CE arm the first signal wait, then publish material + wake.
  await new Promise((r) => setImmediate(r))
  assert.equal(waiters.length, 1)
  ready = true
  waiters[0]()
  const result = await pending
  assert.equal(caseOf(result), 'Ok')
  assert.equal(result.fields[0], 'material')
  advance(10_000)
  assert.equal(activeCount(registry), 0)
})

test('THEOREM_untilSignalOrDeadline_stale_signal_loops_until_deadline', async () => {
  const registry = new CausalWaitRegistry()
  const { rawPort, advance } = timerPort.createVirtual()
  const handle = rawPort.Delay(250)
  const waiters = []
  const pending = causalAwait.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => null,
    () =>
      new Promise((resolve) => {
        waiters.push(resolve)
      }),
  )
  // Two empty wakes (tryRead still None). Each awaitSignal must be a FRESH pending
  // Promise — a resolved Promise would busy-spin the CE loop.
  await new Promise((r) => setImmediate(r))
  assert.equal(waiters.length, 1)
  waiters[0]()
  await new Promise((r) => setImmediate(r))
  assert.equal(waiters.length, 2)
  waiters[1]()
  await new Promise((r) => setImmediate(r))
  assert.equal(waiters.length, 3)
  advance(250)
  const result = await pending
  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(result.fields[0]), 'WaitTimedOut')
  assert.ok(waiters.length >= 2, `expected ≥2 signal arms, got ${waiters.length}`)
})
