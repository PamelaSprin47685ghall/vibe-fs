import assert from 'node:assert/strict'
import test from 'node:test'
import { admitAction } from './support/contract.mjs'

test('WHAT[work-041] a retired tenure cannot dispatch the fixed DevOps', () => {
  const active = { active: true, reviewed: true }
  const retired = { active: false, reviewed: true }
  assert.equal(admitAction(active, 'dispatch-devops').admitted, true)
  assert.equal(admitAction(retired, 'dispatch-devops').admitted, false)
})
