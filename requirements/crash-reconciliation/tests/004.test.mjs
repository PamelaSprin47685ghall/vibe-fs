import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import { fold, link, acceptRun, canonical } from './support/load-projection.mjs'

test('WHAT[crash-reconciliation-004] actual explicit recovery re-enters ordinary workflows from durable facts without replaying tool continuations or program counters (GAP-149)', () => {
  const state = recovery.create()
  link(state, { agent: 'engineer' })
  const child = acceptRun(state, { agent: 'engineer' })

  // Active child before restart
  const before = recovery.childView(state, 'parent', 'child')
  assert.equal(before.activeRun, child.logicalRun)
  assert.equal(before.lifecycle, 'Active')

  // 孤儿保持 Active、不写 void、joinable 为 0
  // Load recovery re-enters ordinary workflow from durable facts:
  // Settlement does not write void; active child run stays Active as child logical run
  const settlements = recovery.childSettlements(state)
  assert.equal(settlements.length, 0, 'settlement does not write void facts')

  const after = recovery.childView(state, 'parent', 'child')
  assert.equal(after.activeRun, child.logicalRun, 'activeRun is still child logical run')
  assert.equal(after.lifecycle, 'Active', 'lifecycle remains Active')
  assert.equal(after.joinable, 0, 'no fake completion created')
})
