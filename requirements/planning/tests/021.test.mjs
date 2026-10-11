import assert from 'node:assert/strict'
import test from 'node:test'
import * as planning from '../../../dist/Mission/Planning/Surface.js'

// Build the ladder to an authorized S3 tenure, then deliver it.
const buildDelivered = (workId) => {
  let state = planning.empty()
  state = planning.applyWorkEvent(planning.workOpened(workId, '/root'), state).state
  state = planning.applyWorkEvent(planning.incumbencyOpened(workId, 'inc-1', 'S1', 0n), state).state
  state = planning.applyWorkEvent(planning.reviewed('inc-1', 'S1', 'Promoted'), state).state
  state = planning.applyWorkEvent(planning.incumbencyRetired('inc-1', 'Relayed', 10n), state).state
  state = planning.applyWorkEvent(planning.incumbencyOpened(workId, 'inc-2', 'S2', 11n), state).state
  state = planning.applyWorkEvent(planning.reviewed('inc-2', 'S2', 'Promoted'), state).state
  state = planning.applyWorkEvent(planning.incumbencyRetired('inc-2', 'Relayed', 20n), state).state
  state = planning.applyWorkEvent(planning.incumbencyOpened(workId, 'inc-3', 'S3', 21n), state).state
  state = planning.applyWorkEvent(planning.reviewed('inc-3', 'S3', 'Promoted'), state).state
  state = planning.applyWorkEvent(planning.incumbencyRetired('inc-3', 'Delivered', 30n), state).state
  state = planning.applyWorkEvent(planning.delivered('inc-3', workId, 'sha-1', 'plan.md', 'exec-1'), state).state
  return state
}

test('WHAT[planning-021] reprompt reopens the delivered work to active, reusing the original tenure', () => {
  const state = buildDelivered('work-21')
  assert.equal(planning.isDelivered(state), true)
  assert.equal(state.Active, undefined, 'delivery clears the active incumbency')

  // The user reprompts: the system appends PlanReopened with the delivered incumbency
  const reopened = planning.applyWorkEvent(planning.reopened('inc-3'), state)
  assert.equal(reopened.ok, true)
  assert.equal(planning.isDelivered(reopened.state), false, 'reopen must clear the delivered state')
  assert.equal(reopened.state.Active.Id, 'inc-3', 'reopen must reuse the original incumbency, not open a new one')
  assert.equal(planning.activeStage(reopened.state), 'S3', 'reopen must keep the stage')
  assert.equal(reopened.state.Active.Authorized, true, 'reopen must keep the authorization')

  // The retired history is never rewritten
  assert.equal(planning.retiredCount(reopened.state), 3)
})

test('WHAT[planning-021] a reopened work can deliver again, forming a full second cycle', () => {
  let state = buildDelivered('work-21b')
  state = planning.applyWorkEvent(planning.reopened('inc-3'), state).state

  // The model revises P and delivers a second time on the same tenure
  const secondRetirement = planning.applyWorkEvent(planning.incumbencyRetired('inc-3', 'Delivered', 40n), state)
  assert.equal(secondRetirement.ok, true, 'a reopened tenure may retire Delivered again')

  const secondDelivery = planning.applyWorkEvent(planning.delivered('inc-3', 'work-21b', 'sha-2', 'plan.md', 'exec-2'), secondRetirement.state)
  assert.equal(secondDelivery.ok, true)
  assert.equal(planning.isDelivered(secondDelivery.state), true)
  assert.equal(secondDelivery.state.Delivered.ExecSessionId, 'exec-2', 'each cycle records its own execution session')

  // A second reopen restores the same tenure again
  const reopenedAgain = planning.applyWorkEvent(planning.reopened('inc-3'), secondDelivery.state)
  assert.equal(reopenedAgain.ok, true)
  assert.equal(reopenedAgain.state.Active.Id, 'inc-3')
  assert.equal(reopenedAgain.state.Active.Authorized, true)
})

test('WHAT[planning-021] reopening without a delivery is rejected', () => {
  let state = planning.empty()
  state = planning.applyWorkEvent(planning.workOpened('work-21c', '/root'), state).state
  state = planning.applyWorkEvent(planning.incumbencyOpened('work-21c', 'inc-1', 'S1', 0n), state).state

  // An active, never-delivered work cannot be reopened
  const reopenActive = planning.applyWorkEvent(planning.reopened('inc-1'), state)
  assert.equal(reopenActive.ok, false, 'reopen requires an existing delivery')

  // A relayed retirement is not a delivery either
  let relayed = planning.applyWorkEvent(planning.reviewed('inc-1', 'S1', 'Stayed'), state).state
  relayed = planning.applyWorkEvent(planning.incumbencyRetired('inc-1', 'Relayed', 10n), relayed).state
  const reopenRelayed = planning.applyWorkEvent(planning.reopened('inc-1'), relayed)
  assert.equal(reopenRelayed.ok, false, 'reopen requires a Delivered retirement, not a Relayed one')
})
