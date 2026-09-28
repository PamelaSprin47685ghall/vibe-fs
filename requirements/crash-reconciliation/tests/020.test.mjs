import assert from 'node:assert/strict'
import test from 'node:test'
import * as roles from '../../../dist/Foundation/RolesSurface.js'
import * as recovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import { fold, link, acceptRun, terminal, materialized, abandoned } from './support/load-projection.mjs'

test('WHAT[crash-reconciliation-020] current role vocabulary includes DevOps and Engineer but excludes legacy Coder', () => {
  assert.ok(roles.allRoleLabels.includes('devops'))
  assert.ok(roles.allRoleLabels.includes('engineer'))
  assert.equal(roles.allRoleLabels.includes('coder'), false)
})

test('WHAT[crash-reconciliation-020] the actual cancellation bridge closes child authority and leaves a reportable completion', () => {
  const state = recovery.create()
  link(state)
  const child = acceptRun(state)
  const result = recovery.settleChatTerminal(state, terminal('Cancelled', child))
  assert.equal(result.ok, true, result.error)
  const after = recovery.childView(state, 'parent', 'child')
  assert.equal(after.activeRun, '')
  assert.equal(after.lifecycle, 'CompletedAwaitingJoin')
  assert.equal(after.joinable, 1)
})

test('WHAT[crash-reconciliation-020] the cancellation bridge leaves Completed to its own completion path', () => {
  const state = recovery.create()
  link(state)
  const child = acceptRun(state)
  const before = recovery.childView(state, 'parent', 'child')
  const result = recovery.settleChatTerminal(state, terminal('Completed', child))
  assert.equal(result.ok, true, result.error)
  assert.deepEqual(recovery.childView(state, 'parent', 'child'), before)
})

test('WHAT[crash-reconciliation-020] production load decision voids active child runs without creating a completion', () => {
  for (const agent of ['engineer', 'devops']) {
    const state = recovery.create()
    link(state, { agent })
    const child = acceptRun(state, { agent })
    assert.equal(recovery.childView(state, 'parent', 'child').activeRun, child.logicalRun)
    const settlements = recovery.childSettlements(state)
    assert.equal(settlements.length, 1)
    assert.equal(JSON.parse(settlements[0])[1][1][0], 'ChildRunVoided')
    fold(state, settlements[0])
    const after = recovery.childView(state, 'parent', 'child')
    assert.equal(after.activeRun, '')
    assert.equal(after.lifecycle, 'Active')
    assert.equal(after.joinable, 0)
    assert.deepEqual(recovery.childSettlements(state), [])
    assert.equal(recovery.lookupChild(state, 'parent', 'work', false).session, 'child')
  }
})

test('WHAT[crash-reconciliation-020] human roots and child runs without a durable handle are not selected for load settlement', () => {
  const human = recovery.create()
  const manager = acceptRun(human, { child: 'parent', agent: 'manager', human: true })
  assert.deepEqual(recovery.childSettlements(human), [])
  assert.equal(recovery.childView(human, 'parent', 'parent').activeRun, manager.logicalRun)
  const unlinked = recovery.create()
  const child = acceptRun(unlinked)
  assert.deepEqual(recovery.childSettlements(unlinked), [])
  assert.equal(recovery.childView(unlinked, 'parent', 'child').activeRun, child.logicalRun)
})

test('WHAT[crash-reconciliation-020] a cancelled completion is reportable and production JoinDrain retires it once through the fold port', async () => {
  const state = recovery.create()
  link(state)
  fold(state, { family: 'Execution', case: 'HandleCompleted', payload: {
    ParentSessionId: 'parent', Handle: 'work', Kind: 'Cancelled', CompletionRef: null, CompletionDigest: null,
  } })
  assert.equal(recovery.childView(state, 'parent', 'child').joinable, 1)
  const drained = await recovery.drainCompletions(state, 'parent', 8, '2026-09-28T04:00:00Z')
  assert.equal(drained.ok, true, drained.error)
  assert.equal(drained.completions.length, 1)
  assert.match(drained.completions[0].run, /cancelled/)
  assert.equal(drained.completions[0].agent, 'engineer')
  assert.ok(drained.appended.some(line => JSON.parse(line)[1][1][0] === 'HandleRetired'))
  assert.equal(recovery.childView(state, 'parent', 'child').lifecycle, 'Retired')
  const repeated = await recovery.drainCompletions(state, 'parent', 8, '2026-09-28T04:00:00Z')
  assert.equal(repeated.ok, true, repeated.error)
  assert.deepEqual(repeated.completions, [])
  assert.deepEqual(repeated.appended, [])
})

test('WHAT[crash-reconciliation-020] stale Blogger selection respects exact live flight and abandonment clears the same folded open request', () => {
  const state = recovery.create()
  fold(state, materialized())
  const expected = [{ main: 'parent', blogger: 'blogger', request: 'request' }]
  assert.deepEqual(recovery.staleBloggerRequests(state, () => false), expected)
  assert.deepEqual(recovery.staleBloggerRequests(state, (blogger, request) => blogger === 'blogger' && request === 'request'), [])
  assert.deepEqual(recovery.staleBloggerRequests(state, (blogger, request) => blogger === 'blogger' && request === 'another-request'), expected)
  assert.deepEqual(recovery.staleBloggerRequests(state, (blogger, request) => blogger === 'another-blogger' && request === 'request'), expected)
  fold(state, abandoned())
  assert.deepEqual(recovery.staleBloggerRequests(state, () => false), [])
  fold(state, materialized('next-request'))
  assert.deepEqual(recovery.staleBloggerRequests(state, () => false), [{ main: 'parent', blogger: 'blogger', request: 'next-request' }])
})

test.todo('WHAT[crash-reconciliation-020] actual load durably settles orphan child and Blogger work before ordinary execution, preserves exact live flights, and refuses append failure (GAP-149)')
test.todo('WHAT[crash-reconciliation-020] a real interrupted run and PTY input are not replayed after restart while unique DevOps authority and durable model Persona binding are retained (GAP-149; GAP-129; GAP-132)')
