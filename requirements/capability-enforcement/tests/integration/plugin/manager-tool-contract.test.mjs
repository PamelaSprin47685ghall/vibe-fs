// Capability-owned plugin contract. Every assertion reaches the real plugin
// hook, Host zod schema or Host config projection; no internal F# value crosses
// this boundary.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  canonicalText,
  markerSource,
  markerToolName,
} from '../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import { permissions } from '../../../../../dist/Foundation/RolesSurface.js'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
  withPlugin,
} from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const TOOL_NAMES = [
  'fork', 'commission', 'join', 'horizon', 'todowrite', 'fission',
  'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm',
  'bash-honeypot', 'inspect', 'establish-behavior', 'repair-behavior',
  'run', 'query-shell', 'stealth-browser-mcp', 'sphinx', 'judge',
  'chronicle', 'fetch',
]

const ROLE_NAMES = ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'blogger', 'distiller']
const ALLOWED = {
  orchestrator: ['commission', 'join', 'horizon'],
  manager: ['fork', 'join', 'horizon', 'todowrite', 'fission'],
  coder: ['fission', 'read', 'write', 'edit', 'glob', 'grep', 'inspect', 'fetch', 'mv', 'rm', 'bash-honeypot'],
  inspector: ['fission', 'read', 'glob', 'grep', 'query-shell', 'fetch'],
  devops: ['join', 'horizon', 'read', 'glob', 'grep', 'inspect', 'run', 'establish-behavior', 'repair-behavior'],
  browser: ['fission', 'read', 'glob', 'grep', 'stealth-browser-mcp'],
  inquiry: ['fission', 'inspect', 'sphinx'],
  reviewer: ['read', 'glob', 'grep', 'judge'],
  blogger: ['chronicle'],
  distiller: [],
}

const withSession = (messages, sessionID = 'ses-capability-manager') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))

const fullConfig = () => ({
  agent: Object.fromEntries(
    ROLE_NAMES.flatMap((role) => [`fast-${role}`, `deep-${role}`]).map((name) => [name, {}]),
  ),
})

test('WHAT[ENF-010] MANAGER_plugin_registers_the_declared_capability_tool_set', async () => {
  await withPlugin(async (hooks) => {
    for (const toolName of TOOL_NAMES) {
      assert.equal(typeof hooks.tool[toolName]?.execute, 'function', `${toolName} is registered`)
    }
    const forbidden = ['auto-injected', '-', 'tool', 'bash', 'shell']
    for (const toolName of forbidden) assert.equal(hooks.tool[toolName], undefined, `${toolName} must not be an export`)
  })
})

test('WHAT[ENF-010] MANAGER_host_schemas_are_present_for_every_declared_argument', async () => {
  await withPlugin(async (hooks) => {
    const expected = {
      fork: ['calling', 'name', 'charge', 'keywords', 'attach', 'expected_tool_calls'],
      commission: ['calling', 'name', 'charge', 'expected_tool_calls'],
      chronicle: ['entry', 'tip'],
      'bash-honeypot': [],
    }
    for (const toolName in expected) {
      for (const argument of expected[toolName]) {
        assert.equal(typeof hooks.tool[toolName].args[argument]?.safeParse, 'function', `${toolName}.${argument}`)
      }
    }
    assert.equal(hooks.tool.commission.args.keywords, undefined)
  })
})

test('WHAT[ENF-010] MANAGER_agent_enum_accepts_only_managed_names', async () => {
  await withPlugin(async (hooks) => {
    for (const toolName of ['fork', 'commission']) {
      for (const name of ['fast-coder', 'deep-coder', 'fast-manager', 'deep-manager']) {
        assert.equal(hooks.tool[toolName].args.name.safeParse(name).success, true, `${toolName}.${name}`)
      }
      assert.equal(hooks.tool[toolName].args.name.safeParse('coder').success, false, `${toolName} rejects legacy coder`)
    }
  })
})

test('WHAT[ENF-011] MANAGER_config_projects_owned_permissions_with_default_deny', async () => {
  await withPlugin(async (hooks) => {
    const config = fullConfig()
    hooks.config(config)
    assert.equal(config.compaction.auto, false)
    for (const role of ROLE_NAMES) {
      const permission = config.agent[`fast-${role}`].permission
      for (const toolName of TOOL_NAMES) {
        const expected = ALLOWED[role].includes(toolName) ? 'allow' : 'deny'
        const key = toolName === 'stealth-browser-mcp' ? 'stealth-browser-mcp_*' : toolName === 'sphinx' ? 'sphinx_*' : toolName
        assert.equal(permission[key], expected, `${role}.${key}`)
      }
      assert.equal(permission.external_directory, 'allow', `${role}.external_directory`)
      assert.equal(config.agent[`fast-${role}`].model, undefined)
    }
  })
})

test('WHAT[ENF-001] MANAGER_role_permission_matrix_is_owned_by_RolesSurface', () => {
  for (const role of ROLE_NAMES) {
    const labels = permissions(role)
    assert.ok(Array.isArray(labels), role)
    if (role === 'distiller') assert.deepEqual(labels, [])
    if (role === 'blogger') assert.deepEqual(labels, ['Chronicle'])
  }
})

test('WHAT[ENF-006] MANAGER_pair_marker_is_not_a_registered_tool_and_transform_keeps_canonical_text', async () => {
  assert.equal(markerToolName, '-')
  assert.equal(typeof markerSource, 'string')
  assert.equal(typeof canonicalText, 'string')
  await withPlugin(async (hooks) => {
    assert.equal(hooks.tool['-'], undefined)
    const transformed = {
      messages: withSession([
        { role: 'user', info: { id: 'user-1' }, parts: [{ type: 'text', text: 'hello' }] },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    for (const message of transformed.messages) {
      assert.notEqual(message.info?.source, markerSource, 'plain user input does not synthesize a pair tool')
    }
  })
})

test('WHAT[ENF-010] MANAGER_legacy_agent_configuration_is_rejected_after_owned_projection', async () => {
  await withPlugin(async (hooks) => {
    const config = fullConfig()
    config.agent.coder = {}
    assert.throws(() => hooks.config(config), /Legacy agent name 'coder'/)
    assert.equal(config.compaction.auto, false)
    assert.equal(config.agent['fast-manager'].permission['*'], 'deny')
  })
})

test('WHAT[ENF-009] MANAGER_non_repository_fork_keywords_are_rejected_before_child_creation', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-manager-contract', 'fast-manager')
    const result = await hooks.tool.fork.execute(
      { calling: 'navigator', name: 'Web Road', charge: 'browse', keywords: 'repository clue' },
      { sessionID: 'ses-manager-contract', agent: 'fast-manager' },
    )
    assert.match(result, /only available when fork targets Coder, Inspector, or DevOps/i)
    assert.equal(createdIds.length, 0)
  })
})
