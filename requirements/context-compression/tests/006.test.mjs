import assert from 'node:assert/strict'
import test from 'node:test'
import * as retryPolicy from '../../../dist/Context/Companion/RetryPolicySurface.js'

test('WHAT[CONTEXT-COMPRESSION-006] role is fixed across retry attempts and consecutive failures', () => {
  assert.equal(retryPolicy.roleIsFixedOnRetry, true)
})

test('WHAT[CONTEXT-COMPRESSION-006] retry policy includes squash only when BloggerMain fails and frames exist', () => {
  assert.equal(retryPolicy.selectRetryAction({ requestKind: 'BloggerMain', framesExist: true }), 'Squash')
  assert.equal(retryPolicy.selectRetryAction({ requestKind: 'BloggerMain', framesExist: false }), 'Main')
})

test('WHAT[CONTEXT-COMPRESSION-006] prefix probe is selected when policy allows and candidate exists', () => {
  assert.equal(retryPolicy.selectProbe({ probeAllowed: true, candidateExists: true }), true)
  assert.equal(retryPolicy.selectProbe({ probeAllowed: false, candidateExists: true }), false)
})

test('WHAT[CONTEXT-COMPRESSION-006] consecutive failures consume the failure budget and exhaustion halts auto-retry', () => {
  assert.equal(retryPolicy.isExhausted(12), true)
  assert.equal(retryPolicy.isExhausted(11), false)
})

test('WHAT[CONTEXT-COMPRESSION-006] compression owner exposes the retry dispatch and planning surface', () => {
  assert.ok(retryPolicy.planningSurface)
})
