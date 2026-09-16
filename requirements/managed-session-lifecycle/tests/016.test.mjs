import assert from 'node:assert/strict'
import test from 'node:test'
import * as interrupt from '../../../dist/Execution/Session/InterruptBoundarySurface.js'

test('WHAT[MANAGED-SESSION-016] Sessions adapter rejects root attempt interrupt and physically aborts a managed child exactly once', async () => {
  const r = await interrupt.testRejectRootInterrupt()
  assert.equal(r.ok, true)
})

test('WHAT[MANAGED-SESSION-016] managed interrupt Host rejection is terminal after exactly one AbortSession attempt', async () => {
  const r = await interrupt.testHostRejectionTerminal()
  assert.equal(r.terminal, true)
})

test('WHAT[MANAGED-SESSION-016] Turn orchestration consumes typed outcome without cross-callback aborted registry PC', () => {
  assert.equal(interrupt.testTurnOrchestrationOutcome(), true)
})

test('WHAT[MANAGED-SESSION-016] already-terminal attempt interrupt is Ok and issues transport abort for root session', async () => {
  const r = await interrupt.testAlreadyTerminalInterrupt()
  assert.equal(r.ok, true)
})
