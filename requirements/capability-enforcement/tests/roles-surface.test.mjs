// P7a pilot: RolesSurface — role/permission vocabulary as JS-native strings.
// owner: capability-enforcement (AGENT-001 vocabulary) + participant-horizon
// (ToolPermission catalog). JS-SEMANTIC-SURFACE-002/003/005: the registered
// surface is the legal entry point; Role/ToolPermission cross as strings,
// never as Fable DU values. Matrix stays single-sourced at Roles.permissions
// (the F# core); this contract pins the string mapping.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const { allRoleLabels, allPublicRoleLabels, allInternalRoleLabels, managedAgentName, permissions, isAllowed } =
  await import('../../../dist/Foundation/RolesSurface.js')

// ── surface shape is JS-native ──────────────────────────────────────────────

test('WHAT[ENF-002] P7_SURFACE_role_labels_are_js_native_strings', () => {
  assertJsData(allRoleLabels, 'allRoleLabels')
  assert.equal(allRoleLabels.length, 10, 'exactly ten canonical roles')
  assert.deepEqual(
    allRoleLabels,
    ['blogger', 'browser', 'coder', 'devops', 'inquiry', 'inspector', 'manager', 'orchestrator', 'reviewer', 'distiller']
      .sort(),
  )
})

test('WHAT[ENF-002] P7_SURFACE_permissions_matrix_matches_the_canonical_roles_matrix', () => {
  // AGENT-001/AGENT-025: Manager/Orchestrator/Coder/Inspector/Browser/Inquiry
  // entitlement sets (the same matrix capability-enforcement pins against the
  // host schema). Values are sorted strings. Static array — no Object.entries
  // (export-discovery debt rule).
  const matrix = [
    ['manager', ['Finality', 'Fission', 'Fork', 'Horizon', 'Join', 'TodoWrite']],
    ['orchestrator', ['Fork', 'Horizon', 'Join']],
    ['coder', ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Inspect', 'Move', 'Read', 'Remove', 'Write']],
    ['inspector', ['Exec', 'Fetch', 'Fission', 'Glob', 'Grep', 'Read']],
    ['browser', ['Fission', 'Glob', 'Grep', 'Network', 'Read']],
    ['inquiry', ['Fission', 'Inspect', 'Sphinx']],
    ['reviewer', ['Glob', 'Grep', 'Judge', 'Read']],
    ['devops', ['Behavior', 'Exec', 'Glob', 'Grep', 'Horizon', 'Inspect', 'Join', 'Pty', 'Read']],
    ['distiller', []],
    ['blogger', ['Chronicle']],
  ]
  for (const [role, expected] of matrix) {
    assertJsData(permissions(role), `permissions(${role})`)
    assert.deepEqual(permissions(role), expected, `permissions(${role}) must equal the canonical matrix`)
  }
  assert.deepEqual(permissions('not-a-role'), [], 'unknown role fails closed to empty set')
})

test('WHAT[ENF-002] P7_SURFACE_public_internal_partition_and_managed_agent_name_are_js_native', () => {
  assertJsData(allPublicRoleLabels, 'allPublicRoleLabels')
  assertJsData(allInternalRoleLabels, 'allInternalRoleLabels')
  assert.deepEqual(allPublicRoleLabels, ['browser', 'coder', 'devops', 'inquiry', 'inspector', 'manager', 'orchestrator', 'reviewer'])
  assert.deepEqual(allInternalRoleLabels, ['blogger', 'distiller'])
  assert.equal(allPublicRoleLabels.length + allInternalRoleLabels.length, allRoleLabels.length)
  assert.equal(managedAgentName('fast', 'distiller'), 'fast-distiller')
  assert.equal(managedAgentName('deep', 'blogger'), 'deep-blogger')
  assert.equal(managedAgentName('fast', 'not-a-role'), '', 'unknown role fails closed to empty name')
  assert.equal(managedAgentName('not-a-tier', 'coder'), '', 'unknown tier fails closed to empty name')
})

test('WHAT[ENF-002] P7_SURFACE_isAllowed_is_default_deny_outside_the_matrix', () => {
  assert.equal(isAllowed('inquiry', 'Inspect'), true)
  assert.equal(isAllowed('inquiry', 'Sphinx'), true)
  assert.equal(isAllowed('inquiry', 'Fission'), true)
  assert.equal(isAllowed('inquiry', 'Read'), false, 'Inquiry lacks Read')
  assert.equal(isAllowed('blogger', 'Chronicle'), true, 'Blogger has exactly Chronicle')
  assert.equal(isAllowed('blogger', 'Fork'), false, 'Blogger lacks Fork')
  assert.equal(isAllowed('manager', 'Finality'), true, 'Manager has Finality')
  assert.equal(isAllowed('unknown-role', 'Fork'), false, 'unknown role → deny')
  assert.equal(isAllowed('manager', 'UnknownPermission'), false, 'unknown permission → deny')
})