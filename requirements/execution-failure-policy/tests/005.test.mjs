import assert from 'node:assert/strict'
import test from 'node:test'
import * as policy from '../../../dist/Execution/Failure/Surface.js'
import { input, executionKey } from './support/policy-input.mjs'

const decide = (change) => policy.decide(input(change))

test('WHAT[execution-failure-policy-005] terminal commands carry phase exact execution and typed disposition', () => {
  for (const [phase, resolution] of [
    ['NoAcceptedFact', 'PreserveCurrentFact'], ['AcceptedBeforeProvider', 'TerminalizeAcceptedPreProvider'],
    ['ProviderStarted', 'TerminalizeProviderStarted'], ['Terminal', 'PreserveCurrentFact'],
  ]) {
    const decision = decide({ phase, failure: 'AuthorizationDenied' })
    assert.equal(decision.resolution, resolution)
    assert.equal(decision.authorization, null)
    if (resolution.startsWith('Terminalize')) {
      assert.deepEqual(decision.executionKey, executionKey)
      assert.equal(decision.terminalDisposition, 'Rejected')
    } else {
      assert.equal(decision.terminalDisposition, null)
    }
  }
  assert.equal(decide({ failure: 'UserCancelled' }).resolution, 'TerminalizeProviderStarted')
  assert.equal(decide({ failure: 'UserCancelled' }).terminalDisposition, 'Cancelled')
  assert.equal(decide({ failure: 'Superseded' }).terminalDisposition, 'Cancelled')
  assert.equal(decide({ failure: 'StreamInterruptedAfterFirstToken' }).terminalDisposition, 'Failed')
  const otherKey = { sessionId: 'other-session', physicalUserMessageId: 'other-message' }
  assert.deepEqual(decide({ executionKey: otherKey }).executionKey, otherKey)
})

test.todo('WHAT[execution-failure-policy-005] GAP-120 actual terminal owner rejects a wrong key and a cross-phase command without altering either execution')
