import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { permissionObj } from '../../../dist/OpenCode/Tools/StaticTools.js'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { Role } from '../../../dist/Foundation/Roles.js'
import {
  ManagerCapabilityFacts,
  OfficeCapability_isAllowedForManagerFacts as isAllowedForManagerFacts,
  ToolPermission,
} from '../../../dist/Foundation/OfficeCapability.js'

const MANAGER_DEDICATED_TOOLS = ['read-manager', 'glob-manager', 'grep-manager']

test('WHAT[capability-enforcement-025] P01_unaccepted_review_manager_static_permissions_include_read_glob_grep', () => {
  const perms = office.permissions('manager')
  assert.ok(perms.includes('Read'), 'Manager static permissions must include Read')
  assert.ok(perms.includes('Glob'), 'Manager static permissions must include Glob')
  assert.ok(perms.includes('Grep'), 'Manager static permissions must include Grep')
})

test('WHAT[capability-enforcement-025] P02_review_accepted_facts_exclude_review_readonly_capabilities', () => {
  // 依据 dist/Foundation/OfficeCapability.js 构造已接纳评审事实（HasActiveIncumbency=true, HasAssessment=true）
  const facts = new ManagerCapabilityFacts(true, true, false, undefined)

  // 评审接纳后，只读能力 Read/Glob/Grep 均不被允许
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Read), false, 'Read must be denied after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Glob), false, 'Glob must be denied after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Grep), false, 'Grep must be denied after assessment accepted')

  // 评审接纳后，Join/Fork 等管理与编排权限仍被允许
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Fork), true, 'Fork must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Resume), true, 'Resume must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Join), true, 'Join must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Horizon), true, 'Horizon must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Finality), true, 'Finality must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Sphinx), true, 'Sphinx must remain allowed after assessment accepted')
})

test('WHAT[capability-enforcement-025] P08_manager_native_read_glob_grep_projected_as_deny', () => {
  const managerPerms = permissionObj(Role.Manager)
  assert.equal(managerPerms.read, 'deny', 'Native read must be projected as deny for Manager')
  assert.equal(managerPerms.glob, 'deny', 'Native glob must be projected as deny for Manager')
  assert.equal(managerPerms.grep, 'deny', 'Native grep must be projected as deny for Manager')
})

test('WHAT[capability-enforcement-025] P09_manager_denies_programming_tools_and_engineers_deny_manager_dedicated_tools', () => {
  const managerPerms = permissionObj(Role.Manager)
  assert.equal(managerPerms['js-engineer'], 'deny', 'Manager must deny js-engineer')
  assert.equal(managerPerms['js-devops'], 'deny', 'Manager must deny js-devops')
  assert.equal(rolePredicate('js-engineer', 'manager'), false, 'Role predicate must deny js-engineer for Manager')
  assert.equal(rolePredicate('js-devops', 'manager'), false, 'Role predicate must deny js-devops for Manager')

  for (const tool of MANAGER_DEDICATED_TOOLS) {
    assert.equal(rolePredicate(tool, 'engineer'), false, `Engineer must deny dedicated tool ${tool}`)
    assert.equal(rolePredicate(tool, 'devops'), false, `DevOps must deny dedicated tool ${tool}`)
  }
})

test('WHAT[capability-enforcement-025] P10_three_dedicated_tools_projected_as_allow_in_manager_request', () => {
  const managerPerms = permissionObj(Role.Manager)
  for (const tool of MANAGER_DEDICATED_TOOLS) {
    assert.equal(managerPerms[tool], 'allow', `Manager request projection must allow dedicated tool ${tool}`)
  }
})

test('WHAT[capability-enforcement-025] P05_no_active_incumbency_and_no_assessment_denies_review_readonly_admission', () => {
  for (const tool of MANAGER_DEDICATED_TOOLS) {
    assert.equal(
      rolePredicate(tool, 'manager'),
      false,
      `Without active incumbency and assessment, admission for ${tool} must fail closed`,
    )
  }
})

test('WHAT[capability-enforcement-025] P06_unknown_identity_never_authorized_by_tool_name_suffix', () => {
  const unknownCallers = ['unknown', 'guest', 'coder', 'inspector', '']
  for (const caller of unknownCallers) {
    assert.equal(rolePredicate('js-unknown', caller), false, `Unknown caller '${caller}' must not be admitted by js- suffix`)
    assert.equal(rolePredicate('read-manager', caller), false, `Unknown caller '${caller}' must not be admitted to read-manager`)
    assert.equal(rolePredicate('glob-manager', caller), false, `Unknown caller '${caller}' must not be admitted to glob-manager`)
    assert.equal(rolePredicate('grep-manager', caller), false, `Unknown caller '${caller}' must not be admitted to grep-manager`)
    assert.equal(rolePredicate('js-engineer', caller), false, `Unknown caller '${caller}' must not be admitted to js-engineer`)
  }
})
