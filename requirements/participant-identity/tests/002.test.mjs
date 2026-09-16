import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'
import * as catalog from '../../../dist/Participant/Persona/Catalog.js'
import * as boundary from '../../../dist/Interaction/Authority/BoundarySurface.js'

test('WHAT[PID-002] persona_catalog_maps_roles_to_single_persona', () => {
  const rolesList = ['Manager', 'Orchestrator', 'Coder', 'Inspector', 'Browser', 'Inquiry', 'Reviewer', 'DevOps', 'Distiller', 'Blogger']
  for (const role of rolesList) {
    const entry = catalog.lookupCanonicalRole(role)
    assert.ok(entry, `no catalog entry for ${role}`)
    assert.ok(entry.personaText.length > 0)
  }
})

test('WHAT[PID-002] all_legacy_bare_names_are_rejected', () => {
  for (const bare of ['fast-coder', 'deep-coder', 'fast-inspector', 'deep-inspector', 'fast-devops', 'deep-devops']) {
    assert.equal(catalog.lookupRequiredAgent(bare), null)
  }
})

test('WHAT[PID-002] rejection_prose_is_version_agnostic', () => {
  const prose = catalog.unsupportedCatalogVersionProse('v999')
  assert.ok(prose.includes('v999'))
})

test('WHAT[PID-002] the identity boundary entries are accepted for clean authority shape', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.equal(report.clean, true)
})

test('WHAT[PID-002] AuthorityRootAcceptedPayload must store the IdentitySeed', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.equal(report.hasIdentitySeedInPayload, true)
})

test('WHAT[PID-002] AuthorityRootAcceptedPayload cannot flatten IdentitySeed fields', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.equal(report.hasFlattenedIdentityFields, false)
})

test('WHAT[PID-002] AuthorityExecutionProfile must derive identity from its seed', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.equal(report.profileDerivesFromSeed, true)
})

test('WHAT[PID-002] a SessionId-keyed identity collection is forbidden', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.equal(report.hasSessionIdIdentityMap, false)
})

test('WHAT[PID-002] a SessionId-keyed identity registry is forbidden', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.equal(report.hasSessionIdIdentityRegistry, false)
})

test('WHAT[PID-002] a second identity fact owner is forbidden', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.equal(report.hasSecondIdentityFactOwner, false)
})

test('WHAT[PID-002] the production participant identity boundary is clean', () => {
  const report = boundary.verifyParticipantIdentityBoundary()
  assert.deepEqual(report.violations, [])
})

test('WHAT[PID-002] ProviderAttempt carries its ParticipantIdentity as one nested value', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(typeof resolved.identity, 'object')
})

test('WHAT[PID-002] the retired peer slot is ignored and never affects identity', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.name, 'coder')
})
