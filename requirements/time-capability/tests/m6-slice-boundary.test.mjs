import assert from 'node:assert/strict'
import test from 'node:test'

import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const temporal = await import('../../../dist/Process/Surface.js')
const deadline = await import('../../../dist/Process/DeadlineSurface.js')
const START_MS = Date.parse('2000-01-01T00:00:00Z')
const settle = () => Promise.resolve()

test('WHAT[TIME-008] clock and timer capabilities are opaque instance-bound values', async () => {
  const firstClock = temporal.createVirtualClock()
  const secondClock = temporal.createVirtualClock()
  const firstTimer = temporal.createVirtualTimer()
  const secondTimer = temporal.createVirtualTimer()
  const firstHandle = temporal.timerDelay(firstTimer, 10)
  const secondHandle = temporal.timerDelay(secondTimer, 10)
  let firstFired = 0
  let secondFired = 0

  assertOpaque(firstClock, 'clock capability')
  assertOpaque(firstTimer, 'timer capability')
  assertOpaque(firstHandle, 'deadline capability')
  temporal.timerAwait(firstHandle).then(() => {
    firstFired += 1
  })
  temporal.timerAwait(secondHandle).then(() => {
    secondFired += 1
  })

  temporal.clockAdvanceMs(firstClock, 10)
  temporal.timerAdvance(firstTimer, 10)
  await settle()

  assert.equal(Number(temporal.clockNowMs(firstClock)), START_MS + 10)
  assert.equal(Number(temporal.clockNowMs(secondClock)), START_MS)
  assert.equal(firstFired, 1)
  assert.equal(secondFired, 0)

  temporal.timerCancel(secondHandle)
  temporal.timerDispose(firstTimer)
  temporal.timerDispose(secondTimer)
})

test('WHAT[TIME-008] Deadline is immutable and decided only by explicit clock input', () => {
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)
  assertOpaque(value, 'deadline')

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', value), true)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:01Z', value), 4000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:04Z', value), false)
})

test('WHAT[TIME-008] Node capability construction cannot mutate virtual time', async () => {
  const virtualClock = temporal.createVirtualClock()
  const virtualTimer = temporal.createVirtualTimer()
  const virtualHandle = temporal.timerDelay(virtualTimer, 0)
  let virtualFired = 0
  temporal.timerAwait(virtualHandle).then(() => {
    virtualFired += 1
  })

  const nodeClock = temporal.createNodeClock()
  const nodeTimer = temporal.createNodeTimer()
  await settle()

  assertOpaque(nodeClock, 'Node clock capability')
  assertOpaque(nodeTimer, 'Node timer capability')
  assert.equal(Number(temporal.clockNowMs(virtualClock)), START_MS)
  assert.equal(virtualFired, 0, 'constructing Node capabilities must not advance a virtual timer')

  temporal.timerAdvance(virtualTimer, 0)
  await settle()
  assert.equal(virtualFired, 1)
  temporal.nodeTimerDispose(nodeTimer)
  temporal.timerDispose(virtualTimer)
})
