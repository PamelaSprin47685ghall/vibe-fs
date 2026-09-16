// P7a pilot: RolesSurface — participant identity as JS-native strings.
// JS-SEMANTIC-SURFACE-002/003/005: the registered surface is the legal entry
// point; Role and AgentTier cross as strings, never as Fable DU values.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const { allRoleLabels, allPublicRoleLabels, allInternalRoleLabels } =
  await import('../../../dist/Foundation/RolesSurface.js')
const { nameOf: managedAgentName } = await import('../../../dist/Participant/Persona/Surface.js')

// ── surface shape is JS-native ──────────────────────────────────────────────

test('WHAT[ENF-002] P7_SURFACE_role_labels_are_js_native_strings', () => {
  assertJsData(allRoleLabels, 'allRoleLabels')
  assert.equal(allRoleLabels.length, 9, 'exactly nine canonical roles')
  assert.deepEqual(
    allRoleLabels,
    ['blogger', 'browser', 'coder', 'devops', 'inquiry', 'inspector', 'manager', 'orchestrator', 'distiller']
      .sort(),
  )
})

test('WHAT[ENF-002] P7_SURFACE_public_internal_partition_and_managed_agent_name_are_js_native', () => {
  assertJsData(allPublicRoleLabels, 'allPublicRoleLabels')
  assertJsData(allInternalRoleLabels, 'allInternalRoleLabels')
  assert.deepEqual(allPublicRoleLabels, ['browser', 'coder', 'devops', 'inquiry', 'inspector', 'manager', 'orchestrator'])
  assert.deepEqual(allInternalRoleLabels, ['blogger', 'distiller'])
  assert.equal(allPublicRoleLabels.length + allInternalRoleLabels.length, allRoleLabels.length)
  assert.equal(managedAgentName('fast', 'distiller'), 'distiller')
  assert.equal(managedAgentName('deep', 'blogger'), 'blogger')
  assert.equal(managedAgentName('coder', 'coder'), 'coder')
  assert.equal(managedAgentName('fast', 'not-a-role'), '', 'unknown role fails closed to empty name')
  assert.equal(managedAgentName('not-a-tier', 'coder'), 'coder', 'canonical role resolves without a tier')
})