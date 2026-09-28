import assert from 'node:assert/strict'
import test from 'node:test'
import * as identity from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[managed-session-lifecycle-023] production identity resolution rejects legacy names without promoting them', () => {
  for (const name of identity.legacyNames) {
    const result = identity.resolveParticipantIdentityAtRoot(name)
    assert.equal(result.ok, false, name)
    assert.equal(result.identity, null, name)
    assert.equal(result.error, 'LegacyParticipantName', name)
  }
  const engineer = identity.resolveParticipantIdentityAtRoot('engineer')
  assert.equal(engineer.ok, true)
  assert.equal(engineer.identity.role, 'engineer')
})

test.todo('WHAT[managed-session-lifecycle-023] discovering a historical active role through recovery durably retires it and drains resources without upgrading authority (GAP-133, GAP-124)')
