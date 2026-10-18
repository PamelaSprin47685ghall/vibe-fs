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

test('WHAT[participant-identity-007] Predictor has a private identity and never aliases Engineer', () => {
  const predictor = Persona.resolveParticipantIdentityAtRoot('predictor')
  assert.equal(predictor.ok, true)
  assert.equal(predictor.identity.name, 'predictor')
  assert.equal(predictor.identity.role, 'predictor')
  assert.equal(predictor.identity.persona, 'Predictor')
  assert.equal(Persona.allRoleLabels.includes('predictor'), false)
  const restored = Persona.rehydrateParticipantIdentity('', 'predictor', 'predictor', 'deep', '', 'Predictor', 1, 'ResolvedAtRoot')
  assert.equal(restored.ok, true)
  assert.deepEqual(restored.identity, predictor.identity)
  const inherited = Persona.inheritParticipantIdentityFromOwner('predictor', 'manager')
  assert.equal(inherited.ok, true)
  assert.equal(inherited.identity.role, 'predictor')
  assert.equal(inherited.identity.persona, 'Lead')
  const inheritedReplay = Persona.rehydrateParticipantIdentity('manager', 'predictor', 'predictor', 'deep', '', 'Lead', 1, 'InheritedFromOwner')
  assert.equal(inheritedReplay.ok, true)
  assert.deepEqual(inheritedReplay.identity, inherited.identity)
})

test('WHAT[participant-identity-007] rejects old Predictor evidence that claims Engineer without rewriting it', () => {
  const old = { name: 'predictor', role: 'engineer', persona: 'Engineer', catalogVersion: 1, origin: 'ResolvedAtRoot' }
  const bytes = JSON.stringify(old)
  const restored = Persona.rehydrateParticipantIdentity('', old.name, old.role, 'deep', '', old.persona, old.catalogVersion, old.origin)
  assert.equal(restored.ok, false)
  assert.equal(restored.error, 'RoleMismatch')
  assert.equal(JSON.stringify(old), bytes)
  assert.equal(Persona.resolveParticipantIdentityAtRoot('engineer').identity.role, 'engineer')
})
