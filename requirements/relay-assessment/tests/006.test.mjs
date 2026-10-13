import assert from 'node:assert/strict'
import test from 'node:test'
import { review } from '../../work/tests/support/contract.mjs'

test('WHAT[relay-assessment-006] a reviewed tenure cannot review again', () => {
  const again = review({ active: true, reviewed: true, sequence: ['S4'], projection: 'S4', incumbencyId: 'i' }, 'Promoted')
  assert.equal(again.ok, false)
  assert.deepEqual(again.written, [])
})
