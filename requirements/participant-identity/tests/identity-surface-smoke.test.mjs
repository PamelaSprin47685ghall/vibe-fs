import assert from 'node:assert/strict'
import test from 'node:test'

import * as fission from '../../../dist/Execution/Fission/Surface.js'
import * as roles from '../../../dist/Foundation/RolesSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as identity from '../../../dist/Participant/Persona/Surface.js'
import * as journalCodec from '../../../dist/Persistence/Journal/CodecSurface.js'
import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

test('WHAT[PID-001] registered identity surfaces load and expose their narrow contracts', async () => {
  const coder = identity.resolveParticipantIdentityAtRoot('coder')
  assert.equal(coder.ok, true)
  assert.equal(coder.identity.peer, 'coder')
  assert.equal(authority.promotePhysical('msg_identity_surface_smoke'), 'msg_identity_surface_smoke')
  assert.equal(journalCodec.deserialize('{}').ok, false)
  assert.equal(factCodec.containsLegacyFallbackFields('{}'), false)
  assert.deepEqual(fission.ringMergeOrder(1), [])
  assert.equal(identity.nameOf('fast', 'coder'), 'coder')
  assert.equal(identity.nameOf('deep', 'coder'), 'coder')
  assert.deepEqual(roles.allInternalRoleLabels, ['blogger', 'distiller'])
})
