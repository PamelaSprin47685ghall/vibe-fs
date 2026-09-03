/**
 * office-capability — package-owned live-repo canary (ARCH-017 / Gate F).
 *
 * The five forkable offices are the canonical 五分法; their entitled
 * consequence must hit every decision surface: Manager Role Law (worldview),
 * fork description (call-time choice), and each office's own Role Law
 * (self-model). Projection wording may differ; the consequence must not.
 *
 * The anchor regexes mirror the ids in scripts/checks/semantic-anchors.mjs
 * OFFICE_CAPABILITY_ANCHORS / OFFICE_CAPABILITY_NEGATIVES, which are declared
 * owned by office-capability (see HOW.md anchor list). The catalog itself is
 * exercised by the shared Gate F fixture tests (language-parity-gate.test.mjs);
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

// ── ARCH-017 five-office consequence catalog (ids mirror OFFICE_CAPABILITY_ANCHORS) ──
const FIVE_OFFICES = [
  {
    id: 'coder-mutation',
    managerEn: /entrust mutation to a Coder/i,
    managerZh: /把 mutation 托付给 Coder/,
    forkEn: /Coder \/ Engineer[\s\S]{0,120}Changes repository source/,
    forkZh: /Coder \/ Engineer[\s\S]{0,80}改变 repository source/,
    lawEn: /changing the written world/,
    lawZh: /书写出来的世界/,
  },
  {
    id: 'inspector-existing-facts',
    managerEn: /entrust an Inspector/i,
    managerZh: /托付 Inspector/,
    forkEn: /Scout \/ Investigator[\s\S]{0,160}already exist in the repository/,
    forkZh: /Scout \/ Investigator[\s\S]{0,80}已经存在的事实/,
    lawEn: /establish facts that already exist in the repository/,
    lawZh: /repository 中已经存在的事实/,
  },
  {
    id: 'devops-execution',
    managerEn: /entrust DevOps/i,
    managerZh: /托付 DevOps/,
    forkEn: /Technician \/ Operator[\s\S]{0,160}running world/,
    forkZh: /Technician \/ Operator[\s\S]{0,80}运行中的世界/,
    lawEn: /operational objective[\s\S]{0,60}honest[\s\S]{0,3}closure/,
    lawZh: /operational objective/,
  },
  {
    id: 'browser-external-provenance',
    managerEn: /entrust a Browser/i,
    managerZh: /托付 Browser/,
    forkEn: /Navigator \/ Researcher[\s\S]{0,160}external world with provenance/,
    forkZh: /Navigator \/ Researcher[\s\S]{0,80}外部世界的事实/,
    lawEn: /establish facts from the Internet and[\s\S]{0,60}other external web sources/,
    lawZh: /从 Internet 与其他外部 web sources 建立事实/,
  },
  {
    id: 'inquiry-reasoning',
    managerEn: /entrust Inquiry/i,
    managerZh: /托付 Inquiry/,
    forkEn: /Analyst \/ Inquirer[\s\S]{0,160}not yet clear/,
    forkZh: /Analyst \/ Inquirer[\s\S]{0,80}尚无明确答案/,
    lawEn: /semantic intelligence/,
    lawZh: /语义智能|semantic intelligence/,
  },
]

test('WHAT[OFF-002] OFF_002_managed_catalog_forkable_offices_are_exactly_the_five_canonical_offices', () => {
  assert.deepEqual(managerForkableOffices(), ['Coder', 'Inspector', 'DevOps', 'Browser', 'Inquiry'])
})

test('WHAT[ENF-002] office_permission_surface_matches_the_canonical_roles_matrix', () => {
  const matrix = [
    ['manager', ['Finality', 'Fission', 'Fork', 'Horizon', 'Join', 'TodoWrite']],
    ['orchestrator', ['Fork', 'Horizon', 'Join']],
    ['coder', ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Inspect', 'Move', 'Read', 'Remove', 'Write']],
    ['inspector', ['Exec', 'Fetch', 'Fission', 'Glob', 'Grep', 'Read']],
    ['browser', ['Fission', 'Glob', 'Grep', 'Network', 'Read']],
    ['inquiry', ['Fission', 'Inspect', 'Sphinx']],
    ['reviewer', ['Glob', 'Grep', 'Judge', 'Read']],
    ['devops', ['Behavior', 'Exec', 'Glob', 'Grep', 'Horizon', 'Inspect', 'Join', 'Pty', 'Read']],
    ['distiller', []],
    ['blogger', ['Chronicle']],
  ]

  for (const [role, expected] of matrix) {
    assertJsData(permissions(role), `permissions(${role})`)
    assert.deepEqual(permissions(role), expected, `permissions(${role}) must equal the canonical matrix`)
  }

  assert.deepEqual(permissions('not-a-role'), [], 'unknown role fails closed to empty set')
})

test('WHAT[ENF-002] office_permission_surface_denies_outside_the_matrix', () => {
  assert.equal(isAllowed('inquiry', 'Inspect'), true)
  assert.equal(isAllowed('inquiry', 'Sphinx'), true)
  assert.equal(isAllowed('inquiry', 'Fission'), true)
  assert.equal(isAllowed('inquiry', 'Read'), false, 'Inquiry lacks Read')
  assert.equal(isAllowed('blogger', 'Chronicle'), true, 'Blogger has exactly Chronicle')
  assert.equal(isAllowed('blogger', 'Fork'), false, 'Blogger lacks Fork')
  assert.equal(isAllowed('manager', 'Finality'), true, 'Manager has Finality')
  assert.equal(isAllowed('unknown-role', 'Fork'), false, 'unknown role → deny')
  assert.equal(isAllowed('manager', 'UnknownPermission'), false, 'unknown permission → deny')
})

test('WHAT[OFF-005] OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales', () => {
  const surfaces = {
    managerEn: read('role/manager/en.md'),
    managerZh: read('role/manager/zh-CN.md'),
    forkEn: read('tool/fork/description/en.md'),
    forkZh: read('tool/fork/description/zh-CN.md'),
  }
  for (const office of FIVE_OFFICES) {
    for (const key of ['managerEn', 'managerZh', 'forkEn', 'forkZh']) {
      assert.match(
        surfaces[key],
        office[key],
        `${office.id} must hit ${key} (projection drift → consequence lost)`,
      )
    }
  }
})

test('WHAT[OFF-002] OFF_002_each_office_role_law_carries_its_entitled_consequence', () => {
  for (const office of FIVE_OFFICES) {
    assert.match(read(`role/${office.id.split('-')[0]}/en.md`), office.lawEn, `${office.id} law en`)
    assert.match(read(`role/${office.id.split('-')[0]}/zh-CN.md`), office.lawZh, `${office.id} law zh`)
  }
})

test('WHAT[OFF-006] OFF_006_offices_are_not_interchangeable_general_purpose_agents', () => {
  const managerEn = read('role/manager/en.md')
  const managerZh = read('role/manager/zh-CN.md')
  assert.match(managerEn, /Do not treat these offices as interchangeable/i)
  assert.match(managerEn, /A Coder is not an Operator/i)
  assert.match(managerZh, /可互换|碰巧没有 shell/)
  assert.match(managerZh, /Coder 不是碰巧没有 shell 的 Operator/)

  // fork must not read as "commission a witness" (delegation is by consequence).
  assert.doesNotMatch(read('tool/fork/description/en.md'), /Commission another witness/i)
})

test('WHAT[OFF-003] OFF_003_two_calling_names_differ_in_persona_and_depth_not_authority', () => {
  assert.match(
    read('tool/fork/description/en.md'),
    /differ in persona and reasoning depth,\s*not in the office's authority/i,
  )
  assert.match(
    read('tool/fork/description/zh-CN.md'),
    /区别在 persona 与 reasoning depth，不改变该 Office 的 authority/,
  )
})

test('WHAT[OFF-001] OFF_001_office_capability_is_consequence_not_tool_whitelist', () => {
  // The manager law names offices by what they can establish or change,
  // not by their instruments (ARCH-017: "not a list of names").
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
