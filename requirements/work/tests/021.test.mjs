import assert from 'node:assert/strict'
import test from 'node:test'
import { allPublicRoleLabels, allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'
import { admissionAuthority, capabilityToolNames, rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { toolSpecNames } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { isAllowed, permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[work-021] Role catalog contains Plan and wire label is plan', () => {
  // Roles.all must contain Role.Plan with wire label 'plan'
  assert.ok(allRoleLabels.includes('plan'), 'Roles.all must contain Role.Plan with wire label "plan"')
  assert.ok(allPublicRoleLabels.includes('plan'), 'allPublicRoleLabels must contain "plan"')
})

test('WHAT[work-021] Plan separates human ask from devops investigation and denies direct execution', () => {
  const allowed = permissions('plan')
  assert.ok(Array.isArray(allowed), 'permissions("plan") must return an array')

  // WHAT[work-021]: ask asks the human; devops delegates an investigation.
  // resume and handoff are no longer part of the plan surface.
  const expectedPermissions = ['Ask', 'DevOpsConsultation', 'Deliver', 'JsPlan', 'Review']
  assert.deepEqual(
    [...allowed].sort(),
    expectedPermissions.sort(),
    'Plan must expose distinct human-question and DevOps capabilities',
  )

  // Each designated capability is actually allowed.
  for (const perm of expectedPermissions) {
    assert.equal(isAllowed('plan', perm), true, `Plan must be allowed ${perm}`)
  }

  // Retired plan tools must be strictly denied
  for (const perm of ['Handoff', 'Resume']) {
    assert.equal(isAllowed('plan', perm), false, `Plan must be denied retired tool ${perm}`)
  }

  // Must strictly deny repository, execution and orchestration tools
  const forbiddenPermissions = [
    'Read',
    'Write',
    'Edit',
    'Glob',
    'Grep',
    'Move',
    'Remove',
    'Exec',
    'Pty',
    'Fork',
    'Fission',
    'Fetch',
    'Finality',
    'BashHoneypot',
  ]
  for (const perm of forbiddenPermissions) {
    assert.equal(isAllowed('plan', perm), false, `Plan must be denied ${perm}`)
  }

  // 真实注册的 devops 工具存在且 Plan 角色可调用；非 Plan 角色拒绝
  assert.ok(toolSpecNames().includes('devops'), 'Tool catalog must contain registered devops tool')
  assert.equal(admissionAuthority('devops'), 'office', 'devops tool must resolve to office admission authority')
  assert.equal(rolePredicate('devops', 'plan'), true, 'Plan role must be admitted to devops tool')
  assert.ok(capabilityToolNames('plan', 'work-main').includes('devops'), 'Plan work-main capabilities must include devops')

  const nonPlanRoles = allRoleLabels.filter((role) => role !== 'plan')
  for (const role of nonPlanRoles) {
    assert.equal(rolePredicate('devops', role), false, `Non-plan role "${role}" must be denied devops tool`)
    assert.equal(capabilityToolNames(role, 'work-main').includes('devops'), false, `Non-plan role "${role}" must not expose devops tool`)
  }
})
