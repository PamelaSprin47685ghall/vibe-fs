/**
 * office-capability — package-owned live-repo canary (ARCH-017 / Gate F).
 *
 * Entitled consequence must hit every decision surface: Manager Role Law (worldview),
 * fork/resume description (call-time choice), and each office's own Role Law
 * (self-model). Projection wording may differ; the consequence must not.
 *
 * this file is the live-repo canary that scans the real resources.
 *
 * Imports: node builtins + dist only (contract §4.6).
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  isAllowed,
  managerForkableOffices,
  permissions,
} from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const PROVIDER = join(ROOT, 'resources/provider')
const read = (rel) => readFileSync(join(PROVIDER, rel), 'utf8')

// ── Canonical active offices consequence catalog ──
const ACTIVE_OFFICES = [
  {
    id: 'engineer-investigation-mutation',
    managerEn: /entrust.*Engineer/i,
    managerZh: /托付.*Engineer/,
    forkEn: /Engineer[sS]{0,160}local facts[sS]{0,80}source/i,
    forkZh: /Engineer[sS]{0,120}本地事实[sS]{0,80}源码/,
    lawEn: /local facts|changing the written world|implement.*refactor/i,
    lawZh: /本地事实|书写出来的世界|源码/,
  },
  {
    id: 'devops-execution-repair',
    managerEn: /DevOps/i,
    managerZh: /DevOps/,
    forkEn: /DevOps[sS]{0,160}execution[sS]{0,80}repair/i,
    forkZh: /DevOps[sS]{0,120}执行[sS]{0,80}修复/,
    lawEn: /operational objective|non-architectural repair/i,
    lawZh: /operational objective|非架构级.*修复/,
  },
]

test('WHAT[OFF-007] OFF_007_manager_forkable_offices_is_strictly_engineer', () => {
  assert.deepEqual(managerForkableOffices(), ['Engineer'])
})

test('WHAT[ENF-002] office_permission_surface_matches_the_canonical_roles_matrix', () => {
  const matrix = [
    ['manager', ['Finality', 'Fork', 'Horizon', 'Join', 'ReviewAssessment', 'TodoWrite']],
    ['orchestrator', ['Fork', 'Horizon', 'Join']],
    ['engineer', ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Move', 'Read', 'Remove', 'Write']],
    ['devops', ['Edit', 'Exec', 'Glob', 'Grep', 'Horizon', 'Join', 'Move', 'Pty', 'Read', 'Remove', 'Write']],
    ['blogger', ['Chronicle']],
  ]

  for (const [role, expected] of matrix) {
    assertJsData(permissions(role), `permissions(${role})`)
    assert.deepEqual(permissions(role), expected, `permissions(${role}) must equal the canonical matrix`)
  }

  assert.deepEqual(permissions('not-a-role'), [], 'unknown role fails closed to empty set')
  assert.deepEqual(permissions('coder'), [], 'retired role fails closed to empty set')
  assert.deepEqual(permissions('inspector'), [], 'retired role fails closed to empty set')
  assert.deepEqual(permissions('browser'), [], 'retired role fails closed to empty set')
  assert.deepEqual(permissions('inquiry'), [], 'retired role fails closed to empty set')
  assert.deepEqual(permissions('distiller'), [], 'retired role fails closed to empty set')
})

test('WHAT[ENF-002] office_permission_surface_denies_outside_the_matrix', () => {
  // Engineer permissions
  assert.equal(isAllowed('engineer', 'Fission'), true, 'Engineer has Fission')
  assert.equal(isAllowed('engineer', 'Read'), true, 'Engineer has Read')
  assert.equal(isAllowed('engineer', 'Write'), true, 'Engineer has Write')
  assert.equal(isAllowed('engineer', 'Exec'), false, 'Engineer lacks Exec')
  assert.equal(isAllowed('engineer', 'Pty'), false, 'Engineer lacks Pty')

  // DevOps permissions
  assert.equal(isAllowed('devops', 'Exec'), true, 'DevOps has Exec')
  assert.equal(isAllowed('devops', 'Pty'), true, 'DevOps has Pty')
  assert.equal(isAllowed('devops', 'Write'), true, 'DevOps has Write')
  assert.equal(isAllowed('devops', 'Edit'), true, 'DevOps has Edit')
  assert.equal(isAllowed('devops', 'Fission'), false, 'DevOps lacks Fission')
  assert.equal(isAllowed('devops', 'Fork'), false, 'DevOps lacks Fork')

  // Manager permissions
  assert.equal(isAllowed('manager', 'Fork'), true, 'Manager has Fork')
  assert.equal(isAllowed('manager', 'Finality'), true, 'Manager has Finality')
  assert.equal(isAllowed('manager', 'Fission'), false, 'Manager lacks Fission')
  assert.equal(isAllowed('manager', 'Write'), false, 'Manager lacks Write')
  assert.equal(isAllowed('manager', 'Exec'), false, 'Manager lacks Exec')

  // Orchestrator permissions
  assert.equal(isAllowed('orchestrator', 'Fork'), true, 'Orchestrator has Fork')
  assert.equal(isAllowed('orchestrator', 'Fission'), false, 'Orchestrator lacks Fission')

  // Blogger permissions
  assert.equal(isAllowed('blogger', 'Chronicle'), true, 'Blogger has exactly Chronicle')
  assert.equal(isAllowed('blogger', 'Fork'), false, 'Blogger lacks Fork')
  assert.equal(isAllowed('blogger', 'Fission'), false, 'Blogger lacks Fission')

  // Retired & unknown roles
  assert.equal(isAllowed('unknown-role', 'Fork'), false, 'unknown role → deny')
  assert.equal(isAllowed('browser', 'Network'), false, 'retired browser → deny')
  assert.equal(isAllowed('inquiry', 'Sphinx'), false, 'retired inquiry → deny')
  assert.equal(isAllowed('coder', 'Write'), false, 'retired coder → deny')
})

test('WHAT[OFF-005] OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales', () => {
  const surfaces = {
    managerEn: read('role/manager/en.md'),
    managerZh: read('role/manager/zh-CN.md'),
    forkEn: read('tool/fork/description/en.md'),
    forkZh: read('tool/fork/description/zh-CN.md'),
  }
  for (const office of ACTIVE_OFFICES) {
    for (const key of ['managerEn', 'managerZh', 'forkEn', 'forkZh']) {
      assert.match(
        surfaces[key],
        office[key],
        `${office.id} must hit ${key} (projection drift → consequence lost)`,
      )
    }
  }
})

test('WHAT[OFF-006] OFF_006_offices_are_not_interchangeable_general_purpose_agents', () => {
  const managerEn = read('role/manager/en.md')
  const managerZh = read('role/manager/zh-CN.md')
  assert.match(managerEn, /Do not treat these offices as interchangeable/i)
  assert.match(managerEn, /Engineer is not an Operator|DevOps is not.*architect/i)
  assert.match(managerZh, /可互换|Engineer 不是.*DevOps 不是/)

  // fork must not read as "commission a witness" (delegation is by consequence).
  assert.doesNotMatch(read('tool/fork/description/en.md'), /Commission another witness/i)
})

test('WHAT[OFF-001] OFF_001_office_capability_is_consequence_not_tool_whitelist', () => {
  const managerEn = read('role/manager/en.md')
  assert.match(managerEn, /Know another office by its promises, not by its keys/i)
  assert.match(managerEn, /not by the instruments hidden[\s\S]{0,20}inside it/i)
  assert.match(managerEn, /Do not prescribe the hidden instruments of another office/i)
})

test('WHAT[OFF-015] predictor_is_internal_mechanism_role_not_forkable_or_scheduled', () => {
  const forkEn = read('tool/fork/description/en.md')
  const forkZh = read('tool/fork/description/zh-CN.md')
  assert.doesNotMatch(forkEn, /\bpredictor\b/i)
  assert.doesNotMatch(forkZh, /predictor/)
})

test('WHAT[OFF-016] OFF_016_engineer_is_the_only_office_entitled_to_fission', () => {
  assert.equal(isAllowed('engineer', 'Fission'), true)
  assert.equal(isAllowed('manager', 'Fission'), false)
  assert.equal(isAllowed('orchestrator', 'Fission'), false)
  assert.equal(isAllowed('devops', 'Fission'), false)
  assert.equal(isAllowed('blogger', 'Fission'), false)
})

test('WHAT[OFF-017] OFF_017_devops_has_inherent_mutation_authority_without_allow_repair_toggle', () => {
  assert.equal(isAllowed('devops', 'Write'), true)
  assert.equal(isAllowed('devops', 'Edit'), true)
  assert.equal(isAllowed('devops', 'Move'), true)
  assert.equal(isAllowed('devops', 'Remove'), true)
  assert.equal(isAllowed('devops', 'Exec'), true)
  assert.equal(isAllowed('devops', 'Pty'), true)
})
