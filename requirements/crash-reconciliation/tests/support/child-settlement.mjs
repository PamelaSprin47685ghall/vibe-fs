import assert from 'node:assert/strict'
import test from 'node:test'
import { recovery, link, acceptRun } from './load-projection.mjs'

test('WHAT[crash-reconciliation-020] actual load settlement propagates unavailable and unknown journal appends without claiming settlement', async () => {
  for (const unknown of [false, true]) {
    const state = recovery.create()
    link(state)
    acceptRun(state)
    link(state, { parent: 'parent-2', child: 'child-2', handle: 'work-2' })
    acceptRun(state, { parent: 'parent-2', child: 'child-2' })
    const before = recovery.childView(state, 'parent', 'child')
    const result = await recovery.rejectChildSettlementAppends(state, unknown)
    assert.equal(result.settled, false)
    assert.match(result.failure, unknown ? /outcome unknown.*injected settlement write failure/ : /not attempted.*writer is closing/)
    assert.equal(result.attempted.length, 1, 'the refusal must come from an actual journal append attempt')
    assert.equal(JSON.parse(result.attempted[0])[1][1][0], 'ChildRunVoided')
    assert.deepEqual(recovery.childView(state, 'parent', 'child'), before)
  }
})

test('WHAT[crash-reconciliation-020] load settlement with no orphan succeeds without using the rejecting writer', async () => {
  const result = await recovery.rejectChildSettlementAppends(recovery.create(), false)
  assert.deepEqual(result, { settled: true, failure: '', attempted: [] })
})
