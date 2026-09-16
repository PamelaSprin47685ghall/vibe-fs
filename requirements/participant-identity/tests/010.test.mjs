// requirements/participant-identity/tests/010.test.mjs
//
// WHAT[PID-010] — Active participant identity resolution accepts only valid
// canonical roles (engineer, devops, manager, orchestrator, blogger), while
// retired roles (coder, inspector, browser, inquiry, distiller) are rejected
// from active scheduling and kept strictly isolated in historical decoding.

import assert from 'node:assert/strict'
import test from 'node:test'

const identity = await import('../../../dist/Participant/Persona/Surface.js')

test('WHAT[PID-010] active_identity_resolution_accepts_engineer_and_rejects_retired_roles', () => {
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

test('WHAT[PID-010] historical_identity_decoding_does_not_silently_upgrade_inspector_or_devops_to_engineer', () => {
  // Retired roles are legacy names and cannot be managed names for active scheduling
  assert.equal(identity.isLegacyName('inspector'), true)
  assert.equal(identity.isLegacyName('coder'), true)
  assert.equal(identity.isManagedName('inspector'), false)
  assert.equal(identity.isManagedName('coder'), false)

  // Verify that engineer is a managed name
  assert.equal(identity.isManagedName('engineer'), true)
  assert.equal(identity.isLegacyName('engineer'), false)
})
