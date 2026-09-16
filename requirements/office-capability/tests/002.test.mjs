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
const read = (rel) => readFileSync(join(ROOT, 'resources/provider', rel), 'utf8')

const FIVE_OFFICES = [
  { id: 'coder-mutation', lawEn: /changing the written world/, lawZh: /书写出来的世界/ },
  { id: 'inspector-existing-facts', lawEn: /establish facts that already exist in the repository/, lawZh: /repository 中已经存在的事实/ },
  { id: 'devops-execution', lawEn: /operational objective[\s\S]{0,60}honest[\s\S]{0,3}closure/, lawZh: /operational objective/ },
  { id: 'browser-external-provenance', lawEn: /establish facts from the Internet and[\s\S]{0,60}other external web sources/, lawZh: /从 Internet 与其他外部 web sources 建立事实/ },
  { id: 'inquiry-reasoning', lawEn: /semantic intelligence/, lawZh: /语义智能|semantic intelligence/ },
]

test('WHAT[OFF-002] OFF_002_managed_catalog_forkable_offices_are_exactly_the_five_canonical_offices', () => {
  assert.deepEqual(managerForkableOffices(), ['Coder', 'Inspector', 'DevOps', 'Browser', 'Inquiry'])
})

test('WHAT[ENF-002] office_permission_surface_matches_the_canonical_roles_matrix', () => {
  const matrix = [
    ['manager', ['Finality', 'Fission', 'Fork', 'Horizon', 'Join', 'ReviewAssessment', 'TodoWrite']],
    ['orchestrator', ['Fork', 'Horizon', 'Join']],
    ['coder', ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Inspect', 'Move', 'Read', 'Remove', 'Write']],
    ['inspector', ['Fetch', 'Fission', 'Glob', 'Grep', 'Read']],
    ['browser', ['Fission', 'Glob', 'Grep', 'Network', 'Read']],
    ['inquiry', ['Fission', 'Inspect', 'Sphinx']],
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

test('WHAT[OFF-002] OFF_002_each_office_role_law_carries_its_entitled_consequence', () => {
  for (const office of FIVE_OFFICES) {
    assert.match(read(`role/${office.id.split('-')[0]}/en.md`), office.lawEn, `${office.id} law en`)
    assert.match(read(`role/${office.id.split('-')[0]}/zh-CN.md`), office.lawZh, `${office.id} law zh`)
  }
})
