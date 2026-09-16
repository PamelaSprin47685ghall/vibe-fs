import assert from 'node:assert/strict'
import test from 'node:test'
import * as retryPolicy from '../../../dist/Context/Companion/RetryPolicySurface.js'

test('WHAT[CONTEXT-COMPRESSION-007] same_failed_kind_with_same_material_always_selects_the_same_next_request', () => {
  const r1 = retryPolicy.nextRequest('BloggerMain', true)
  const r2 = retryPolicy.nextRequest('BloggerMain', true)
  assert.equal(r1, r2)
})
