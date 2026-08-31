import assert from 'node:assert/strict'
import test from 'node:test'

import {
  acceptHumanRoot,
  cursor,
  fallbackProjection,
  fold,
} from '../../../dist/Participant/Provider/Attempt/Fallback/CursorSurface.js'
import {
  recordConfirmedFailure,
  snapshot,
} from '../../../dist/Participant/Provider/Attempt/Fallback/HandleSurface.js'

test('WHAT[VERIFICATION-SYSTEM-008] fallback recovery has one importable production surface', () => {
  assert.equal(typeof cursor, 'object')
  assert.equal(typeof fallbackProjection, 'object')
  assert.equal(typeof fold, 'function')
  assert.equal(typeof acceptHumanRoot, 'function')
  assert.equal(typeof recordConfirmedFailure, 'function')
  assert.equal(typeof snapshot, 'function')
})
