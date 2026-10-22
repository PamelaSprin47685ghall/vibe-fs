import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import { link, acceptRun } from './support/load-projection.mjs'

test('WHAT[crash-reconciliation-004] actual explicit recovery re-enters ordinary workflows from durable facts without replaying tool continuations or program counters (GAP-149)', () => {
  const state = recovery.create()
  link(state, { agent: 'engineer' })
  const child = acceptRun(state, { agent: 'engineer' })

  // Active child before restart
  const before = recovery.childView(state, 'parent', 'child')
  assert.equal(before.activeRun, child.logicalRun)
  assert.equal(before.lifecycle, 'Active')

  const settlements = recovery.childSettlements(state)
  assert.equal(settlements.length, 1, 'read-only selection proposes the exact admitted orphan settlement')
  const fact = JSON.parse(settlements[0])
  assert.equal(fact[0], 'Agent')
  assert.equal(fact[1][0], 'Execution')
  assert.equal(fact[1][1][0], 'ChildWorkVoided')
  assert.deepEqual(fact[1][1][1], {
    ParentSessionId: ['SessionId', 'parent'],
    Work: {
      Handle: ['Agent', ['AgentHandleId', 'work']],
      ChildSessionId: ['SessionId', 'child'],
      AuthorityRoot: ['AuthorityRootUserMessageId', child.authorityRoot],
    },
  })

  const after = recovery.childView(state, 'parent', 'child')
  assert.equal(after.activeRun, child.logicalRun, 'selecting a settlement does not write it')
  assert.equal(after.lifecycle, 'Active', 'read-only selection leaves the lifecycle unchanged')
  assert.equal(after.joinable, 0, 'no fake completion created')
})
