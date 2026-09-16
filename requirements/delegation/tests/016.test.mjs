import assert from 'node:assert/strict'
import test from 'node:test'
import * as wire from '../../../dist/Execution/Delegation/JoinV2WireSurface.js'

test('WHAT[DELEG-016] JOIN_V2_empty_batch_is_plain_empty_wire', () => {
  assert.equal(wire.renderBatch([]), '')
})
