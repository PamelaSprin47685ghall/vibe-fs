import assert from 'node:assert/strict'
import test from 'node:test'

import {
  acceptHumanRoot,
  budget,
  providerFailureProjection,
  fold,
  recordConfirmedFailure,
  snapshot,
} from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

test('WHAT[VERIFICATION-SYSTEM-008] provider failure has one importable production surface', () => {
  assert.equal(typeof budget, 'object')
  assert.equal(typeof providerFailureProjection, 'object')
  assert.equal(typeof fold, 'function')
  assert.equal(typeof acceptHumanRoot, 'function')
  assert.equal(typeof recordConfirmedFailure, 'function')
  assert.equal(typeof snapshot, 'function')
})
