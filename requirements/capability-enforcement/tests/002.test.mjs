// ENF-002: Provider-visible schema and runtime gate read identical capability truth
import assert from 'node:assert/strict'
import test from 'node:test'
import { permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import {
  configure as configureManagedAgents,
  installDefaultResources,
} from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

const { allRoleLabels, allPublicRoleLabels, allInternalRoleLabels } =
  await import('../../../dist/Foundation/RolesSurface.js')
const { nameOf: managedAgentName } = await import('../../../dist/Participant/Persona/Surface.js')

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

const HOST_UTILITY_ALLOW = ['skill']
const COGNITIVE_UTILITY_ALLOW = ['assume']
const cognitiveUtilityAllowFor = (role) => role === 'Blogger' || role === 'Distiller' ? [] : COGNITIVE_UTILITY_ALLOW

const ROLE_ALLOW = {
  Manager: ['fork', 'resume', 'join', 'horizon', 'todowrite', 'fission', 'suicide', 'review'],
  Orchestrator: ['commission', 'join', 'horizon'],
  Coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspect', 'mv', 'rm', 'bash-honeypot', 'fetch', 'fission'],
  Inspector: ['read', 'glob', 'grep', 'fetch', 'fission'],
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

const READ_TOOLS = ['read', 'glob', 'grep']

test('WHAT[ENF-002] AGENT_006_role_tool_matrix_reaches_the_host_schema', () => {
  const config = buildConfig()
  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, outcome.error)

  for (const role of ROLES) {
    const name = agentName(role)
    const allowed = allowList(config, name).sort()
    assert.deepEqual(
      allowed,
      [...ROLE_ALLOW[role], ...HOST_UTILITY_ALLOW, ...cognitiveUtilityAllowFor(role)].sort(),
      `${name} allow set must equal AGENT-006 matrix + non-authority utilities`,
    )
  }
})

test('WHAT[ENF-002] office_capability_permissions_agree_with_the_host_schema_matrix', () => {
  const permissionOf = (toolName) =>
    ({
      fork: 'Fork',
      resume: 'Fork',
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
      review: 'ReviewAssessment',
      chronicle: 'Chronicle',
      fetch: 'Fetch',
      suicide: 'Finality',
    })[toolName]
  for (const role of ROLES) {
    const fromRoles = permissions(role.toLowerCase())
    const config = buildConfig()
    configureManagedAgents(config)
    const nonDomainUtilities = [...HOST_UTILITY_ALLOW, ...COGNITIVE_UTILITY_ALLOW]
    const fromSchema = [...new Set(allowList(config, agentName(role)).filter((tool) => !nonDomainUtilities.includes(tool)).map(permissionOf))].sort()
    assert.deepEqual(fromSchema, fromRoles, `${role}: domain permissions must equal the Host schema allow list`)
  }
})

test('WHAT[ENF-002] Inquiry_host_schema_allow_list_is_inspect_sphinx_and_fission', () => {
  const config = {
    agent: {
      inquiry: { model: 'inquiry-model' },
    },
  }
  for (const role of [
    'manager',
    'orchestrator',
    'coder',
    'inspector',
    'browser',
    'devops',
    'distiller',
    'blogger',
    'bookkeeper',
    'predictor',
  ]) {
    const name = role
    if (!config.agent[name]) config.agent[name] = { model: `${name}-model` }
  }

  const outcome = configureManagedAgents(config)
  assert.equal(outcome.ok, true, outcome.error)

  for (const name of ['inquiry']) {
    const permission = config.agent[name].permission
    assert.equal(permission['*'], 'deny')
    assert.equal(permission.inspect, 'allow')
    assert.equal(permission['sphinx_*'], 'allow')
    assert.equal(permission.fission, 'allow')
    for (const tool of READ_TOOLS) {
      assert.notEqual(permission[tool], 'allow', `${name} must not allow ${tool}`)
    }
    assert.notEqual(permission['stealth-browser-mcp_*'], 'allow', `${name} must not allow stealth-browser MCP`)
  }
})

test('WHAT[ENF-002] P7_SURFACE_role_labels_are_js_native_strings', () => {
  assertJsData(allRoleLabels, 'allRoleLabels')
  assert.equal(allRoleLabels.length, 9, 'exactly nine canonical roles')
  assert.deepEqual(
    allRoleLabels,
    ['blogger', 'browser', 'coder', 'devops', 'inquiry', 'inspector', 'manager', 'orchestrator', 'distiller']
      .sort(),
  )
})

test('WHAT[ENF-002] P7_SURFACE_public_internal_partition_and_managed_agent_name_are_js_native', () => {
  assertJsData(allPublicRoleLabels, 'allPublicRoleLabels')
  assertJsData(allInternalRoleLabels, 'allInternalRoleLabels')
  assert.deepEqual(allPublicRoleLabels, ['browser', 'coder', 'devops', 'inquiry', 'inspector', 'manager', 'orchestrator'])
  assert.deepEqual(allInternalRoleLabels, ['blogger', 'distiller'])
  assert.equal(allPublicRoleLabels.length + allInternalRoleLabels.length, allRoleLabels.length)
  assert.equal(managedAgentName('fast', 'distiller'), 'distiller')
  assert.equal(managedAgentName('deep', 'blogger'), 'blogger')
  assert.equal(managedAgentName('coder', 'coder'), 'coder')
  assert.equal(managedAgentName('fast', 'not-a-role'), '', 'unknown role fails closed to empty name')
  assert.equal(managedAgentName('not-a-tier', 'coder'), 'coder', 'canonical role resolves without a tier')
})

test('WHAT[ENF-002] TOOLSPEC_delegation_tools_have_owner_defined_admission', () => {
  assert.equal(rolePredicate('fork', 'manager'), true)
  assert.equal(rolePredicate('fork', 'coder'), false)
  assert.equal(rolePredicate('fork', 'orchestrator'), false)
  assert.equal(rolePredicate('resume', 'manager'), true)
  assert.equal(rolePredicate('resume', 'coder'), false)
  assert.equal(rolePredicate('resume', 'orchestrator'), false)

  assert.equal(rolePredicate('commission', 'orchestrator'), true)
  assert.equal(rolePredicate('commission', 'manager'), false)
  assert.equal(rolePredicate('commission', 'coder'), false)

  assert.equal(rolePredicate('join', 'manager'), true)
  assert.equal(rolePredicate('join', 'orchestrator'), true)
  assert.equal(rolePredicate('join', 'coder'), false)

  assert.equal(rolePredicate('horizon', 'manager'), true)
  assert.equal(rolePredicate('horizon', 'orchestrator'), true)
  assert.equal(rolePredicate('horizon', 'coder'), false)
})

test('WHAT[ENF-002] TOOLSPEC_coder_and_devops_tools_have_owner_defined_admission', () => {
  assert.equal(rolePredicate('bash-honeypot', 'coder'), true)
  assert.equal(rolePredicate('bash-honeypot', 'inspector'), false)
  assert.equal(rolePredicate('bash-honeypot', 'devops'), false)

  assert.equal(rolePredicate('mv', 'coder'), true)
  assert.equal(rolePredicate('mv', 'inspector'), false)
  assert.equal(rolePredicate('rm', 'coder'), true)
  assert.equal(rolePredicate('rm', 'inspector'), false)

  assert.equal(rolePredicate('open-terminal', 'devops'), true)
  assert.equal(rolePredicate('open-terminal', 'coder'), false)
  assert.equal(rolePredicate('send-terminal', 'devops'), true)
  assert.equal(rolePredicate('read-terminal', 'devops'), true)
  assert.equal(rolePredicate('signal-terminal', 'devops'), true)

  assert.equal(rolePredicate('run', 'devops'), true)
  assert.equal(rolePredicate('run', 'inspector'), false)
  assert.equal(rolePredicate('query-shell', 'inspector'), false)
  assert.equal(rolePredicate('query-shell', 'devops'), false)

  assert.equal(rolePredicate('establish-behavior', 'devops'), true)
  assert.equal(rolePredicate('establish-behavior', 'coder'), false)
  assert.equal(rolePredicate('repair-behavior', 'devops'), true)
  assert.equal(rolePredicate('repair-behavior', 'coder'), false)
})

test('WHAT[ENF-002] TOOLSPEC_cognitive_utility_and_fission_tools_admission', () => {
  for (const tool of ['assume', 'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']) {
    assert.equal(rolePredicate(tool, 'coder'), true, `${tool} should be allowed for coder`)
    assert.equal(rolePredicate(tool, 'manager'), true, `${tool} should be allowed for manager`)
    assert.equal(rolePredicate(tool, 'blogger'), false, `${tool} should be denied for blogger`)
    assert.equal(rolePredicate(tool, 'distiller'), false, `${tool} should be denied for distiller`)
  }

  assert.equal(rolePredicate('fission', 'manager'), true)
  assert.equal(rolePredicate('fission', 'coder'), true)
  assert.equal(rolePredicate('fission', 'inspector'), true)
  assert.equal(rolePredicate('fission', 'inquiry'), true)
  assert.equal(rolePredicate('fission', 'devops'), false)
})
