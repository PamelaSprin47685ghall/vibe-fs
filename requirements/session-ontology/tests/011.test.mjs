import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)

assert.equal(linked.ok, true, linked.message)

const state = linked.value

test('WHAT[session-ontology-011] HOST_008_strength_replica_is_not_a_satellite_kind', () => {
  assert.equal(assoc.isStrengthReplicaAttachment('StrengthReplica'), true)
  for (const kind of ['Companion', 'SyncInspector', 'SyncCoder', 'Bookkeeper']) {
    assert.equal(assoc.isStrengthReplicaAttachment(kind), false)
  }
  assert.deepEqual(assoc.satelliteKinds, ['Companion'])
})
