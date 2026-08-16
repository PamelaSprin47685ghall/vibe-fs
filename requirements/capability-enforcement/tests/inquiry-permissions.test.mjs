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

import { capabilityToolNames, rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { permissions as rolePermissions, isAllowed as surfaceIsAllowed } from '../../../dist/Foundation/RolesSurface.js'

installDefaultResources()

const names = (permissions) => permissions

const READ_TOOLS = ['read', 'glob', 'grep']
const READ_PERMISSIONS = ['Read', 'Glob', 'Grep']

test('WHAT[ENF-006] Inquiry_permissions_are_inspect_sphinx_and_fission', () => {
  const allowed = rolePermissions('inquiry')
  assert.deepEqual(allowed, ['Fission', 'Inspect', 'Sphinx'])
  assert.equal(allowed.includes('Read'), false)
  assert.equal(allowed.includes('Glob'), false)
  assert.equal(allowed.includes('Grep'), false)
})

test('WHAT[ENF-006] Inquiry_isAllowed_denies_read_glob_grep_and_allows_inspect_sphinx_fission', () => {
  assert.equal(surfaceIsAllowed('inquiry', 'Inspect'), true)
  assert.equal(surfaceIsAllowed('inquiry', 'Sphinx'), true)
  assert.equal(surfaceIsAllowed('inquiry', 'Fission'), true)
  for (const permission of READ_PERMISSIONS) {
    assert.equal(surfaceIsAllowed('inquiry', permission), false, `Inquiry must lack ${permission}`)
  }
})

test('WHAT[ENF-001] Inquiry_toolCapabilitiesFor_WorkMain_matches_Roles_permissions', () => {
  const caps = capabilityToolNames('inquiry', 'work-main')
  assert.deepEqual(caps, ['fission', 'inspect', 'sphinx_*'])
  for (const tool of READ_TOOLS) {
    assert.equal(caps.includes(tool), false, `toolCapabilitiesFor must omit ${tool}`)
  }
})

test('WHAT[ENF-010] Inquiry_rolePredicate_inspector_allow_and_host_native_read_gap', () => {
  // Gap note: read/glob/grep are Host-native builtins — they are NOT ToolRegistry
  // specs, so rolePredicate has no isAllowed cases for them and falls through to
  // default deny. The real Inquiry gate for those tools is Roles.permissions /
  // Host permission schema (asserted above), not a ToolRegistry rolePredicate.
  for (const tool of READ_TOOLS) {
    assert.equal(rolePredicate(tool, 'inquiry'), false, `default deny for Host-native ${tool}`)
    assert.equal(rolePredicate(tool, 'inspector'), false, `not role-gated: even Inspector is denied here`)
  }

  assert.equal(rolePredicate('inspect', 'inquiry'), true, 'rolePredicate(inspect) must allow Inquiry')
})

test('WHAT[ENF-002] Inquiry_host_schema_allow_list_is_inspect_sphinx_and_fission', () => {
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

  const outcome = configureManagedAgents(config)
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
