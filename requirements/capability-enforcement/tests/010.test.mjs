import test from 'node:test'

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

test('WHAT[ENF-010] AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields', () => {
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
test('WHAT[ENF-010] AGENT_007_bash_stays_denied_even_when_the_gate_fails', () => {
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
test('WHAT[ENF-010] AGENT_007_validation_error_is_still_reported', () => {
  const config = buildConfig()
  config.agent.build = { model: 'some-model' }
  const outcome = validateManagedAgents(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /Legacy agent name 'build'/)
})
test('WHAT[ENF-010] AGENT_004_legacy_agent_name_fails_validation', () => {
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


test('WHAT[ENF-010] BASHHONEY_spec_is_parameterless_and_named_bash_honeypot', () => {
  const contract = bashHoneypotContract()
  assert.equal(contract.name, 'bash-honeypot')
  assert.match(contract.description, /[Hh]oneypot/)
  assert.deepEqual(contract.argumentNames, [])
})
test('WHAT[ENF-010] BASHHONEY_execute_returns_hard_denial_and_runs_nothing', async () => {
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


test('WHAT[ENF-010] FORK_orchestrator_missing_authority_is_refused_without_session_identity', async () => {
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


test('WHAT[ENF-010] inquiry_rolePredicate_denies_all_tools', () => {
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

test('WHAT[ENF-010] MACFG_validate_rejects_null_config_and_legacy_agent', () => {
  assert.match(errOf(validate(null)), /Host config object/)
  assert.match(errOf(validate({ agent: { build: {} } })), /build/)
})
test('WHAT[ENF-010] MACFG_validate_rejects_legacy_agent_present', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}
  assert.match(errOf(validate(cfg)), /build/)
  delete cfg.agent.build
  cfg.agent.coder = {}
  assert.match(errOf(validate(cfg)), /coder/)
})
test('WHAT[ENF-010] MACFG_configureManager_legacy_agent_is_fatal_after_owned_fields_land', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}

  assert.throws(() => configureManager(cfg), /Legacy agent name 'build'/)
  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.agent.manager.permission['*'], 'deny')
  assert.equal(cfg.agent.orchestrator.permission['*'], 'deny')
})
}
