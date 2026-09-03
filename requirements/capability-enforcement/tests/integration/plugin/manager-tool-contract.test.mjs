// Capability-owned plugin contract. Every assertion reaches the real plugin
// hook, Host zod schema or Host config projection; no internal F# value crosses
// this boundary.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  markerSource,
  markerToolName,
} from '../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import { permissions } from '../../../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
  withPlugin,
} from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const TOOL_NAMES = [
  'fork', 'commission', 'join', 'horizon', 'todowrite', 'fission',
  'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm',
  'bash-honeypot', 'assume', 'inspect', 'establish-behavior', 'repair-behavior',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'run', 'query-shell', 'stealth-browser-mcp', 'sphinx', 'judge',
  'chronicle', 'fetch',
]

const PLUGIN_TOOL_NAMES = [
  'fork', 'commission', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'join', 'horizon', 'fission', 'judge', 'suicide', 'run', 'query-shell', 'inspect',
  'establish-behavior', 'repair-behavior', 'mv', 'rm', 'bash-honeypot', 'assume', 'chronicle',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'js-browser', 'js-coder', 'js-devops', 'js-inspector', 'js-reviewer',
]

const HOST_OWNED_TOOL_NAMES = [
  'todowrite', 'read', 'write', 'edit', 'glob', 'grep', 'skill', 'stealth-browser-mcp', 'sphinx',
]

const ROLE_NAMES = ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'blogger', 'distiller']
const COGNITIVE_TOOLS = ['enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']
const ALLOWED = {
  orchestrator: ['commission', 'join', 'horizon', 'assume', ...COGNITIVE_TOOLS],
  manager: ['fork', 'join', 'horizon', 'todowrite', 'fission', 'assume', ...COGNITIVE_TOOLS],
  coder: ['fission', 'read', 'write', 'edit', 'glob', 'grep', 'inspect', 'fetch', 'mv', 'rm', 'bash-honeypot', 'assume', ...COGNITIVE_TOOLS],
  inspector: ['fission', 'read', 'glob', 'grep', 'query-shell', 'fetch', 'assume', ...COGNITIVE_TOOLS],
  devops: ['join', 'horizon', 'read', 'glob', 'grep', 'inspect', 'run', 'establish-behavior', 'repair-behavior', 'assume', ...COGNITIVE_TOOLS],
  browser: ['fission', 'read', 'glob', 'grep', 'stealth-browser-mcp', 'assume', ...COGNITIVE_TOOLS],
  inquiry: ['fission', 'inspect', 'sphinx', 'assume', ...COGNITIVE_TOOLS],
  reviewer: ['read', 'glob', 'grep', 'judge', 'assume', ...COGNITIVE_TOOLS],
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
    ROLE_NAMES.map((role) => [role, {}]),
  ),
})

test('WHAT[ENF-010] MANAGER_plugin_registers_only_plugin_owned_capability_tools', async () => {
  await withPlugin(async (hooks) => {
    assert.deepEqual(Object.keys(hooks.tool).sort(), [...PLUGIN_TOOL_NAMES].sort())
    for (const toolName of PLUGIN_TOOL_NAMES) {
      assert.equal(typeof hooks.tool[toolName]?.execute, 'function', `${toolName} is registered`)
    }
    for (const toolName of HOST_OWNED_TOOL_NAMES) {
      assert.equal(hooks.tool[toolName], undefined, `${toolName} stays Host-owned`)
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
      assume: ['update', 'query'],
      enough: ['decision'],
      abandon: ['commitment'],
      defer: ['new_work'],
      subscribe: ['id', 'concern'],
      publish: ['id', 'message'],
      celebrate: ['experience'],
      regret: ['experience'],
    }
    for (const toolName in expected) {
      for (const argument of expected[toolName]) {
        assert.equal(typeof hooks.tool[toolName].args[argument]?.safeParse, 'function', `${toolName}.${argument}`)
      }
    }
    assert.equal(hooks.tool.commission.args.keywords, undefined)
  })
})

test('WHAT[ENF-010] ASSUME_updates_then_queries_one_persistent_jq_canvas_in_one_call', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'manager-assume', 'manager')

    const first = await hooks.tool.assume.execute(
      {
        update: '{ideas:["compressed memory","random access"]}',
        query: '.ideas | map(select(test("memory")))',
      },
      { sessionID: 'manager-assume', agent: 'manager' },
    )
    const second = await hooks.tool.assume.execute(
      { update: '.', query: '.ideas[1]' },
      { sessionID: 'manager-assume', agent: 'manager' },
    )
    const scalar = await hooks.tool.assume.execute(
      { update: '"hello"', query: '.' },
      { sessionID: 'manager-assume', agent: 'manager' },
    )
    const scalarRead = await hooks.tool.assume.execute(
      { update: '.', query: '.' },
      { sessionID: 'manager-assume', agent: 'manager' },
    )
    await assert.rejects(
      hooks.tool.assume.execute(
        { update: '{committed:true}', query: 'error("query failed")' },
        { sessionID: 'manager-assume', agent: 'manager' },
      ),
      /assume query failed after update committed/,
    )
    const afterQueryFailure = await hooks.tool.assume.execute(
      { update: '.', query: '.committed' },
      { sessionID: 'manager-assume', agent: 'manager' },
    )
    await assert.rejects(
      hooks.tool.assume.execute(
        { update: 'empty', query: '.' },
        { sessionID: 'manager-assume', agent: 'manager' },
      ),
      /assume update must produce exactly one JSON value/,
    )
    const afterRejectedUpdate = await hooks.tool.assume.execute(
      { update: '.', query: '.committed' },
      { sessionID: 'manager-assume', agent: 'manager' },
    )

    assert.match(first, /compressed memory/)
    assert.doesNotMatch(first, /random access/)
    assert.match(second, /random access/)
    assert.doesNotMatch(second, /compressed memory/)
    assert.equal(scalar, '"hello"')
    assert.equal(scalarRead, '"hello"')
    assert.equal(afterQueryFailure, 'true')
    assert.equal(afterRejectedUpdate, 'true')
  })
})

test('WHAT[ENF-010] MANAGER_calling_enum_uses_personas_while_name_remains_a_free_byname', async () => {
  await withPlugin(async (hooks) => {
    const managerPersonas = [
      'coder', 'investigator', 'operator', 'researcher', 'analyst',
    ]
    for (const calling of managerPersonas) {
      assert.equal(hooks.tool.fork.args.calling.safeParse(calling).success, true, `fork.calling=${calling}`)
    }
    for (const calling of ['navigator', 'engineer', 'coordinator', 'lead', 'director']) {
      assert.equal(hooks.tool.fork.args.calling.safeParse(calling).success, false, `fork rejects ${calling}`)
    }
    for (const calling of ['lead']) {
      assert.equal(hooks.tool.commission.args.calling.safeParse(calling).success, true, `commission.calling=${calling}`)
    }
    for (const calling of ['coordinator', 'director', 'coder', 'navigator']) {
      assert.equal(hooks.tool.commission.args.calling.safeParse(calling).success, false, `commission rejects ${calling}`)
    }
    for (const managedName of ['fast-coder', 'deep-coder', 'fast-manager', 'deep-manager']) {
      assert.equal(hooks.tool.fork.args.calling.safeParse(managedName).success, false, `fork rejects ${managedName}`)
      assert.equal(hooks.tool.commission.args.calling.safeParse(managedName).success, false, `commission rejects ${managedName}`)
      assert.equal(hooks.tool.fork.args.name.safeParse(managedName).success, true, `fork.name is free-form byname`)
      assert.equal(hooks.tool.commission.args.name.safeParse(managedName).success, true, `commission.name is free-form byname`)
    }
  })
})

test('WHAT[ENF-011] MANAGER_config_projects_owned_permissions_with_default_deny', async () => {
  await withPlugin(async (hooks) => {
    const config = fullConfig()
    hooks.config(config)
    assert.equal(config.compaction.auto, false)
    for (const role of ROLE_NAMES) {
      const permission = config.agent[role].permission
      for (const toolName of TOOL_NAMES) {
        const expected = ALLOWED[role].includes(toolName) ? 'allow' : 'deny'
        const key = toolName === 'stealth-browser-mcp' ? 'stealth-browser-mcp_*' : toolName === 'sphinx' ? 'sphinx_*' : toolName
        assert.equal(permission[key], expected, `${role}.${key}`)
      }
      assert.equal(permission.external_directory, 'allow', `${role}.external_directory`)
      assert.equal(config.agent[role].model, undefined)
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

test('WHAT[ENF-006] MANAGER_pair_guidance_rides_cursor_suffix_without_synthetic_skill_row', async () => {
  assert.equal(markerToolName, 'skill')
  assert.equal(typeof markerSource, 'string')
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-capability-manager', 'manager')
    assert.equal(hooks.tool.skill, undefined, 'skill remains Host-owned')
    assert.equal(hooks.tool['auto-injected'], undefined, 'legacy auto-injected must not be plugin-registered')
    const transformed = {
      messages: withSession([
        { role: 'user', info: { id: 'root-ses-capability-manager' }, parts: [{ type: 'text', text: 'start' }] },
        { role: 'assistant', info: { id: 'c1' }, parts: [{ type: 'tool', tool: 'read', callID: 't1', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
        { role: 'assistant', info: { id: 'r1' }, parts: [{ type: 'tool', tool: 'read', callID: 't1', state: { status: 'completed', input: {}, output: 'ok1', time: { start: 0, end: 0 } } }] },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    const synthetic = transformed.messages.find((message) => message.info?.source === markerSource)
    assert.equal(synthetic, undefined, 'zero-synthetic mode must not inject a synthetic skill row')
    const terminal = transformed.messages.find((message) => message.info?.id === 'r1')
    assert.ok(terminal, 'terminal real tool result survives the transform')
    const output = terminal.parts?.[0]?.state?.output ?? ''
    assert.ok(output.startsWith('ok1\0\uFEFF'), 'guidance travels as NUL+BOM suffix on the terminal real tool result')
    assert.match(output, /#/, 'suffix carries guidance bytes')
    assert.equal(hooks.tool[markerToolName], undefined, 'cursor suffix borrows no plugin-registered skill name')
  })
})

test('WHAT[ENF-010] MANAGER_legacy_agent_configuration_is_rejected_after_owned_projection', async () => {
  await withPlugin(async (hooks) => {
    const config = fullConfig()
    config.agent.build = {}
    assert.throws(() => hooks.config(config), /Legacy agent name 'build'/)
    assert.equal(config.compaction.auto, false)
    assert.equal(config.agent['manager'].permission['*'], 'deny')
  })
})

test('WHAT[ENF-009] MANAGER_non_repository_fork_keywords_are_rejected_before_child_creation', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-manager-contract', 'manager')
    const result = await hooks.tool.fork.execute(
      { calling: 'researcher', name: 'Web Road', charge: 'browse', keywords: 'repository clue' },
      { sessionID: 'ses-manager-contract', agent: 'manager' },
    )
    assert.match(result, /fork targets Coder, Inspector, or DevOps|fork 目标为 Coder、Inspector 或 DevOps/i)
    assert.equal(createdIds.length, 0)
  })
})
