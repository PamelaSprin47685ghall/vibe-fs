
import assert from 'node:assert/strict'
import test from 'node:test'
import * as XWireSurface from '../../../dist/Context/Prefix/XWireSurface.js'

test('WHAT[PREFIX-STABILITY-001] compiled XWireSurface exposes the production horizon decision', () => {
  assert.equal(XWireSurface.presentationHorizon(false), 'Current')
  assert.equal(XWireSurface.presentationHorizon(true), 'TentativeCold')
})

test('WHAT[PREFIX-STABILITY-001] compiled XWireSurface reconciles completion and failure', () => {
  assert.deepEqual(
    XWireSurface.reconcile({
      hasPlan: true,
      outcome: 'completed',
      hasProbe: true,
      currentEpoch: 4,
      probeEpoch: 4,
    }),
    { promoted: true, cleared: true, keptPlan: false },
  )

  assert.deepEqual(
    XWireSurface.reconcile({ hasPlan: true, outcome: 'failed', hasProbe: true }),
    { promoted: false, cleared: true, keptPlan: false },
  )
})
