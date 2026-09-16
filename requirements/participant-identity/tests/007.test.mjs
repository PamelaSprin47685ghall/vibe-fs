import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[PID-007] Bookkeeper has private identity and no public Role', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('bookkeeper')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.name, 'bookkeeper')
})
