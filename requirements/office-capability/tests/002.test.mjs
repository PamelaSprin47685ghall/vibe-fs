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

test('WHAT[ENF-002] office_permission_surface_matches_the_canonical_roles_matrix', () => {
  const matrix = [
    ['manager', ['Finality', 'Fork', 'Horizon', 'Join', 'Resume', 'ReviewAssessment', 'TodoWrite']],
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
