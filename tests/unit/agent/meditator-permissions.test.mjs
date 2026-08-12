// tests/unit/agent/meditator-permissions.test.mjs — G3 Meditator capability matrix.
//
// Meditator is an Inspector-only reasoner: Roles.permissions carries Inspector
// and must not carry Read/Glob/Grep. Host schema, PromptAuthority.toolCapabilitiesFor,
// and ToolRegistry.rolePredicate all derive from that set (or deny when the
// capability is absent).

import assert from 'node:assert/strict'
import test from 'node:test'

import { ProviderRequestKind } from '../../../dist/Domain/PrefixCandidate.js'
import { toolCapabilitiesFor } from '../../../dist/Domain/PromptAuthority.js'
import { ToolRegistry_rolePredicate as rolePredicate } from '../../../dist/Infrastructure/OpenCode/Tools/ToolRegistry.js'
import { Role, Roles_isAllowed as isAllowed, ToolPermission } from '../../../dist/Kernel/Roles.js'
import { StaticTools_toolName as toolName } from '../../../dist/Tools/StaticTools.js'
import { toArray as setToArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'

import { managedAgentConfig, roles, runtimeResources } from '../support/domain.mjs'

const names = (permissions) => setToArray(permissions).map(toolName).sort()

const READ_TOOLS = ['read', 'glob', 'grep']
const READ_PERMISSIONS = [ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep]

test.before(() => {
  runtimeResources.installFromPackage()
})

test('G3_Meditator_permissions_are_inspector_only', () => {
  const allowed = roles.permissions(roles.of('Meditator'))
  assert.deepEqual(allowed, ['Inspector'])
  assert.equal(allowed.includes('Read'), false)
  assert.equal(allowed.includes('Glob'), false)
  assert.equal(allowed.includes('Grep'), false)
})

test('G3_Meditator_isAllowed_denies_read_glob_grep_and_allows_inspector', () => {
  assert.equal(isAllowed(Role.Meditator, ToolPermission.Inspector), true)
  for (const permission of READ_PERMISSIONS) {
    assert.equal(isAllowed(Role.Meditator, permission), false, `Meditator must lack ${permission}`)
  }
})

test('G3_Meditator_toolCapabilitiesFor_WorkMain_matches_Roles_permissions', () => {
  const caps = names(toolCapabilitiesFor(Role.Meditator, ProviderRequestKind.WorkMain))
  assert.deepEqual(caps, ['inspector'])
  for (const tool of READ_TOOLS) {
    assert.equal(caps.includes(tool), false, `toolCapabilitiesFor must omit ${tool}`)
  }
})

test('G3_Meditator_rolePredicate_inspector_allow_and_host_native_read_gap', () => {
  // Gap note: read/glob/grep are Host-native builtins — they are NOT ToolRegistry
  // specs, so rolePredicate has no isAllowed cases for them and falls through to
  // default deny. The real Meditator gate for those tools is Roles.permissions /
  // Host permission schema (asserted above), not a ToolRegistry rolePredicate.
  for (const tool of READ_TOOLS) {
    const allowed = rolePredicate(tool, undefined, 'ses-meditator')
    assert.equal(typeof allowed, 'function', `rolePredicate(${tool}) returns default-deny fn`)
    assert.equal(allowed(Role.Meditator), false, `default deny for Host-native ${tool}`)
    assert.equal(allowed(Role.Inspector), false, `not role-gated: even Inspector is denied here`)
  }

  // inspector IS ToolRegistry-gated via Roles_isAllowed(Inspector).
  const inspector = rolePredicate('inspector', undefined, 'ses-meditator')
  assert.equal(inspector(Role.Meditator), true, 'rolePredicate(inspector) must allow Meditator')
})

test('G3_Meditator_host_schema_allow_list_is_inspector_only', () => {
  const config = {
    agent: {
      'fast-meditator': { model: 'fast-meditator-model' },
      'deep-meditator': { model: 'deep-meditator-model' },
    },
  }
  // Fill the rest of the 20 required agents so configure accepts the config.
  for (const tier of ['fast', 'deep']) {
    for (const role of [
      'manager',
      'orchestrator',
      'coder',
      'inspector',
      'browser',
      'reviewer',
      'devops',
      'executor',
      'blogger',
    ]) {
      const name = `${tier}-${role}`
      if (!config.agent[name]) config.agent[name] = { model: `${name}-model` }
    }
  }

  const outcome = managedAgentConfig.configure(config)
  assert.equal(outcome.ok, true, outcome.error)

  for (const name of ['fast-meditator', 'deep-meditator']) {
    const permission = config.agent[name].permission
    assert.equal(permission['*'], 'deny')
    assert.equal(permission.inspector, 'allow')
    for (const tool of READ_TOOLS) {
      assert.notEqual(permission[tool], 'allow', `${name} must not allow ${tool}`)
    }
    assert.notEqual(permission['stealth-browser-mcp_*'], 'allow', `${name} must not allow stealth-browser MCP`)
  }
})
