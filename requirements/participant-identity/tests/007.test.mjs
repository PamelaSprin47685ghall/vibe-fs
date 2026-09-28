import assert from 'node:assert/strict'
import test from 'node:test'
import * as Persona from '../../../dist/Participant/Persona/Surface.js'
import * as Roles from '../../../dist/Foundation/RolesSurface.js'

test('WHAT[participant-identity-007] Bookkeeper has private identity and no public Role', () => {
  const bookkeeper = Persona.resolveParticipantIdentityAtRoot('bookkeeper')

  assert.equal(bookkeeper.ok, true, bookkeeper.ok ? '' : bookkeeper.error)
  assert.equal(bookkeeper.identity.name, 'bookkeeper')
  assert.equal(bookkeeper.identity.role, 'bookkeeper')
  assert.equal(bookkeeper.identity.persona, 'Curator')
  assert.equal(bookkeeper.identity.catalogVersion, 1)
  assert.equal(Persona.allPublicRoleLabels.includes('bookkeeper'), false)
  assert.equal(Persona.allRoleLabels.includes('bookkeeper'), false)
  assert.equal(Persona.isManagedName('bookkeeper'), true)
  assert.equal(Roles.allRoleLabels.includes('bookkeeper'), false)
  assert.equal(Persona.nameOf('deep', 'bookkeeper'), '')
})
