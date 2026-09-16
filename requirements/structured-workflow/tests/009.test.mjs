import assert from 'node:assert/strict'
import test from 'node:test'

import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

test('WHAT[STRUCTURED-WORKFLOW-009] operator abort is a control-plane wake, never a business outcome', () => {
  assert.equal(typeof ReconcileSurface.isAbortControlPlaneWake, 'function')
})
