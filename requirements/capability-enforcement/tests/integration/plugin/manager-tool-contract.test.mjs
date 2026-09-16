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
  grantWorkOwned,
  withExecutablePlugin,
  withPlugin,
} from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const TOOL_NAMES = [
  'fork', 'resume', 'commission', 'join', 'horizon', 'todowrite', 'fission',
  'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm',
  'bash-honeypot', 'assume',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'run', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'review', 'chronicle', 'fetch', 'suicide',
]

const PLUGIN_TOOL_NAMES = [
  'fork', 'resume', 'commission', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'join', 'horizon', 'fission', 'review', 'suicide', 'run',
  'mv', 'rm', 'bash-honeypot', 'assume', 'chronicle',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'js-engineer', 'js-devops',
]

const HOST_OWNED_TOOL_NAMES = [
  'todowrite', 'read', 'write', 'edit', 'glob', 'grep', 'skill',
]

const ROLE_NAMES = ['orchestrator', 'manager', 'engineer', 'devops', 'blogger']
const COGNITIVE_TOOLS = ['enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']
const ALLOWED = {
  orchestrator: ['commission', 'join', 'horizon', 'assume', ...COGNITIVE_TOOLS],
  manager: ['fork', 'resume', 'join', 'horizon', 'todowrite', 'review', 'suicide', 'assume', ...COGNITIVE_TOOLS],
  engineer: ['fission', 'read', 'write', 'edit', 'glob', 'grep', 'fetch', 'mv', 'rm', 'bash-honeypot', 'assume', ...COGNITIVE_TOOLS],
  devops: [
    'join', 'horizon', 'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm', 'run',
    'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
    'assume', ...COGNITIVE_TOOLS,
  ],
  blogger: ['chronicle'],
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
    const forbidden = ['auto-injected', '-', 'tool', 'bash', 'shell', 'inspect', 'establish-behavior', 'repair-behavior', 'query-shell', 'js-browser', 'js-coder', 'js-inspector']
    for (const toolName of forbidden) assert.equal(hooks.tool[toolName], undefined, `${toolName} must not be an export`)
  })
})

test('WHAT[ENF-010] MANAGER_host_schemas_are_present_for_every_declared_argument', async () => {
  await withPlugin(async (hooks) => {
    const expected = {
      fork: ['calling', 'name', 'charge', 'keywords', 'attach', 'expected_tool_calls'],
      resume: ['name', 'charge', 'keywords', 'attach', 'expected_tool_calls'],
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
    const managerPersonas = ['engineer']
    for (const calling of managerPersonas) {
      assert.equal(hooks.tool.fork.args.calling.safeParse(calling).success, true, `fork.calling=${calling}`)
    }
    for (const calling of ['coder', 'investigator', 'operator', 'devops', 'researcher', 'analyst', 'coordinator', 'lead', 'director']) {
      assert.equal(hooks.tool.fork.args.calling.safeParse(calling).success, false, `fork rejects ${calling}`)
    }
    for (const calling of ['lead']) {
      assert.equal(hooks.tool.commission.args.calling.safeParse(calling).success, true, `commission.calling=${calling}`)
    }
    for (const calling of ['coordinator', 'director', 'coder', 'engineer', 'navigator']) {
      assert.equal(hooks.tool.commission.args.calling.safeParse(calling).success, false, `commission rejects ${calling}`)
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
        const key = toolName
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

test('WHAT[ENF-022] MANAGER_fission_is_denied_for_manager_and_devops', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-mgr-fission', 'manager')
    const resMgr = await hooks.tool.fission.execute(
      { prompts: ['lane 1', 'lane 2'] },
      { sessionID: 'ses-mgr-fission', agent: 'manager' },
    )
    assert.match(resMgr, /only available to Engineer|仅 Engineer 允许|denied/i)

    await acceptAuthorityRoot(runtime, 'ses-devops-fission', 'devops')
    const resDevOps = await hooks.tool.fission.execute(
      { prompts: ['lane 1', 'lane 2'] },
      { sessionID: 'ses-devops-fission', agent: 'devops' },
    )
    assert.match(resDevOps, /only available to Engineer|仅 Engineer 允许|denied/i)
  })
})
