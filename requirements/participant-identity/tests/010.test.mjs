// requirements/participant-identity/tests/010.test.mjs
//
// WHAT[participant-identity-010] — Active participant identity resolution accepts only valid
// canonical roles (engineer, devops, manager, orchestrator, blogger), while
// retired roles (coder, inspector, browser, inquiry, distiller) are rejected
// from active scheduling and kept strictly isolated in historical decoding.

import assert from 'node:assert/strict'
import test from 'node:test'

const identity = await import('../../../dist/Participant/Persona/Surface.js')
const officeCap = await import('../../../dist/Participant/Persona/OfficeCapabilitySurface.js')

test('WHAT[participant-identity-010] active_identity_resolution_accepts_engineer_and_rejects_retired_roles', () => {
  const engineer = identity.resolveParticipantIdentityAtRoot('engineer')
  assert.equal(engineer.ok, true, 'engineer must be accepted as canonical active identity')
  assert.equal(engineer.identity.name, 'engineer')
  assert.equal(engineer.identity.role, 'engineer')
  assert.equal(engineer.identity.persona, 'Engineer')

  const retired = ['coder', 'inspector', 'browser', 'inquiry', 'distiller']
  for (const role of retired) {
    const resolved = identity.resolveParticipantIdentityAtRoot(role)
    assert.equal(
      resolved.ok,
      false,
      `retired role '${role}' must not resolve as an active participant identity`,
    )
  }
})

test('WHAT[participant-identity-010] historical_identity_decoding_does_not_silently_upgrade_inspector_or_devops_to_engineer', () => {
  // 1. Retired roles are legacy names and cannot be managed names for active scheduling
  assert.equal(identity.isLegacyName('inspector'), true)
  assert.equal(identity.isLegacyName('coder'), true)
  assert.equal(identity.isManagedName('inspector'), false)
  assert.equal(identity.isManagedName('coder'), false)

  // Verify that engineer is a managed name
  assert.equal(identity.isManagedName('engineer'), true)
  assert.equal(identity.isLegacyName('engineer'), false)

  // 2. Historical rehydration preserves inspector without silently upgrading to engineer
  const rehydrated = identity.rehydrateParticipantIdentity(
    'manager',
    'inspector',
    'inspector',
    'deep',
    'inspector',
    'Lead',
    1,
    'InheritedFromOwner',
  )
  assert.equal(rehydrated.ok, true, 'historical rehydration of inspector must succeed')
  assert.equal(rehydrated.identity.role, 'inspector', 'historical role must stay inspector')
  assert.notEqual(rehydrated.identity.role, 'engineer', 'must never be upgraded to engineer')

  // 3. Historical inspector permissions must be empty set
  const inspectorPerms = officeCap.permissions('inspector')
  assert.deepEqual(inspectorPerms, [], 'historical inspector permissions must be empty set')
  assert.equal(officeCap.isAllowed('inspector', 'Write'), false, 'inspector must not have Write capability')
  assert.equal(officeCap.isAllowed('inspector', 'Fission'), false, 'inspector must not have Fission capability')

  // 4. Attempting to rehydrate a legacy participant with upgraded engineer role fails closed
  const upgraded = identity.rehydrateParticipantIdentity(
    'manager',
    'inspector',
    'engineer',
    'deep',
    'inspector',
    'Lead',
    1,
    'InheritedFromOwner',
  )
  assert.equal(upgraded.ok, false, 'attempting to rehydrate legacy participant with engineer role must fail')

  // 5. Historical devops has no Fission capability
  assert.equal(officeCap.isAllowed('devops', 'Fission'), false, 'devops must never have Fission capability')
  assert.equal(officeCap.permissions('devops').includes('Fission'), false, 'devops permission list must not include Fission')
})
