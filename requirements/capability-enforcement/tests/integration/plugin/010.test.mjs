import assert from 'node:assert/strict'
import test from 'node:test'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
  withPlugin,
} from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const PLUGIN_TOOL_NAMES = [
  'fork', 'resume', 'commission', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'join', 'horizon', 'fission', 'review', 'suicide', 'run', 'query-shell', 'inspect',
  'establish-behavior', 'repair-behavior', 'mv', 'rm', 'bash-honeypot', 'assume', 'chronicle',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'js-browser', 'js-coder', 'js-devops', 'js-inspector',
]

const HOST_OWNED_TOOL_NAMES = [
  'todowrite', 'read', 'write', 'edit', 'glob', 'grep', 'skill', 'stealth-browser-mcp', 'sphinx',
]

const ROLE_NAMES = ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'blogger', 'distiller']

test('WHAT[ENF-010] BASHHONEY_execute_returns_hard_denial_and_runs_nothing', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-honey', 'coder')
    const result = await hooks.tool['bash-honeypot'].execute({}, { sessionID: 'ses-honey', agent: 'coder' })
    assert.doesNotMatch(result, /\berror\s*=/)
    assert.match(result, /DENIED/)
    assert.match(result, /unauthorized privilege-escalation|未经授权的提权/i)
    assert.match(result, /not permitted to execute bash|不允许执行 bash|不得执行 bash/i)
    assert.match(result, /No command ran|没有运行任何命令|未运行任何命令/i)
    assert.match(result, /DevOps/)
  })
})

test('WHAT[ENF-010] FORK_orchestrator_missing_authority_is_refused_without_session_identity', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool.commission.execute(
      { calling: 'coordinator', name: 'North Road', charge: 'x' },
      { sessionID: '', agent: 'orchestrator' },
    )
    assert.match(result, /caller's authority is established|调用方权威确立之前/i)
    assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  })
})

test('WHAT[ENF-010] AGENT_023_coder_receives_hard_denial_and_no_shell', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'coder-bash-honey', 'coder')
    assert.ok(hooks.tool['bash-honeypot'], 'bash-honeypot must be registered')

    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'coder-bash-honey', agent: 'coder' },
    )

    assert.match(result, /DENIED/)
    assert.match(result, /unauthorized privilege-escalation|提权/)
    assert.match(result, /No command ran|没有运行任何命令/)
  })
})

test('WHAT[ENF-010] AGENT_023_bash_honeypot_is_denied_for_non_coder_roles', async () => {
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
      { sessionID: 'unresolved-bash-honey', agent: 'coder' },
    )
    assert.match(result, /authority is established|权威确立/)
  })
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
      assert.equal(hooks.tool.fork.args.name.safeParse(managedName).success, true, 'fork.name is free-form byname')
      assert.equal(hooks.tool.commission.args.name.safeParse(managedName).success, true, 'commission.name is free-form byname')
    }
  })
})

test('WHAT[ENF-010] MANAGER_legacy_agent_configuration_is_rejected_after_owned_projection', async () => {
  const fullConfigObj = () => ({
    agent: Object.fromEntries(
      ROLE_NAMES.map((role) => [role, {}]),
    ),
  })
  await withPlugin(async (hooks) => {
    const config = fullConfigObj()
    config.agent.build = {}
    assert.throws(() => hooks.config(config), /Legacy agent name 'build'/)
    assert.equal(config.compaction.auto, false)
    assert.equal(config.agent['manager'].permission['*'], 'deny')
  })
})
