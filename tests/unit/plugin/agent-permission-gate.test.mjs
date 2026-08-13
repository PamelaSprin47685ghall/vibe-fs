// tests/unit/Plugin/agent-permission-gate.test.mjs — AGENT-002 / AGENT-006 / AGENT-007.
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
import { managedAgentConfig, roles, runtimeResources } from '../support/domain.mjs'

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

/** Plugin permission object → the ruleset Host's `fromConfig` would build. */
const rulesOf = (permissionObj) => {
  const rules = []
  for (const [key, value] of Object.entries(permissionObj)) {
    if (typeof value === 'string') {
      rules.push({ permission: key, action: value, pattern: '*' })
      continue
    }
    for (const [pattern, action] of Object.entries(value)) rules.push({ permission: key, pattern, action })
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
    'auto-injected',
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
  ]
  return tools.filter((tool) => evaluate(rules, tool, '*').action === 'allow')
}

// AGENT-006 matrix (tool names as they reach the Host permission schema).
const ROLE_ALLOW = {
  Manager: ['fork', 'join', 'horizon', 'todowrite', 'fission', 'suicide', 'auto-injected'],
  Orchestrator: ['commission', 'join', 'horizon', 'auto-injected'],
  Coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspect', 'mv', 'rm', 'bash-honeypot', 'fetch', 'auto-injected'],
  Inspector: ['read', 'glob', 'grep', 'query-shell', 'fetch', 'auto-injected'],
  Browser: ['read', 'glob', 'grep', 'stealth-browser-mcp_*', 'auto-injected'],
  Inquiry: ['inspect', 'sphinx_*', 'auto-injected'],
  Reviewer: ['read', 'glob', 'grep', 'judge', 'auto-injected'],
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
    'auto-injected',
  ],
  Distiller: [],
  Blogger: ['chronicle'],
}

test.before(() => {
  runtimeResources.installFromPackage()
})

test('AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields', () => {
  const config = buildConfig()
  const outcome = managedAgentConfig.configure(config)
  assert.equal(outcome.ok, true, `gate must accept distinct models: ${outcome.error}`)

  // Every managed agent got the owned mode + permission + prompt.
  for (const tier of TIERS) {
    for (const role of ROLES) {
      const entry = config.agent[agentName(tier, role)]
      assert.equal(entry.mode, 'primary', `${agentName(tier, role)} must be primary`)
      assert.ok(entry.permission && entry.permission['*'] === 'deny', `${agentName(tier, role)} must deny by default`)
      assert.ok(typeof entry.prompt === 'string' && entry.prompt.length > 0, `${agentName(tier, role)} must carry a prompt`)
      // AGENT-010: fast and deep carry the same permission set.
      assert.deepEqual(allowList(config, agentName(tier, role)), allowList(config, agentName(tier === 'fast' ? 'deep' : 'fast', role)))
    }
  }
})

test('AGENT_006_role_tool_matrix_reaches_the_host_schema', () => {
  const config = buildConfig()
  const outcome = managedAgentConfig.configure(config)
  assert.equal(outcome.ok, true, outcome.error)

  for (const role of ROLES) {
    for (const tier of TIERS) {
      const name = agentName(tier, role)
      const allowed = allowList(config, name).sort()
      assert.deepEqual(allowed, [...ROLE_ALLOW[role]].sort(), `${name} allow set must equal AGENT-006 matrix`)
    }
  }
})

test('AGENT_007_bash_stays_denied_even_when_the_gate_fails', () => {
  // The live-config regression: a duplicated fast/deep model pair makes
  // `configureFromHostConfig` return Error, but the permission writes must
  // still land — otherwise bash opens for every role.
  const config = buildConfig({ duplicateBrowserModel: true })
  const outcome = managedAgentConfig.configure(config)
  assert.equal(outcome.ok, false, 'duplicate pair must fail the gate')
  assert.match(outcome.error, /same model/)

  for (const tier of TIERS) {
    for (const role of ROLES) {
      const name = agentName(tier, role)
      const entry = config.agent[name]
      assert.equal(entry.mode, 'primary', `${name} mode must survive a gate error`)
      assert.ok(entry.permission && entry.permission['*'] === 'deny', `${name} must keep "*": deny after a gate error`)
      assert.deepEqual(
        allowList(config, name).sort(),
        [...ROLE_ALLOW[role]].sort(),
        `${name} tool set must survive a gate error`,
      )
      assert.ok(!allowList(config, name).includes('bash'), `${name} must never allow bash`)
    }
  }
})

test('AGENT_007_validation_error_is_still_reported', () => {
  const config = buildConfig({ duplicateBrowserModel: true })
  const outcome = managedAgentConfig.validate(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /fast-browser\/deep-browser/)
})

test('AGENT_002_missing_agent_fails_validation', () => {
  const config = buildConfig()
  delete config.agent['deep-coder']
  const outcome = managedAgentConfig.validate(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /deep-coder/)
})

test('AGENT_004_legacy_agent_name_fails_validation', () => {
  const config = buildConfig()
  config.agent.coder = { model: 'some-model' }
  const outcome = managedAgentConfig.validate(config)
  assert.equal(outcome.ok, false)
  assert.match(outcome.error, /Legacy agent name 'coder'/)
})

test('AGENT_002_owned_writes_never_touch_the_model_binding', () => {
  const config = buildConfig()
  const before = Object.fromEntries(Object.entries(config.agent).map(([k, v]) => [k, v.model]))
  const outcome = managedAgentConfig.configure(config)
  assert.equal(outcome.ok, true, outcome.error)
  for (const [name, model] of Object.entries(before)) {
    assert.equal(config.agent[name].model, model, `model binding of ${name} must be untouched`)
  }
})

test('roles.permissions_agree_with_the_host_schema_matrix', () => {
  // Same matrix, expressed at the domain layer: the Host permission object is
  // built from Roles.permissions, so the two must agree per role.
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
      'auto-injected': 'AutoInjected',
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
    const fromRoles = roles.permissions(roles.of(role))
    const config = buildConfig()
    managedAgentConfig.configure(config)
    const fromSchema = [...new Set(allowList(config, agentName('fast', role)).map(permissionOf))].sort()
    assert.deepEqual(fromSchema, fromRoles, `${role}: domain permissions must equal the Host schema allow list`)
  }
})

test('AGENT_019_external_directory_overrides_host_default_ask', () => {
  // AGENT-019: Host agent.ts defaults external_directory:* = ask. Managed agents
  // must emit a trailing allow so findLast cancels the Host ask on any external path.
  const config = buildConfig()
  assert.equal(managedAgentConfig.configure(config).ok, true)

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
