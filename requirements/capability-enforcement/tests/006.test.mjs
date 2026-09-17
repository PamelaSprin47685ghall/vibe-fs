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

test('WHAT[ENF-006] HOST_skill_is_a_host_utility_for_interactive_roles_only', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)
  for (const role of ROLES) {
    const expected = role === 'Blogger' ? 'deny' : 'allow'
    assert.equal(
      evaluate(mergedRules(config, agentName(role)), 'skill', '*').action,
      expected,
      `${agentName(role)} skill permission`,
    )
  }
})
test('WHAT[ENF-006] ASSUME_is_a_non_authority_utility_for_interactive_roles_only', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)
  for (const role of ROLES) {
    const expected = role === 'Blogger' ? 'deny' : 'allow'
    assert.equal(
      evaluate(mergedRules(config, agentName(role)), 'assume', '*').action,
      expected,
      `${agentName(role)} assume permission`,
    )
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { markerSource, markerToolName } = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");
const { rolePredicate } = await import("../../../dist/OpenCode/Tools/ToolRegistrySurface.js");
const { withExecutablePlugin, withPlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");

const withSession = (messages, sessionID = 'ses-auto-injected') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))
const admitManagedRoot = async (hooks, sessionID = 'ses-auto-injected') => {
  const output = {
    message: {
      id: `root-${sessionID}`,
      role: 'user',
      sessionID,
      agent: 'engineer',
      model: { providerID: 'host', modelID: 'placeholder' },
    },
    parts: [],
  }
  await hooks['chat.message']({ sessionID, agent: 'engineer' }, output)
}

test('WHAT[ENF-006] AUTOINJ_skill_wire_stays_host_owned_and_is_not_plugin_registered', async () => {
  assert.equal(markerToolName, 'skill')
  assert.equal(rolePredicate('skill', 'engineer'), false, 'Host-owned skill is not a plugin role tool')
  assert.equal(rolePredicate('skill', 'manager'), false)
  assert.equal(rolePredicate('skill', 'blogger'), false)

  await withPlugin(async (hooks) => {
    assert.equal(hooks.tool['auto-injected'], undefined, 'legacy auto-injected must not be in hooks.tool')
    assert.equal(hooks.tool.skill, undefined, 'skill remains Host-owned rather than plugin-registered')
  })
})
test('WHAT[ENF-006] AUTOINJ_active_empty_skill_call_is_denied_without_touching_real_skill_names', async () => {
  await withExecutablePlugin(async (hooks) => {
    await admitManagedRoot(hooks)
    const transformed = {
      messages: withSession([
        {
          role: 'assistant',
          info: { id: 'asst-empty-skill' },
          parts: [{
            type: 'tool',
            tool: 'skill',
            callID: 'call-empty',
            state: { status: 'error', input: { name: '' }, error: 'Skill not found' },
          }],
        },
        {
          role: 'assistant',
          info: { id: 'asst-real-skill' },
          parts: [{
            type: 'tool',
            tool: 'skill',
            callID: 'call-real',
            state: { status: 'completed', input: { name: 'pdfs' }, output: 'real skill output' },
          }],
        },
        {
          role: 'user',
          info: { id: 'root-ses-auto-injected' },
          parts: [{ type: 'text', text: 'hello' }],
        },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    const rewritten = transformed.messages.find((message) => message.info?.id === 'asst-empty-skill')
    assert.ok(rewritten)
    const part = rewritten.parts[0]
    assert.equal(part.state.status, 'completed', 'empty-name skill failure must be rewritten to completed')
    assert.equal(part.state.error, undefined, 'error field must be cleared')
    assert.match(part.state.output, /DENIED|禁止/, 'result must contain denial text')
    assert.match(part.state.output, /skill/, 'denial must identify the reserved empty-name skill load')

    const real = transformed.messages.find((message) => message.info?.id === 'asst-real-skill')
    assert.ok(real)
    assert.deepEqual(real.parts[0].state.input, { name: 'pdfs' })
    assert.equal(real.parts[0].state.output, 'real skill output')
  })
})
test('WHAT[ENF-006] AUTOINJ_tryInject_rewrites_active_call_without_synthetic_injection', async () => {
  await withExecutablePlugin(async (hooks) => {
    await admitManagedRoot(hooks)
    const transformed = {
      messages: withSession([
        {
          role: 'assistant',
          info: { id: 'asst-1' },
          parts: [
            {
              type: 'tool',
              tool: 'skill',
              callID: 'call-active',
              state: { status: 'error', input: { name: '' }, error: 'Skill not found' },
            },
          ],
        },
        {
          role: 'user',
          info: { id: 'root-ses-auto-injected' },
          parts: [{ type: 'text', text: 'hello' }],
        },
      ]),
    }

    await hooks['experimental.chat.messages.transform']({}, transformed)
    const rewrittenActive = transformed.messages.find((message) => message.info?.id === 'asst-1')
    assert.ok(rewrittenActive)
    assert.equal(rewrittenActive.parts[0].state.status, 'completed')
    assert.match(rewrittenActive.parts[0].state.output, /DENIED/)

    const synthetic = transformed.messages.find(
      (message) => message.info?.source === markerSource,
    )
    assert.equal(synthetic, undefined, 'zero-synthetic mode must not inject a synthetic skill row')
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { installDefaultResources } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");
const { chronicleContract } = await import("../../../dist/OpenCode/Tools/ToolSurface.js");

installDefaultResources()

test('WHAT[ENF-006] CHRONICLE_spec_exposes_identity_and_argument_surface', () => {
  const contract = chronicleContract()
  assert.equal(contract.name, 'chronicle')
  assert.deepEqual(contract.argumentNames, ['entry', 'tip'])
  assert.equal(contract.tipCount, 120)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { rolePredicate } = await import("../../../dist/OpenCode/Tools/ToolRegistrySurface.js");
const { permissions: rolePermissions, isAllowed: surfaceIsAllowed } = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { allRoleLabels } = await import("../../../dist/Foundation/RolesSurface.js");


test('WHAT[ENF-006] inquiry_role_is_revoked_and_permissions_fail_closed', () => {
  assert.equal(allRoleLabels.includes('inquiry'), false, 'Inquiry must not be in canonical active roles')
  const allowed = rolePermissions('inquiry')
  assert.deepEqual(allowed, [], 'inquiry permissions must fail closed to empty set')
})
test('WHAT[ENF-006] inquiry_isAllowed_denies_all_tools', () => {
  assert.equal(surfaceIsAllowed('inquiry', 'Inspect'), false)
  assert.equal(surfaceIsAllowed('inquiry', 'Sphinx'), false)
  assert.equal(surfaceIsAllowed('inquiry', 'Fission'), false)
  assert.equal(surfaceIsAllowed('inquiry', 'Read'), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { admissionAuthority, privateAttachmentAdmits, rolePredicate } = await import("../../../dist/OpenCode/Tools/ToolRegistrySurface.js");
const { allRoleLabels } = await import("../../../dist/Foundation/RolesSurface.js");

const OFFICE_TOOLS = ['fetch', 'review', 'join', 'chronicle', 'run', 'fork', 'resume']

test('WHAT[ENF-006] internal_leaf_tool_declares_attachment_authority_not_a_public_office', () => {
  assert.equal(admissionAuthority('js-bookkeeper'), 'private-attachment')
  for (const tool of OFFICE_TOOLS) {
    assert.equal(admissionAuthority(tool), 'office', `${tool} is an office tool`)
  }
  assert.equal(admissionAuthority('no-such-tool'), 'unknown')
})
test('WHAT[ENF-006] internal_leaf_tool_is_invisible_to_every_public_office_role', () => {
  assert.ok(allRoleLabels.length > 0)
  for (const role of allRoleLabels) {
    assert.equal(rolePredicate('js-bookkeeper', role), false, `js-bookkeeper must stay invisible to ${role}`)
  }
})
test('WHAT[ENF-006] attachment_authority_is_fail_closed_without_an_attached_transaction', () => {
  assert.equal(privateAttachmentAdmits('js-bookkeeper', 'ses-never-attached'), false)
  assert.equal(privateAttachmentAdmits('js-bookkeeper', ''), false)
})
test('WHAT[ENF-006] an_office_tool_can_never_be_admitted_through_the_attachment_path', () => {
  for (const tool of OFFICE_TOOLS) {
    assert.equal(privateAttachmentAdmits(tool, 'ses-any'), false, `${tool} must not take the attachment path`)
  }
  assert.equal(privateAttachmentAdmits('no-such-tool', 'ses-any'), false)
})
}
