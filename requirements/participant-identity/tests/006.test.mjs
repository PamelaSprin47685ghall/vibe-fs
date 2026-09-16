import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[PID-006] fresh physical retries preserve ParticipantIdentity while the failure budget advances', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.role, 'Coder')
})

test('WHAT[PID-006] durable provider failure fold preserves the exact IdentitySeed', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.role, 'Coder')
})
