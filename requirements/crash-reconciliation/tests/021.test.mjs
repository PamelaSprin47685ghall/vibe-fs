import assert from 'node:assert/strict'
import test from 'node:test'
import * as binding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'
import { recovery, fold, link, admittedFission } from './support/load-projection.mjs'

test('WHAT[crash-reconciliation-021] durable handles answer by id and byname without process registration', () => {
  const state = recovery.create()
  link(state)
  const expected = { session: 'child', role: 'Engineer', agent: 'engineer' }
  assert.deepEqual(recovery.lookupChild(state, 'parent', 'work', false), expected)
  assert.deepEqual(recovery.lookupChild(state, 'parent', 'review-change', true), expected)
  assert.equal(recovery.lookupChild(state, 'parent', 'unknown', false), null)
  assert.equal(recovery.lookupChild(state, 'other-parent', 'work', false), null)
})

test('WHAT[crash-reconciliation-021] a binding cache miss resolves the latest durable TargetAgent rather than the logical byname', () => {
  for (const agent of ['engineer', 'devops']) {
    const state = recovery.create()
    binding.drop('child')
    recovery.installResolvers(state)
    try {
      assert.equal(binding.tryParent('child'), '')
      assert.equal(binding.tryAgent('child'), '')
      link(state, { agent })
      assert.deepEqual(recovery.bindingEvidence(state, 'child'), { parent: 'parent', agent })
      assert.equal(binding.tryParent('child'), 'parent')
      assert.equal(binding.tryAgent('child'), agent)
      binding.drop('child')
      assert.equal(binding.tryAgent('child'), agent)
    } finally {
      recovery.clearResolvers()
      binding.drop('child')
    }
  }
})

test('WHAT[crash-reconciliation-021] a hidden Host-owned leaf yields no parent-visible execution binding', () => {
  const state = recovery.create()
  link(state, { ownership: 'HostOwnedHidden' })
  assert.equal(recovery.bindingEvidence(state, 'child'), null)
})

test('WHAT[crash-reconciliation-021] a Fission lane cache miss resolves the folded owner and slot without pre-registration', () => {
  const state = recovery.create()
  recovery.clearLane('lane-0')
  recovery.clearLane('lane-1')
  recovery.installResolvers(state)
  try {
    assert.equal(recovery.lane('lane-0'), null)
    fold(state, admittedFission())
    assert.deepEqual(recovery.lane('lane-0'), { group: 'group', owner: 'owner', index: 0, count: 2 })
    assert.deepEqual(recovery.lane('lane-1'), { group: 'group', owner: 'owner', index: 1, count: 2 })
    assert.equal(recovery.lane('unknown-lane'), null)
  } finally {
    recovery.clearResolvers()
    recovery.clearLane('lane-0')
    recovery.clearLane('lane-1')
  }
})

test.todo('WHAT[crash-reconciliation-021] actual reuse, placement and await entry points all resolve durable children after an OS restart without registry prewarming (GAP-149)')
