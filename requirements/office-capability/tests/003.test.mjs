import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[OFF-003] office authority is immutable and invariant across persona tiers and composite names', () => {
  // 1. Immutable authority per canonical role: Engineer permissions are exact and invariant
  const engineerPermissions = office.permissions('engineer')
  assert.deepEqual(
    [...engineerPermissions].sort(),
    ['BashHoneypot', 'Edit', 'Fetch', 'Fission', 'Glob', 'Grep', 'Move', 'Read', 'Remove', 'Write'].sort()
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
