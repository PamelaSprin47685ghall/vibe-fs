import assert from 'node:assert/strict'
import test from 'node:test'
import * as successor from '../../../dist/Execution/Session/InterruptSuccessorSurface.js'

test('WHAT[MANAGED-SESSION-017] fail-closed interrupt becomes Failed terminal so fork completion wakes parent', () => {
  assert.equal(successor.testFailClosedBecomesFailedTerminal(), true)
})

test('WHAT[MANAGED-SESSION-017] invariant and tool fail-closed paths cannot use orphan InterruptAttempt', () => {
  assert.equal(successor.testFailClosedNoOrphanInterrupt(), true)
})

test('WHAT[MANAGED-SESSION-017] raw InterruptAttempt callers are restricted to workflows with an explicit successor owner', () => {
  assert.equal(successor.testRawInterruptRestricted(), true)
})

test('WHAT[MANAGED-SESSION-017] fatal termination never stores cross-callback cause state', () => {
  assert.equal(successor.testFatalTerminationNoCrossCallbackState(), true)
})
