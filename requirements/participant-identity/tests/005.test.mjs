import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[PID-005] provider planning selects the system prompt and tool set from profile Role', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.role, 'Coder')
})
