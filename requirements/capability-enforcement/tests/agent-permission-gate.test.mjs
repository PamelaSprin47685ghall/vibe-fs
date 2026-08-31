// requirements/capability-enforcement/tests/agent-permission-gate.test.mjs —
// AGENT-002 / AGENT-006 / AGENT-007, moved from tests/unit/plugin/.
//
// Regression: the plugin's `config` hook (ManagedAgentConfig.configureFromHostConfig)
// writes Wanxiangshu-owned mode/permission/prompt onto the Host's live config
// object. A validation failure elsewhere in the config (e.g. a duplicated
// fast/deep model pair) used to short-circuit BEFORE any write, so every managed
// agent fell back to Host defaults — whose `"*": "allow"` baseline opened bash
// for Coder/Inspector/Manager alike.
//
// These tests drive the real dist entry and assert the writes the Host's
// Agent.state consumes: the `permission` object with a concrete `"*": "deny"`
// and role tool allow/deny, plus `mode` and `prompt`.

import assert from 'node:assert/strict'
import test from 'node:test'
import { permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import {
  configure as configureManagedAgents,
  installDefaultResources,
  validate as validateManagedAgents,
} from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

installDefaultResources()

const ROLES = [
  'Manager',
  'Orchestrator',
  'Coder',
  'Inspector',
  'Browser',
  'Inquiry',
  'Reviewer',
  'DevOps',
  'Distiller',
  'Blogger',
]
const TIERS = ['fast', 'deep']

const agentName = (tier, role) => `${tier}-${role.toLowerCase()}`

const buildConfig = ({ duplicateBrowserModel = false } = {}) => {
  const agent = {}
  for (const tier of TIERS) {
    for (const role of ROLES) {
      agent[agentName(tier, role)] = {
        model:
          duplicateBrowserModel && role === 'Browser'
            ? 'shared-browser-model'
            : `${tier}-${role.toLowerCase()}-model`,
      }
    }
    agent[`${tier}-bookkeeper`] = { model: `${tier}-bookkeeper-model` }
  }
  return { agent }
}

/** Host permission/index.ts `evaluate`: findLast over the merged flat ruleset. */
const wildcardMatch = (input, pattern) => {
  const normalized = input.replaceAll('\\', '/')
  let escaped = pattern
    .replaceAll('\\', '/')
    .replace(/[.+^${}()|[\]\\]/g, '\\$&')
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

/** Host agent.ts defaults (1.18.x) — the baseline the plugin writes must override. */
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
    'judge',
    'chronicle',
    'fetch',
    'suicide',
    'skill',
  ]
  return tools.filter((tool) => evaluate(rules, tool, '*').action === 'allow')
}

// AGENT-006 matrix (tool names as they reach the Host permission schema).
const HOST_UTILITY_ALLOW = ['skill']
const COGNITIVE_UTILITY_ALLOW = ['assume']
const cognitiveUtilityAllowFor = (role) => role === 'Blogger' || role === 'Distiller' ? [] : COGNITIVE_UTILITY_ALLOW

const ROLE_ALLOW = {
  Manager: ['fork', 'join', 'horizon', 'todowrite', 'fission', 'suicide'],
  Orchestrator: ['commission', 'join', 'horizon'],
  Coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspect', 'mv', 'rm', 'bash-honeypot', 'fetch', 'fission'],
  Inspector: ['read', 'glob', 'grep', 'query-shell', 'fetch', 'fission'],
  Browser: ['read', 'glob', 'grep', 'stealth-browser-mcp_*', 'fission'],
  Inquiry: ['inspect', 'sphinx_*', 'fission'],
  Reviewer: ['read', 'glob', 'grep', 'judge'],
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

  // Every managed agent got the owned mode + permission + prompt.
  for (const tier of TIERS) {
    for (const role of ROLES) {
      const entry = config.agent[agentName(tier, role)]
      assert.equal(entry.mode, 'primary', `${agentName(tier, role)} must be primary`)
      assert.ok(entry.permission && entry.permission['*'] === 'deny', `${agentName(tier, role)} must deny by default`)
      assert.ok(typeof entry.prompt === 'string' && entry.prompt.length > 0, `${agentName(tier, role)} must carry a prompt`)
    }
  }
})

test('WHAT[ENF-004] AGENT_010_fast_and_deep_agents_carry_the_same_allow_set', () => {
  const config = buildConfig()
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, outcome.error)

  // AGENT-010: fast and deep carry the same permission set.
  for (const tier of TIERS) {
    for (const role of ROLES) {
      assert.deepEqual(allowList(config, agentName(tier, role)), allowList(config, agentName(tier === 'fast' ? 'deep' : 'fast', role)))
    }
  }
})

test('WHAT[ENF-002] AGENT_006_role_tool_matrix_reaches_the_host_schema', () => {
  const config = buildConfig()
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, outcome.error)

  for (const role of ROLES) {
    for (const tier of TIERS) {
      const name = agentName(tier, role)
      const allowed = allowList(config, name).sort()
      assert.deepEqual(
        allowed,
        [...ROLE_ALLOW[role], ...HOST_UTILITY_ALLOW, ...cognitiveUtilityAllowFor(role)].sort(),
        `${name} allow set must equal AGENT-006 matrix + non-authority utilities`,
      )
    }
  }
})

test('WHAT[ENF-010] AGENT_007_bash_stays_denied_even_when_the_gate_fails', () => {
  // The live-config regression: a catalog validation failure used to
  // short-circuit BEFORE any write. EMR-008 made duplicate fast/deep models
  // legal, so the error path is a leftover legacy agent name.
  const config = buildConfig()
  config.agent.coder = { model: 'some-model' }
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, false, 'legacy agent name must fail the gate')
  assert.match(outcome.error, /Legacy agent name 'coder'/)

  for (const tier of TIERS) {
    for (const role of ROLES) {
      const name = agentName(tier, role)
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
  }
})

test('WHAT[ENF-010] AGENT_007_validation_error_is_still_reported', () => {
  const config = buildConfig()
  config.agent.coder = { model: 'some-model' }
  const outcome = validateManagedAgents(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /Legacy agent name 'coder'/)
})

test('WHAT[ENF-011] AGENT_002_missing_agent_is_projected_on_configure', () => {
  const config = buildConfig()
  delete config.agent['deep-coder']
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, outcome.error)
  const entry = config.agent['deep-coder']
  assert.equal(entry.mode, 'primary')
  assert.equal(entry.permission['*'], 'deny')
  assert.ok(typeof entry.prompt === 'string' && entry.prompt.length > 0)
  assert.equal('model' in entry, false)
})

test('WHAT[ENF-010] AGENT_004_legacy_agent_name_fails_validation', () => {
  const config = buildConfig()
  config.agent.coder = { model: 'some-model' }
  const outcome = validateManagedAgents(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /Legacy agent name 'coder'/)
})

test('WHAT[ENF-011] AGENT_002_owned_writes_never_touch_the_model_binding', () => {
  const config = buildConfig()
  const before = {}
  for (const name in config.agent) before[name] = config.agent[name].model
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, outcome.error)
  for (const name in before) {
    assert.equal(config.agent[name].model, before[name], `model binding of ${name} must be untouched`)
  }
})

test('WHAT[ENF-002] office_capability_permissions_agree_with_the_host_schema_matrix', () => {
  // Same matrix, expressed at the domain layer: the Host permission object is
  // built from OfficeCapability.permissions, so the two must agree per role.
  const permissionOf = (toolName) =>
    ({
      fork: 'Fork',
      commission: 'Fork',
      'open-terminal': 'Pty',
      'send-terminal': 'Pty',
      'read-terminal': 'Pty',
      'signal-terminal': 'Pty',
      join: 'Join',
      horizon: 'Horizon',
      todowrite: 'TodoWrite',
      fission: 'Fission',
      read: 'Read',
      write: 'Write',
      edit: 'Edit',
      glob: 'Glob',
      grep: 'Grep',
      mv: 'Move',
      rm: 'Remove',
      'bash-honeypot': 'BashHoneypot',
      inspect: 'Inspect',
      'sphinx_*': 'Sphinx',
      run: 'Exec',
      'query-shell': 'Exec',
      'establish-behavior': 'Behavior',
      'repair-behavior': 'Behavior',
      'stealth-browser-mcp_*': 'Network',
      judge: 'Judge',
      chronicle: 'Chronicle',
      fetch: 'Fetch',
      suicide: 'Finality',
    })[toolName]
  for (const role of ROLES) {
    const fromRoles = permissions(role.toLowerCase())
    const config = buildConfig()
    configureManagedAgents(config)
    const nonDomainUtilities = [...HOST_UTILITY_ALLOW, ...COGNITIVE_UTILITY_ALLOW]
    const fromSchema = [...new Set(allowList(config, agentName('fast', role)).filter((tool) => !nonDomainUtilities.includes(tool)).map(permissionOf))].sort()
    assert.deepEqual(fromSchema, fromRoles, `${role}: domain permissions must equal the Host schema allow list`)
  }
})

test('WHAT[ENF-006] HOST_skill_remains_allowed_for_every_managed_role', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)
  for (const tier of TIERS) {
    for (const role of ROLES) {
      assert.equal(evaluate(mergedRules(config, agentName(tier, role)), 'skill', '*').action, 'allow')
    }
  }
})

test('WHAT[ENF-006] ASSUME_is_a_non_authority_utility_for_interactive_roles_only', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)
  for (const tier of TIERS) {
    for (const role of ROLES) {
      const expected = role === 'Blogger' || role === 'Distiller' ? 'deny' : 'allow'
      assert.equal(
        evaluate(mergedRules(config, agentName(tier, role)), 'assume', '*').action,
        expected,
        `${agentName(tier, role)} assume permission`,
      )
    }
  }
})

test('WHAT[ENF-011] AGENT_019_external_directory_overrides_host_default_ask', () => {
  // AGENT-019: Host agent.ts defaults external_directory:* = ask. Managed agents
  // must emit a trailing allow so findLast cancels the Host ask on any external path.
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  for (const tier of TIERS) {
    for (const role of ROLES) {
      const name = agentName(tier, role)
      const rules = mergedRules(config, name)
      const action = evaluate(rules, 'external_directory', '/tmp/outside/*').action
      assert.equal(action, 'allow', `${name} must allow external_directory (got ${action})`)
      assert.equal(
        config.agent[name].permission.external_directory,
        'allow',
        `${name} permission object must set external_directory allow`,
      )
    }
  }
})
