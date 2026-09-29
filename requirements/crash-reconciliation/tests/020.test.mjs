import assert from 'node:assert/strict'
import test from 'node:test'
import * as roles from '../../../dist/Foundation/RolesSurface.js'
import * as recovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import { fold, link, acceptRun, terminal, canonical, materialized, abandoned } from './support/load-projection.mjs'

test('WHAT[crash-reconciliation-020] current role vocabulary includes DevOps and Engineer but excludes legacy Coder', () => {
  assert.ok(roles.allRoleLabels.includes('devops'))
  assert.ok(roles.allRoleLabels.includes('engineer'))
  assert.equal(roles.allRoleLabels.includes('coder'), false)
})

test('WHAT[crash-reconciliation-020] in-process physical terminals preserve an active child logical run and handle', () => {
  for (const disposition of ['Cancelled', 'Failed', 'Completed']) {
    const state = recovery.create()
    link(state)
    const child = acceptRun(state)
    const before = recovery.childView(state, 'parent', 'child')
    const ended = terminal(disposition, child)
    const payload = JSON.parse(ended)[1][1][1]
    const started = payload.Evidence[1]
    fold(state, canonical('ChatExecution', 'Accepted', {
      SchemaVersion: 1, Key: payload.Key, Evidence: started.Accepted,
    }))
    fold(state, canonical('ChatExecution', 'ProviderStarted', {
      SchemaVersion: 1, Key: payload.Key, Evidence: started,
    }))
    fold(state, ended)
    assert.deepEqual(recovery.childView(state, 'parent', 'child'), before)
    assert.equal(before.activeRun, child.logicalRun)
    assert.equal(before.lifecycle, 'Active')
    assert.equal(before.joinable, 0)
  }
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
    assert.deepEqual(recovery.childView(state, 'parent', 'child'), {
      activeRun: '', lifecycle: 'Active', joinable: 0, horizonVisible: 1,
    })
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
