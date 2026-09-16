// ENF-006: Internal-only tools and actions isolation
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  configure as configureManagedAgents,
  installDefaultResources,
} from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { admissionAuthority, privateAttachmentAdmits, rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { chronicleContract } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'
import { permissions as rolePermissions, isAllowed as surfaceIsAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

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

const READ_PERMISSIONS = ['Read', 'Glob', 'Grep']
const OFFICE_TOOLS = ['inspect', 'fetch', 'review', 'join', 'chronicle', 'run', 'fork', 'resume']

test('WHAT[ENF-006] HOST_skill_remains_allowed_for_every_managed_role', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)
  for (const role of ROLES) {
    assert.equal(evaluate(mergedRules(config, agentName(role)), 'skill', '*').action, 'allow')
  }
})

test('WHAT[ENF-006] ASSUME_is_a_non_authority_utility_for_interactive_roles_only', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)
  for (const role of ROLES) {
    const expected = role === 'Blogger' || role === 'Distiller' ? 'deny' : 'allow'
    assert.equal(
      evaluate(mergedRules(config, agentName(role)), 'assume', '*').action,
      expected,
      `${agentName(role)} assume permission`,
    )
  }
})

test('WHAT[ENF-006] CHRONICLE_spec_exposes_identity_and_argument_surface', () => {
  const contract = chronicleContract()
  assert.equal(contract.name, 'chronicle')
  assert.deepEqual(contract.argumentNames, ['entry', 'tip'])
  assert.equal(contract.tipCount, 120)
})

test('WHAT[ENF-006] Inquiry_permissions_are_inspect_sphinx_and_fission', () => {
  const allowed = rolePermissions('inquiry')
  assert.deepEqual(allowed, ['Fission', 'Inspect', 'Sphinx'])
  assert.equal(allowed.includes('Read'), false)
  assert.equal(allowed.includes('Glob'), false)
  assert.equal(allowed.includes('Grep'), false)
})

test('WHAT[ENF-006] Inquiry_isAllowed_denies_read_glob_grep_and_allows_inspect_sphinx_fission', () => {
  assert.equal(surfaceIsAllowed('inquiry', 'Inspect'), true)
  assert.equal(surfaceIsAllowed('inquiry', 'Sphinx'), true)
  assert.equal(surfaceIsAllowed('inquiry', 'Fission'), true)
  for (const permission of READ_PERMISSIONS) {
    assert.equal(surfaceIsAllowed('inquiry', permission), false, `Inquiry must lack ${permission}`)
  }
})

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
