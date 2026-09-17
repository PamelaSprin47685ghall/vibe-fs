import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { withExecutablePlugin, acceptAuthorityRoot } = await import("../../../../verification-system/tests/support/plugin-fixture.mjs");


test('WHAT[ENF-010] AGENT_023_engineer_receives_hard_denial_and_no_shell', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'engineer-bash-honey', 'engineer')
    assert.ok(hooks.tool['bash-honeypot'], 'bash-honeypot must be registered')

    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'engineer-bash-honey', agent: 'engineer' },
    )

    assert.match(result, /DENIED/)
    assert.match(result, /unauthorized privilege-escalation|提权/)
    assert.match(result, /No command ran|未运行任何命令|没有运行任何命令/)
  })
})
test('WHAT[ENF-010] AGENT_023_bash_honeypot_is_denied_for_non_engineer_roles', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'manager-bash-honey', 'manager')
    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'manager-bash-honey', agent: 'manager' },
    )
    assert.match(result, /not available to Manager|对 Manager 不可用/)
  })
})
test('WHAT[ENF-010] AGENT_023_bash_honeypot_is_denied_when_the_role_is_unresolved', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'unresolved-bash-honey', agent: 'engineer' },
    )
    assert.match(result, /authority is established|权威确立/)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { markerSource, markerToolName } = await import("../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");
const { permissions } = await import("../../../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin, withPlugin } = await import("../../../../verification-system/tests/support/plugin-fixture.mjs");

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
test('WHAT[ENF-010] MANAGER_legacy_agent_configuration_is_rejected_after_owned_projection', async () => {
  await withPlugin(async (hooks) => {
    const config = fullConfig()
    config.agent.build = {}
    assert.throws(() => hooks.config(config), /Legacy agent name 'build'/)
    assert.equal(config.compaction.auto, false)
    assert.equal(config.agent['manager'].permission['*'], 'deny')
  })
})
}
