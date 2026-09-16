// ENF-010: Dual-layer fail-closed execution and configuration error policies
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  configure as configureManagedAgents,
  installDefaultResources,
  validate as validateManagedAgents,
} from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { bashHoneypotContract } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

const { validate, configureManager } = await import(
  '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
)
installDefaultResources()

const errOf = (result) => result.error

const ROLES = [
  'Manager',
  'Orchestrator',
  'Coder',
  'Inspector',
  'Browser',
  'Inquiry',
  'DevOps',
  'Distiller',
  'Blogger',
]
const agentName = (role) => `${role.toLowerCase()}`

const NAMES = [
  'manager',
  'orchestrator',
  'coder',
  'inspector',
  'devops',
  'browser',
  'inquiry',
  'blogger',
  'distiller',
  'bookkeeper',
  'predictor',
]

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

function fullConfig() {
  return { agent: Object.fromEntries(NAMES.map((name) => [name, {}])) }
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
    'inspect',
    'run',
    'query-shell',
    'establish-behavior',
    'repair-behavior',
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
    'stealth-browser-mcp_*',
    'sphinx_*',
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
const cognitiveUtilityAllowFor = (role) => role === 'Blogger' || role === 'Distiller' ? [] : COGNITIVE_UTILITY_ALLOW

const ROLE_ALLOW = {
  Manager: ['fork', 'resume', 'join', 'horizon', 'todowrite', 'fission', 'suicide', 'review'],
  Orchestrator: ['commission', 'join', 'horizon'],
  Coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspect', 'mv', 'rm', 'bash-honeypot', 'fetch', 'fission'],
  Inspector: ['read', 'glob', 'grep', 'query-shell', 'fetch', 'fission'],
  Browser: ['read', 'glob', 'grep', 'stealth-browser-mcp_*', 'fission'],
  Inquiry: ['inspect', 'sphinx_*', 'fission'],
  DevOps: [
    'read',
    'glob',
    'grep',
    'inspect',
    'run',
    'establish-behavior',
    'repair-behavior',
    'join',
    'horizon',
    'open-terminal',
    'send-terminal',
    'read-terminal',
    'signal-terminal',
  ],
  Distiller: [],
  Blogger: ['chronicle'],
}

test('WHAT[ENF-010] AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields', () => {
  const config = buildConfig()
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, `gate must accept distinct models: ${outcome.error}`)

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
      [...ROLE_ALLOW[role], ...HOST_UTILITY_ALLOW, ...cognitiveUtilityAllowFor(role)].sort(),
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

test('WHAT[ENF-010] BASHHONEY_spec_is_parameterless_and_named_bash_honeypot', () => {
  const contract = bashHoneypotContract()
  assert.equal(contract.name, 'bash-honeypot')
  assert.match(contract.description, /[Hh]oneypot/)
  assert.deepEqual(contract.argumentNames, [])
})

test('WHAT[ENF-010] Inquiry_rolePredicate_inspector_allow_and_host_native_read_gap', () => {
  const READ_TOOLS = ['read', 'glob', 'grep']
  for (const tool of READ_TOOLS) {
    assert.equal(rolePredicate(tool, 'inquiry'), false, `default deny for Host-native ${tool}`)
    assert.equal(rolePredicate(tool, 'inspector'), false, 'not role-gated: even Inspector is denied here')
  }

  assert.equal(rolePredicate('inspect', 'inquiry'), true, 'rolePredicate(inspect) must allow Inquiry')
})

test('WHAT[ENF-010] MACFG_validate_rejects_null_config_and_legacy_agent', () => {
  assert.match(errOf(validate(null)), /Host config object/)
  assert.match(errOf(validate({ agent: { build: {} } })), /build/)
})

test('WHAT[ENF-010] MACFG_validate_rejects_legacy_agent_present', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}
  assert.match(errOf(validate(cfg)), /build/)
})

test('WHAT[ENF-010] MACFG_configureManager_legacy_agent_is_fatal_after_owned_fields_land', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}

  assert.throws(() => configureManager(cfg), /Legacy agent name 'build'/)
  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.agent.manager.permission['*'], 'deny')
  assert.equal(cfg.agent.orchestrator.permission['*'], 'deny')
})
