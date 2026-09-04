import assert from 'node:assert/strict'
import test from 'node:test'
import * as retirement from '../../../dist/Mission/Relay/Retirement/Surface.js'

test('WHAT[RELAY-007] silent normal terminal schedules an exit nudge instead of ending the incumbency', () => {
  const state = retirement.emptyNudges()
  const scheduled = retirement.observeNormalTerminal(state, 'inc-1', 1)
  assert.equal(scheduled.scheduled, true)
  assert.equal(retirement.nudgeCount(scheduled.state), 1)
})

test('WHAT[RETIRE-005] each fresh frontier schedules at most one nudge without a protocol retry ceiling', () => {
  let state = retirement.emptyNudges()
  for (let frontier = 1; frontier <= 100; frontier++) {
    const scheduled = retirement.observeNormalTerminal(state, 'inc-1', frontier)
    assert.equal(scheduled.scheduled, true)
    state = scheduled.state
    const duplicate = retirement.observeNormalTerminal(state, 'inc-1', frontier)
    assert.equal(duplicate.scheduled, false)
    state = duplicate.state
  }
  assert.equal(retirement.nudgeCount(state), 100)
})

test('WHAT[RETIRE-006] provider failure and external terminal never schedule an exit nudge', () => {
  const state = retirement.emptyNudges()
  assert.equal(retirement.observeProviderFailure(state, 'inc-1', 1).scheduled, false)
  assert.equal(retirement.observeAuthorityRevoked(state, 'inc-1', 2).scheduled, false)
})

