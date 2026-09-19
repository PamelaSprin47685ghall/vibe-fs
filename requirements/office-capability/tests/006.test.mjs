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

const ACTIVE_OFFICES = [
  {
    id: 'engineer-investigation-mutation',
    managerEn: /entrust.*Engineer/i,
    managerZh: /托付.*Engineer/,
    forkEn: /Engineer[\s\S]{0,160}local facts[\s\S]{0,80}source/i,
    forkZh: /Engineer[\s\S]{0,120}本地事实[\s\S]{0,80}源码/,
    lawEn: /local facts|changing the written world|implement.*refactor/i,
    lawZh: /本地事实|书写出来的世界|源码/,
  },
]

test('WHAT[office-capability-006] OFF_006_offices_are_not_interchangeable_general_purpose_agents', () => {
  const managerEn = read('role/manager/en.md')
  const managerZh = read('role/manager/zh-CN.md')
  assert.match(managerEn, /Do not treat these offices as interchangeable/i)
  assert.match(managerEn, /Engineer is not an Operator|DevOps is not.*architect/i)
  assert.match(managerZh, /可互换|Engineer 不是.*DevOps 不是/)

  // fork must not read as "commission a witness" (delegation is by consequence).
  assert.doesNotMatch(read('tool/fork/description/en.md'), /Commission another witness/i)
})

import { readdirSync } from 'node:fs'
import { CASES } from './eval/provider-office-boundary/corpus.mjs'
import { ORACLES, evaluateCase } from './eval/provider-office-boundary/oracles.mjs'

const EVAL_DIR = join(dirname(fileURLToPath(import.meta.url)), 'eval/provider-office-boundary')

/** Shared case shape + oracle red/green assertion. */
const assertCaseShape = (c) => {
  assert.equal(typeof c.id, 'string', c.id)
  assert.equal(typeof c.setup, 'string', c.id)
  assert.equal(typeof c.pass_if, 'string', c.id)
  assert.ok(c.pass_example?.role && Array.isArray(c.pass_example.toolCalls), `${c.id} pass_example`)
  assert.ok(c.fail_example?.role && Array.isArray(c.fail_example.toolCalls), `${c.id} fail_example`)
  assert.equal(typeof ORACLES[c.id], 'function', `${c.id} oracle`)
  const pass = evaluateCase(c, c.pass_example)
  const fail = evaluateCase(c, c.fail_example)
  assert.equal(pass.ok, true, `${c.id} pass_example: ${pass.reason ?? ''}`)
  assert.equal(fail.ok, false, `${c.id} fail_example must be rejected`)
}

test('WHAT[office-capability-006] office_boundary_eval_corpus_has_id_setup_oracles_and_synthetic_traces', () => {
  assert.equal(CASES.length, 4)
  assert.deepEqual(
    CASES.map((c) => c.id).sort(),
    [
      'devops-does-not-choose-among-valid-behaviors',
      'devops-inherent-repair',
      'engineer-local-investigation-and-mutation',
      'manager-mixed-mission',
    ],
  )
})

test('WHAT[office-capability-006] office_boundary_eval_engineer_local_investigation_case_is_red_and_green', () => {
  const c = CASES.find((x) => x.id === 'engineer-local-investigation-and-mutation')
  assertCaseShape(c)
})

test('WHAT[office-capability-006] office_boundary_eval_devops_inherent_repair_case_is_red_and_green', () => {
  const c = CASES.find((x) => x.id === 'devops-inherent-repair')
  assertCaseShape(c)
})

test('WHAT[office-capability-006] office_boundary_eval_devops_does_not_choose_case_is_red_and_green', () => {
  const c = CASES.find((x) => x.id === 'devops-does-not-choose-among-valid-behaviors')
  assertCaseShape(c)
})

test('WHAT[office-capability-006] office_boundary_eval_manager_mixed_mission_case_is_red_and_green', () => {
  const c = CASES.find((x) => x.id === 'manager-mixed-mission')
  assertCaseShape(c)
})

test('WHAT[office-capability-006] office_boundary_eval_oracles_are_not_wired_into_production_tools', () => {
  const sources = [
    readFileSync(join(EVAL_DIR, 'oracles.mjs'), 'utf8'),
    readFileSync(join(EVAL_DIR, 'corpus.mjs'), 'utf8'),
  ]
  for (const text of sources) {
    assert.doesNotMatch(text, /src\/Wanxiangshu|Infrastructure\/OpenCode\/Tools/)
  }

  const dirs = [
    join(ROOT, 'src/Wanxiangshu/OpenCode/Tools'),
    join(ROOT, 'src/Wanxiangshu/OpenCode/Tools'),
  ]
  for (const dir of dirs) {
    for (const name of readdirSync(dir)) {
      if (!name.endsWith('.fs')) continue
      const text = readFileSync(join(dir, name), 'utf8')
      assert.doesNotMatch(text, /engineer-local-investigation-and-mutation/, name)
    }
  }
})
