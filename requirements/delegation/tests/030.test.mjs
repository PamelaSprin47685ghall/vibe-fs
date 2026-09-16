import assert from 'node:assert/strict'
import test from 'node:test'
import * as m6 from '../../../dist/Execution/Delegation/M6SliceBoundarySurface.js'

test('WHAT[DELEG-030] delegation invariant fatal preserves settlement and one injected fuse', () => {
  assert.equal(m6.hasInjectedFatalFuse(), true)
})
