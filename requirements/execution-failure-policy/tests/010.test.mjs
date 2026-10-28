import assert from 'node:assert/strict'
import test from 'node:test'
import { check } from '../../../scripts/checks/fatal-inventory-gate.mjs'

test('WHAT[execution-failure-policy-010] actual production fatal locations remain indexed by the standard gate', () => {
  assert.deepEqual(check().issues, [])
})

test.todo('WHAT[execution-failure-policy-010] GAP-121 every retained fuse has actual phase-specific proof; source and test file existence do not establish unreachable branches')
