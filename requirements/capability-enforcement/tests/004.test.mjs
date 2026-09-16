// ENF-004: Execution tier invariance of authority
import assert from 'node:assert/strict'
import test from 'node:test'
import { plan } from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import {
  configure as configureManagedAgents,
  installDefaultResources,
} from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

installDefaultResources()

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

test('WHAT[ENF-004] AGENT_010_canonical_agents_carry_stable_allow_sets', () => {
  const first = buildConfig()
  const second = buildConfig()
  assert.equal(configureManagedAgents(first).ok, true)
  assert.equal(configureManagedAgents(second).ok, true)

  for (const role of ROLES) {
    assert.deepEqual(allowList(first, agentName(role)), allowList(second, agentName(role)))
  }
})

test('WHAT[ENF-004] AGENT_010_the_tier_does_not_reach_the_system_prompt_or_the_tool_set', () => {
  const fast = plan({ role: 'coder', tier: 'fast', kind: 'work-main' })
  const deep = plan({ role: 'coder', tier: 'deep', kind: 'work-main' })

  assert.equal(fast.ok, true, fast.error)
  assert.equal(deep.ok, true, deep.error)
  assert.equal(fast.systemPromptId, deep.systemPromptId)
  assert.deepEqual(fast.toolCapabilities, deep.toolCapabilities)
})
