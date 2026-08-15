// Moved from tests/unit/agent/inquiry-permissions.test.mjs (cutover Wave 2a); owner: capability-enforcement.
//
// Inquiry capability matrix (AGENT-025 / AGENT-030): ENF-010 role-predicate gate +
// ENF-007 MCP wildcard schema mechanism.
//
// Inquiry = SyncDelegate inspect + Sphinx MCP + Fission: Roles.permissions carries Inspect,
// Sphinx and Fission; must not carry Read/Glob/Grep. Host schema, PromptAuthority.toolCapabilitiesFor,
// and ToolRegistry.rolePredicate all derive from that set (or deny when the
// capability is absent).
// (office-capability OFF-014 / repository-investigation REPOSITORY-INVESTIGATION-003
// REUSE the Inquiry tool-face fact; physical file home is this package.)

import assert from 'node:assert/strict'
import test from 'node:test'

import { ProviderRequestKind } from '../../../dist/Context/Prefix/Candidate.js'
import { toolCapabilitiesFor } from '../../../dist/Interaction/Authority/Model.js'
import { ToolRegistry_rolePredicate as rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistry.js'
import { Role, ToolPermission } from '../../../dist/Foundation/Roles.js'
import { StaticTools_toolName as toolName } from '../../../dist/OpenCode/Tools/StaticTools.js'
import { permissions as rolePermissions, isAllowed as surfaceIsAllowed } from '../../../dist/Foundation/RolesSurface.js'

import { managedAgentConfig, runtimeResources, setItems } from '../../verification-system/tests/support/domain.mjs'

const names = (permissions) => setItems(permissions).map(toolName).sort()

const READ_TOOLS = ['read', 'glob', 'grep']
const READ_PERMISSIONS = [ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep]

test.before(() => {
  runtimeResources.installFromPackage()
})

test('Inquiry_permissions_are_inspect_sphinx_and_fission', () => {
  const allowed = rolePermissions('inquiry')
  assert.deepEqual(allowed, ['Fission', 'Inspect', 'Sphinx'])
  assert.equal(allowed.includes('Read'), false)
  assert.equal(allowed.includes('Glob'), false)
  assert.equal(allowed.includes('Grep'), false)
})

test('Inquiry_isAllowed_denies_read_glob_grep_and_allows_inspect_sphinx_fission', () => {
  assert.equal(surfaceIsAllowed('inquiry', 'Inspect'), true)
  assert.equal(surfaceIsAllowed('inquiry', 'Sphinx'), true)
  assert.equal(surfaceIsAllowed('inquiry', 'Fission'), true)
  for (const permission of READ_PERMISSIONS) {
    assert.equal(surfaceIsAllowed('inquiry', permission), false, `Inquiry must lack ${permission}`)
  }
})

test('Inquiry_toolCapabilitiesFor_WorkMain_matches_Roles_permissions', () => {
  const caps = names(toolCapabilitiesFor(Role.Inquiry, ProviderRequestKind.WorkMain))
  assert.deepEqual(caps, ['fission', 'inspect', 'sphinx_*'])
  for (const tool of READ_TOOLS) {
    assert.equal(caps.includes(tool), false, `toolCapabilitiesFor must omit ${tool}`)
  }
})

test('Inquiry_rolePredicate_inspector_allow_and_host_native_read_gap', () => {
  // Gap note: read/glob/grep are Host-native builtins — they are NOT ToolRegistry
  // specs, so rolePredicate has no isAllowed cases for them and falls through to
  // default deny. The real Inquiry gate for those tools is Roles.permissions /
  // Host permission schema (asserted above), not a ToolRegistry rolePredicate.
  for (const tool of READ_TOOLS) {
    const allowed = rolePredicate(tool, undefined, 'ses-inquiry')
    assert.equal(typeof allowed, 'function', `rolePredicate(${tool}) returns default-deny fn`)
    assert.equal(allowed(Role.Inquiry), false, `default deny for Host-native ${tool}`)
    assert.equal(allowed(Role.Inspector), false, `not role-gated: even Inspector is denied here`)
  }

  const inspect = rolePredicate('inspect', undefined, 'ses-inquiry')
  assert.equal(inspect(Role.Inquiry), true, 'rolePredicate(inspect) must allow Inquiry')
})

test('Inquiry_host_schema_allow_list_is_inspect_sphinx_and_fission', () => {
  const config = {
    agent: {
      'fast-inquiry': { model: 'fast-inquiry-model' },
      'deep-inquiry': { model: 'deep-inquiry-model' },
    },
  }
  for (const tier of ['fast', 'deep']) {
    for (const role of [
      'manager',
      'orchestrator',
      'coder',
      'inspector',
      'browser',
      'reviewer',
      'devops',
      'distiller',
      'blogger',
      'bookkeeper',
    ]) {
      const name = `${tier}-${role}`
      if (!config.agent[name]) config.agent[name] = { model: `${name}-model` }
    }
  }

  const outcome = managedAgentConfig.configure(config)
  assert.equal(outcome.ok, true, outcome.error)

  for (const name of ['fast-inquiry', 'deep-inquiry']) {
    const permission = config.agent[name].permission
    assert.equal(permission['*'], 'deny')
    assert.equal(permission.inspect, 'allow')
    assert.equal(permission['sphinx_*'], 'allow')
    assert.equal(permission.fission, 'allow')
    for (const tool of READ_TOOLS) {
      assert.notEqual(permission[tool], 'allow', `${name} must not allow ${tool}`)
    }
    assert.notEqual(permission['stealth-browser-mcp_*'], 'allow', `${name} must not allow stealth-browser MCP`)
  }
})
