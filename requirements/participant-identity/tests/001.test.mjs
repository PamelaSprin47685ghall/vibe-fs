import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'
import * as catalog from '../../../dist/Participant/Persona/Catalog.js'
import * as boundary from '../../../dist/Interaction/Authority/BoundarySurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as roles from '../../../dist/Foundation/RolesSurface.js'

test('WHAT[PID-001] catalog_has_exactly_ten_canonical_roles', () => {
  const ten = ['Manager', 'Orchestrator', 'Coder', 'Inspector', 'Browser', 'Inquiry', 'Reviewer', 'DevOps', 'Distiller', 'Blogger']
  for (const role of ten) {
    assert.ok(catalog.isCanonicalRoleName(role), `missing canonical role: ${role}`)
  }
  assert.equal(catalog.canonicalRoleCount(), 10)
})

test('WHAT[PID-001] required_names_are_canonical_and_include_twelve_agents', () => {
  const twelve = ['manager', 'orchestrator', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'devops', 'distiller', 'blogger', 'bookkeeper', 'predictor']
  for (const agent of twelve) {
    assert.ok(catalog.isRequiredAgentName(agent), `missing required agent: ${agent}`)
  }
})

test('WHAT[PID-001] rejects SessionId keyed identity cache', () => {
  const result = boundary.verifyParticipantIdentityBoundary()
  assert.equal(result.clean, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[PID-001] registered identity surfaces load and expose their narrow contracts', () => {
  assert.ok(typeof persona.resolveParticipantIdentityAtRoot === 'function')
  assert.ok(typeof authority.createAuthorityRoot === 'function')
  assert.ok(typeof boundary.verifyParticipantIdentityBoundary === 'function')
  assert.ok(Array.isArray(roles.allRoleLabels))
})

test('WHAT[PID-001] resolves every canonical participant identity and persona', () => {
  const names = ['manager', 'orchestrator', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'devops', 'distiller', 'blogger', 'bookkeeper', 'predictor']
  for (const name of names) {
    const resolved = persona.resolveParticipantIdentityAtRoot(name)
    assert.equal(resolved.ok, true, `failed for ${name}: ${resolved.error}`)
    assert.equal(resolved.identity.name, name)
    assert.ok(resolved.identity.persona.length > 0)
    assert.ok(resolved.identity.role.length > 0)
    assert.equal(resolved.identity.catalogVersion, persona.currentCatalogVersion)
  }
})

test('WHAT[PID-001] rejects legacy, malformed, blank, and unknown participant names', () => {
  for (const bad of ['', '   ', 'unknown', 'root', 'user', 'system', 'agent-1', null, undefined]) {
    const resolved = persona.resolveParticipantIdentityAtRoot(bad)
    assert.equal(resolved.ok, false, `expected rejection for ${bad}`)
  }
})

test('WHAT[PID-001] rejects independently supplied role, persona, and origin', () => {
  const resolved = persona.resolveParticipantIdentityAtRoot('coder')
  assert.equal(resolved.ok, true)
  assert.equal(resolved.identity.name, 'coder')
  assert.equal(resolved.identity.role, 'Coder')
})
