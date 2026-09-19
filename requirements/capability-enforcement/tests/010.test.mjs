import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { permissions } = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { configure: configureManagedAgents, installDefaultResources, validate: validateManagedAgents } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");

installDefaultResources()
const ROLES = [
  'Manager',
  'Orchestrator',
  'Engineer',
  'DevOps',
  'Blogger',
]
const agentName = (role) => `${role.toLowerCase()}`
const buildConfig = () => {
  const agent = {}
  for (const role of ROLES) {
    agent[agentName(role)] = {
      model: `${role.toLowerCase()}-model`,
    }
  }
  agent.bookkeeper = { model: 'bookkeeper-model' }
  agent.predictor = { model: 'predictor-model' }
  return { agent }
}
const wildcardMatch = (input, pattern) => {
  const normalized = input.replaceAll('\\', '/')
  let escaped = pattern
    .replaceAll('\\', '/')
    .replace(/[.+^$${}()|[\]\\]/g, '\\$&')
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.')
  if (escaped.endsWith(' .*')) escaped = escaped.slice(0, -3) + '( .*)?'
  return new RegExp('^' + escaped + '$', 's').test(normalized)
}
const evaluate = (rules, permission, pattern) =>
  [...rules].reverse().find((r) => wildcardMatch(permission, r.permission) && wildcardMatch(pattern, r.pattern)) ?? {
    action: 'ask',
  }
const rulesOf = (permissionObj) => {
  const rules = []
  for (const key in permissionObj) {
    const value = permissionObj[key]
    if (typeof value === 'string') {
      rules.push({ permission: key, action: value, pattern: '*' })
      continue
    }
    for (const pattern in value) rules.push({ permission: key, pattern, action: value[pattern] })
  }
  return rules
}
const hostDefaults = () => [
  { permission: '*', pattern: '*', action: 'allow' },
  { permission: 'doom_loop', pattern: '*', action: 'ask' },
  { permission: 'external_directory', pattern: '*', action: 'ask' },
  { permission: 'question', pattern: '*', action: 'deny' },
  { permission: 'plan_enter', pattern: '*', action: 'deny' },
  { permission: 'plan_exit', pattern: '*', action: 'deny' },
  { permission: 'read', pattern: '*', action: 'allow' },
  { permission: 'read', pattern: '*.env', action: 'ask' },
  { permission: 'read', pattern: '*.env.*', action: 'ask' },
  { permission: 'read', pattern: '*.env.example', action: 'allow' },
]
const mergedRules = (config, name) => [...hostDefaults(), ...rulesOf(config.agent[name].permission)]
const allowList = (config, name) => {
  const rules = mergedRules(config, name)
  const tools = [
    'bash',
    'bash-honeypot',
    'assume',
    'read',
    'write',
    'edit',
    'glob',
    'grep',
    'mv',
    'rm',
    'run',
    'fork',
    'resume',
    'commission',
    'open-terminal',
    'send-terminal',
    'read-terminal',
    'signal-terminal',
    'join',
    'horizon',
    'todowrite',
    'fission',
    'review',
    'chronicle',
    'fetch',
    'suicide',
    'skill',
  ]
  return tools.filter((tool) => evaluate(rules, tool, '*').action === 'allow')
}
const HOST_UTILITY_ALLOW = ['skill']
const COGNITIVE_UTILITY_ALLOW = ['assume']
const hostUtilityAllowFor = (role) => (role === 'Blogger' ? [] : HOST_UTILITY_ALLOW)
const cognitiveUtilityAllowFor = (role) => (role === 'Blogger' ? [] : COGNITIVE_UTILITY_ALLOW)
const ROLE_ALLOW = {
  Manager: ['fork', 'resume', 'join', 'horizon', 'todowrite', 'suicide', 'review'],
  Orchestrator: ['commission', 'join', 'horizon'],
  Engineer: ['read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm', 'bash-honeypot', 'fetch', 'fission'],
  DevOps: [
    'read',
    'write',
    'edit',
    'glob',
    'grep',
    'mv',
    'rm',
    'run',
    'join',
    'horizon',
    'open-terminal',
    'send-terminal',
    'read-terminal',
    'signal-terminal',
  ],
  Blogger: ['chronicle'],
}

test('WHAT[capability-enforcement-010] AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields', () => {
  const config = buildConfig()
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, `gate must accept distinct models: ${outcome.error}`)

  // Every canonical managed agent got the owned mode + permission + prompt.
  for (const role of ROLES) {
    const entry = config.agent[agentName(role)]
    assert.equal(entry.mode, 'primary', `${agentName(role)} must be primary`)
    assert.ok(entry.permission && entry.permission['*'] === 'deny', `${agentName(role)} must deny by default`)
    assert.ok(typeof entry.prompt === 'string' && entry.prompt.length > 0, `${agentName(role)} must carry a prompt`)
  }
})
test('WHAT[capability-enforcement-010] AGENT_007_bash_stays_denied_even_when_the_gate_fails', () => {
  const config = buildConfig()
  config.agent.build = { model: 'some-model' }
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, false, 'legacy agent name must fail the gate')
  assert.match(outcome.error, /Legacy agent name 'build'/)

  for (const role of ROLES) {
    const name = agentName(role)
    const entry = config.agent[name]
    assert.equal(entry.mode, 'primary', `${name} mode must survive a gate error`)
    assert.ok(entry.permission && entry.permission['*'] === 'deny', `${name} must keep "*": deny after a gate error`)
    assert.deepEqual(
      allowList(config, name).sort(),
      [...ROLE_ALLOW[role], ...hostUtilityAllowFor(role), ...cognitiveUtilityAllowFor(role)].sort(),
      `${name} tool set must survive a gate error`,
    )
    assert.ok(!allowList(config, name).includes('bash'), `${name} must never allow bash`)
  }
})
test('WHAT[capability-enforcement-010] AGENT_007_validation_error_is_still_reported', () => {
  const config = buildConfig()
  config.agent.build = { model: 'some-model' }
  const outcome = validateManagedAgents(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /Legacy agent name 'build'/)
})
test('WHAT[capability-enforcement-010] AGENT_004_legacy_agent_name_fails_validation', () => {
  const config = buildConfig()
  config.agent.build = { model: 'some-model' }
  const outcome = validateManagedAgents(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /Legacy agent name 'build'/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { bashHoneypotContract } = await import("../../../dist/OpenCode/Tools/ToolSurface.js");
const { acceptAuthorityRoot, withExecutablePlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");


test('WHAT[capability-enforcement-010] BASHHONEY_spec_is_parameterless_and_named_bash_honeypot', () => {
  const contract = bashHoneypotContract()
  assert.equal(contract.name, 'bash-honeypot')
  assert.match(contract.description, /[Hh]oneypot/)
  assert.deepEqual(contract.argumentNames, [])
})
test('WHAT[capability-enforcement-010] BASHHONEY_execute_returns_hard_denial_and_runs_nothing', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-honey', 'engineer')
    const result = await hooks.tool['bash-honeypot'].execute({}, { sessionID: 'ses-honey', agent: 'engineer' })
    assert.doesNotMatch(result, /\berror\s*=/)
    assert.match(result, /DENIED/)
    assert.match(result, /unauthorized privilege-escalation|未经授权的提权/i)
    assert.match(result, /not permitted to execute bash|不允许执行 bash|不得执行 bash/i)
    assert.match(result, /No command ran|没有运行任何命令|未运行任何命令/i)
    assert.match(result, /DevOps/)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin, withPlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");


test('WHAT[capability-enforcement-010] FORK_orchestrator_missing_authority_is_refused_without_session_identity', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool.commission.execute(
      { calling: 'lead', name: 'North Road', charge: 'x' },
      { sessionID: '', agent: 'orchestrator' },
    )
    assert.match(result, /caller's authority is established|调用方权威确立之前/i)
    assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { rolePredicate } = await import("../../../dist/OpenCode/Tools/ToolRegistrySurface.js");
const { permissions: rolePermissions, isAllowed: surfaceIsAllowed } = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { allRoleLabels } = await import("../../../dist/Foundation/RolesSurface.js");


test('WHAT[capability-enforcement-010] inquiry_rolePredicate_denies_all_tools', () => {
  assert.equal(rolePredicate('inspect', 'inquiry'), false)
  assert.equal(rolePredicate('fission', 'inquiry'), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const { installDefaultResources, validate, configure, configureManager } = await import(
  '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
)
installDefaultResources()
const okOf = (result) => result
const errOf = (result) => result.error
const NAMES = [
  'manager',
  'orchestrator',
  'engineer',
  'devops',
  'blogger',
  'bookkeeper',
  'predictor',
]
function fullConfig() {
  return { agent: Object.fromEntries(NAMES.map((name) => [name, {}])) }
}

test('WHAT[capability-enforcement-010] MACFG_validate_rejects_null_config_and_legacy_agent', () => {
  assert.match(errOf(validate(null)), /Host config object/)
  assert.match(errOf(validate({ agent: { build: {} } })), /build/)
})
test('WHAT[capability-enforcement-010] MACFG_validate_rejects_legacy_agent_present', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}
  assert.match(errOf(validate(cfg)), /build/)
  delete cfg.agent.build
  cfg.agent.coder = {}
  assert.match(errOf(validate(cfg)), /coder/)
})
test('WHAT[capability-enforcement-010] MACFG_configureManager_legacy_agent_is_fatal_after_owned_fields_land', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}

  assert.throws(() => configureManager(cfg), /Legacy agent name 'build'/)
  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.agent.manager.permission['*'], 'deny')
  assert.equal(cfg.agent.orchestrator.permission['*'], 'deny')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { withExecutablePlugin, acceptAuthorityRoot } = await import("../../verification-system/tests/support/plugin-fixture.mjs");

integrationTest('WHAT[capability-enforcement-010] AGENT_023_engineer_receives_hard_denial_and_no_shell', async () => {
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
integrationTest('WHAT[capability-enforcement-010] AGENT_023_bash_honeypot_is_denied_for_non_engineer_roles', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'manager-bash-honey', 'manager')
    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'manager-bash-honey', agent: 'manager' },
    )
    assert.match(result, /not available to Manager|对 Manager 不可用/)
  })
})
integrationTest('WHAT[capability-enforcement-010] AGENT_023_bash_honeypot_is_denied_when_the_role_is_unresolved', async () => {
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
const { markerSource, markerToolName } = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");
const { permissions } = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin, withPlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");

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

integrationTest('WHAT[capability-enforcement-010] MANAGER_plugin_registers_only_plugin_owned_capability_tools', async () => {
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
integrationTest('WHAT[capability-enforcement-010] MANAGER_host_schemas_are_present_for_every_declared_argument', async () => {
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
integrationTest('WHAT[capability-enforcement-010] ASSUME_updates_then_queries_one_persistent_jq_canvas_in_one_call', async () => {
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
integrationTest('WHAT[capability-enforcement-010] MANAGER_calling_enum_uses_personas_while_name_remains_a_free_byname', async () => {
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
integrationTest('WHAT[capability-enforcement-010] MANAGER_legacy_agent_configuration_is_rejected_after_owned_projection', async () => {
  await withPlugin(async (hooks) => {
    const config = fullConfig()
    config.agent.build = {}
    assert.throws(() => hooks.config(config), /Legacy agent name 'build'/)
    assert.equal(config.compaction.auto, false)
    assert.equal(config.agent['manager'].permission['*'], 'deny')
  })
})
}
