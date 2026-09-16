import assert from 'node:assert/strict'
import test from 'node:test'
import * as boundary from '../../../dist/Persistence/SliceBoundarySurface.js'

test('WHAT[DURABLE-EVENTS-024] semantic cut fatal requires settlement and one injected physical fuse', () => {
  assert.equal(boundary.semanticCutRequiresSettlement(), true)
  assert.equal(boundary.hasInjectedPhysicalFuse(), true)
})
