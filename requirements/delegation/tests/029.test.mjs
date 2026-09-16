import assert from 'node:assert/strict'
import test from 'node:test'
import * as m6 from '../../../dist/Execution/Delegation/M6SliceBoundarySurface.js'

test('WHAT[DELEG-029] delegation runtime consumes only the delegation-owned journal port', () => {
  assert.equal(m6.isJournalPortIsolated(), true)
})
