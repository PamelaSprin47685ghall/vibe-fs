import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

test('WHAT[office-capability-003] office authority is immutable and invariant across persona tiers and composite names', () => {
  // 1. Immutable authority per canonical role: Engineer permissions are exact and invariant
  const engineerPermissions = office.permissions('engineer')
  assert.deepEqual(
    [...engineerPermissions].sort(),
    ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Move', 'Read', 'Remove', 'Sphinx', 'Write'].sort()
  )

  // 2. Invariance across tiers: Calling with pseudo-tier composite names does not widen authority or create distinct roles
  assert.equal(office.isAllowed('fast-engineer', 'Read'), false)
  assert.equal(office.isAllowed('deep-engineer', 'Read'), false)
  assert.equal(office.isAllowed('engineer-devops', 'Exec'), false)

  // 3. Exact 1:1 role-to-persona mapping: Manager can only fork Engineer, no tier-split roles exist
  const forkable = office.managerForkableOffices()
  assert.deepEqual(forkable, ['engineer'])

  // 4. DevOps has explicit fixed authority, neither fast nor deep tiers alter its capability matrix
  const devopsPermissions = office.permissions('devops')
  assert.ok(devopsPermissions.includes('Exec'))
  assert.ok(devopsPermissions.includes('Pty'))
  assert.equal(devopsPermissions.includes('Fission'), false)
  assert.equal(office.isAllowed('fast-devops', 'Exec'), false)
})

test('WHAT[office-capability-003] office_permission_surface_matches_the_canonical_roles_matrix', () => {
  const matrix = [
    ['manager', ['Finality', 'Fork', 'Horizon', 'Join', 'Resume', 'ReviewAssessment', 'Sphinx']],
    ['orchestrator', ['Fork', 'Horizon', 'Join', 'Sphinx']],
    ['engineer', ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Move', 'Read', 'Remove', 'Sphinx', 'Write']],
    ['devops', ['Edit', 'Exec', 'Glob', 'Grep', 'Horizon', 'Join', 'Move', 'Pty', 'Read', 'Remove', 'Write']],
    ['blogger', ['Chronicle']],
  ]

  for (const [role, expected] of matrix) {
    assertJsData(office.permissions(role), `permissions(${role})`)
    assert.deepEqual(office.permissions(role), expected, `permissions(${role}) must equal the canonical matrix`)
  }

  assert.deepEqual(office.permissions('not-a-role'), [], 'unknown role fails closed to empty set')
  assert.deepEqual(office.permissions('coder'), [], 'retired role fails closed to empty set')
  assert.deepEqual(office.permissions('inspector'), [], 'retired role fails closed to empty set')
  assert.deepEqual(office.permissions('browser'), [], 'retired role fails closed to empty set')
  assert.deepEqual(office.permissions('inquiry'), [], 'retired role fails closed to empty set')
  assert.deepEqual(office.permissions('distiller'), [], 'retired role fails closed to empty set')
})

test('WHAT[office-capability-003] office_permission_surface_denies_outside_the_matrix', () => {
  // Engineer permissions
  assert.equal(office.isAllowed('engineer', 'Fission'), true, 'Engineer has Fission')
  assert.equal(office.isAllowed('engineer', 'Read'), true, 'Engineer has Read')
  assert.equal(office.isAllowed('engineer', 'Write'), true, 'Engineer has Write')
  assert.equal(office.isAllowed('engineer', 'Exec'), false, 'Engineer lacks Exec')
  assert.equal(office.isAllowed('engineer', 'Pty'), false, 'Engineer lacks Pty')

  // DevOps permissions
  assert.equal(office.isAllowed('devops', 'Exec'), true, 'DevOps has Exec')
  assert.equal(office.isAllowed('devops', 'Pty'), true, 'DevOps has Pty')
  assert.equal(office.isAllowed('devops', 'Write'), true, 'DevOps has Write')
  assert.equal(office.isAllowed('devops', 'Edit'), true, 'DevOps has Edit')
  assert.equal(office.isAllowed('devops', 'Fission'), false, 'DevOps lacks Fission')
  assert.equal(office.isAllowed('devops', 'Fork'), false, 'DevOps lacks Fork')

  // Manager permissions
  assert.equal(office.isAllowed('manager', 'Fork'), true, 'Manager has Fork')
  assert.equal(office.isAllowed('manager', 'Finality'), true, 'Manager has Finality')
  assert.equal(office.isAllowed('manager', 'Fission'), false, 'Manager lacks Fission')
  assert.equal(office.isAllowed('manager', 'Write'), false, 'Manager lacks Write')
  assert.equal(office.isAllowed('manager', 'Exec'), false, 'Manager lacks Exec')

  // Orchestrator permissions
  assert.equal(office.isAllowed('orchestrator', 'Fork'), true, 'Orchestrator has Fork')
  assert.equal(office.isAllowed('orchestrator', 'Fission'), false, 'Orchestrator lacks Fission')

  // Blogger permissions
  assert.equal(office.isAllowed('blogger', 'Chronicle'), true, 'Blogger has exactly Chronicle')
  assert.equal(office.isAllowed('blogger', 'Fork'), false, 'Blogger lacks Fork')
  assert.equal(office.isAllowed('blogger', 'Fission'), false, 'Blogger lacks Fission')

  // Retired & unknown roles
  assert.equal(office.isAllowed('unknown-role', 'Fork'), false, 'unknown role → deny')
  assert.equal(office.isAllowed('browser', 'Network'), false, 'retired browser → deny')
  assert.equal(office.isAllowed('inquiry', 'Sphinx'), false, 'retired inquiry → deny')
  assert.equal(office.isAllowed('coder', 'Write'), false, 'retired coder → deny')
})
