import assert from 'node:assert/strict'
import test from 'node:test'
import { planRequest, resetScopeState, onTurnStart, clearSessionState } from '../../../dist/Context/Scope/Surface.js'

const user = (key, timestamp = 1) => ({ kind: 'user', operator: true, key, timestamp, parts: [] })
const assistant = (id, tool) => ({ kind: 'assistant', operator: false, parts: [{ id, tool, terminalField: 'output' }] })
const tail = (plan, index) => plan.hints.find(x => x.kind === 'tool' && x.index === index)?.tail

test('WHAT[prefix-stability-016] repeated provider requests replay each old terminal result verbatim', () => {
  const sid = 'pfx-016-open'
  const operator = user('task', 1), reset = assistant('r1', 'resetScope'), read = assistant('read-1', 'read')
  clearSessionState(sid)
  planRequest(sid, [operator])
  resetScopeState(sid, 6)
  const prior = planRequest(sid, [operator, reset])
  assert.equal(tail(prior, 1), '[Remaining turnBudget: 6]')
  onTurnStart(sid)
  const later = planRequest(sid, [operator, reset, read])
  assert.equal(tail(later, 1), tail(prior, 1), 'the earliest previous result must not change when time advances')
  assert.equal(tail(later, 2), '[Remaining turnBudget: 5]', 'the new result carries the current budget')
  clearSessionState(sid)
})

test('WHAT[prefix-stability-016] new operator steer preserves old result bytes without resetScope reanchor', () => {
  const sid = 'pfx-016-steer'
  const operator = user('task', 1), reset = assistant('r1', 'resetScope')
  clearSessionState(sid)
  planRequest(sid, [operator])
  resetScopeState(sid, 5)
  const previous = planRequest(sid, [operator, reset])
  const next = planRequest(sid, [operator, reset, user('steer', 2)])
  assert.equal(next.initialized, false)
  assert.equal(next.budget, 0)
  assert.equal(tail(next, 1), tail(previous, 1), 'steer is not a cold prefix boundary')
  clearSessionState(sid)
})
