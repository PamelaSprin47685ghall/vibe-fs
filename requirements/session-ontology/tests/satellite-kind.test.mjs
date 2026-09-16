// SESSION-ONTOLOGY proof — SatelliteKind is a one-case durable concept.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'

test('WHAT[SESSION-ONTOLOGY-014] HOST_014_satellite_kind_is_companion_only', () => {
  assert.deepEqual(assoc.satelliteKinds, ['Companion'])
  assert.equal(assoc.satelliteKinds.includes('Teacher'), false)
})
