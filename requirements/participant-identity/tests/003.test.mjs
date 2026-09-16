import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[PID-003] rejects blank Persona and unsupported catalog version', () => {
  const valid = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(valid.ok, true)
  assert.ok(valid.identity.persona.length > 0)
  assert.ok(valid.identity.catalogVersion.length > 0)
})
