import test from 'node:test'
import assert from 'node:assert/strict'
import { planRequest, resetScopeState, turnBudget, isScopeInitialized, onTurnStart, clearSessionState } from '../../../dist/Context/Scope/Surface.js'

const user = (key, timestamp) => ({ kind: 'user', operator: true, key, timestamp, parts: [] })
const tool = (id, name, terminalField = '') => ({ id, tool: name, terminalField })
const assistant = (...parts) => ({ kind: 'assistant', operator: false, parts })
const result = (id) => ({ kind: 'result', operator: false, resultFor: id, resultField: 'content', parts: [] })

test('WHAT[context-compression-038] HNI scope view plans the user reminder independently of physical message envelopes', () => {
  const session = 'ses_scope_038_user'
  clearSessionState(session)
  const messages = [user('100:do the task', 100)]
  const plan = planRequest(session, messages)
  assert.equal(plan.active, true)
  assert.deepEqual(plan.slots.map(x => x.index), [0])
  assert.equal(plan.hints.length, 1)
  assert.equal(plan.hints[0].kind, 'user')
  assert.match(plan.hints[0].tail, /Then call resetScope immediately/)
  assert.equal(turnBudget(session), 0)
  assert.equal(isScopeInitialized(session), false)
  assert.equal(messages[0].parts.length, 0, 'the plan cannot mutate raw Host messages')
  clearSessionState(session)
})

test('WHAT[context-compression-038] the HNI scope plan retains reset and active work, without deciding OpenCode output fields', () => {
  const session = 'ses_scope_038_tools'
  clearSessionState(session)
  const operator = user('100:task', 100)
  planRequest(session, [operator])
  resetScopeState(session, 5)
  const messages = [operator, assistant(tool('old', 'read', 'output')), assistant(tool('reset', 'resetScope', 'output')), assistant(tool('new', 'read', 'output'))]
  const plan = planRequest(session, messages)
  assert.deepEqual(plan.slots.map(x => x.index), [0, 2, 3])
  assert.equal(plan.hints.find(x => x.index === 2).tail, '[Remaining turnBudget: 5]')
  assert.equal(plan.hints.find(x => x.index === 3).field, 'output')
  onTurnStart(session)
  assert.equal(turnBudget(session), 4)
  const later = planRequest(session, [...messages, assistant(tool('later', 'read', 'output'))])
  assert.equal(later.hints.find(x => x.index === 3).tail, '[Remaining turnBudget: 5]', 'the old result is pinned')
  assert.equal(later.hints.find(x => x.index === 4).tail, '[Remaining turnBudget: 4]', 'new results show current budget')
  clearSessionState(session)
})

test('WHAT[context-compression-038] a separate OMP toolResult uses the same plan, not a tool callback', () => {
  const session = 'ses_scope_038_omp'
  clearSessionState(session)
  const operator = user('10:task', 10)
  planRequest(session, [operator])
  resetScopeState(session, 2)
  const messages = [operator, assistant(tool('reset', 'resetScope')), result('reset'), assistant(tool('read', 'read')), result('read')]
  const plan = planRequest(session, messages)
  assert.deepEqual(plan.hints.filter(x => x.kind === 'tool').map(x => x.index), [2, 4])
  assert.ok(plan.hints.filter(x => x.kind === 'tool').every(x => x.field === 'content'))
  assert.ok(plan.hints.filter(x => x.kind === 'tool').every(x => x.tail === '[Remaining turnBudget: 2]'))
  onTurnStart(session)
  const replay = planRequest(session, [...messages, assistant(tool('next', 'read')), result('next')])
  assert.equal(replay.hints.find(x => x.index === 4).tail, '[Remaining turnBudget: 2]', 'earlier OMP tool result stays unchanged')
  assert.equal(replay.hints.find(x => x.index === 6).tail, '[Remaining turnBudget: 1]', 'the newly completed OMP result shows the current budget')
  clearSessionState(session)
})
