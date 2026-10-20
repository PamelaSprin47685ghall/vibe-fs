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

test('WHAT[participant-identity-007] retired Predictor cannot resolve, inherit or restore an active identity', () => {
  const predictor = Persona.resolveParticipantIdentityAtRoot('predictor')
  assert.equal(predictor.ok, false)
  assert.equal(predictor.error, 'UnknownParticipantName')
  assert.equal(Persona.allRoleLabels.includes('predictor'), false)
  const restored = Persona.rehydrateParticipantIdentity('', 'predictor', 'predictor', 'deep', '', 'Predictor', 1, 'ResolvedAtRoot')
  assert.equal(restored.ok, false)
  const inherited = Persona.inheritParticipantIdentityFromOwner('predictor', 'manager')
  assert.equal(inherited.ok, false)
  const inheritedReplay = Persona.rehydrateParticipantIdentity('manager', 'predictor', 'predictor', 'deep', '', 'Lead', 1, 'InheritedFromOwner')
  assert.equal(inheritedReplay.ok, false)
})

test('WHAT[participant-identity-007] rejects old Predictor evidence that claims Engineer without rewriting it', () => {
  const old = { name: 'predictor', role: 'engineer', persona: 'Engineer', catalogVersion: 1, origin: 'ResolvedAtRoot' }
  const bytes = JSON.stringify(old)
  const restored = Persona.rehydrateParticipantIdentity('', old.name, old.role, 'deep', '', old.persona, old.catalogVersion, old.origin)
  assert.equal(restored.ok, false)
  assert.equal(restored.error, 'UnknownParticipantName')
  assert.equal(JSON.stringify(old), bytes)
  assert.equal(Persona.resolveParticipantIdentityAtRoot('engineer').identity.role, 'engineer')
})
