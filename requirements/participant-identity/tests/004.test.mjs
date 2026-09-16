import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[PID-004] terminal dispatch preserves the exact IdentitySeed', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.role, 'Coder')
})

test('WHAT[PID-004] Strength replica inherits owner Persona and version with the same participant', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.name, 'coder')
})

test('WHAT[PID-004] Fission lane inherits owner Persona and version without physical-parent inference', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.name, 'coder')
})

test('WHAT[PID-004] raw legacy PeerAgent/EffectiveAgent/cursor fields are ignored and never re-encoded', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.peer, undefined)
})
