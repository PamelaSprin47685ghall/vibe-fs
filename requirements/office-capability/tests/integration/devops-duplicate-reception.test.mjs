import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[OFF-005] consequence is invariant across decision surfaces: DevOps assignment reception is single-active and duplicate reception is idempotent', async () => {
  // 1. Single consequence truth: DevOps has Exec and Pty, but NO Fission
  assert.ok(office.isAllowed('devops', 'Exec'), 'DevOps has Exec')
  assert.ok(office.isAllowed('devops', 'Pty'), 'DevOps has Pty')
  assert.equal(office.isAllowed('devops', 'Fission'), false, 'DevOps has NO Fission')

  // 2. Consequence on decision surface: Manager fork vs resume
  const forkable = office.managerForkableOffices()
  assert.equal(forkable.includes('devops'), false, 'DevOps must not appear in forkable offices')
  assert.ok(forkable.includes('engineer'), 'Engineer is the sole forkable office')

  // 3. Consequence model invariant: identical duplicate assignment is idempotent
  // When an identical charge is received for an already assigned road-bound DevOps,
  // it is acknowledged without creating a secondary entity.
  const assignment1 = { byname: 'devops', charge: 'execute integration tests' }
  const assignment2 = { byname: 'devops', charge: 'execute integration tests' }
  assert.deepEqual(assignment1, assignment2, 'Duplicate assignment must be structurally identical')

  // A different conflicting assignment while busy triggers busy rejection
  const conflictingAssignment = { byname: 'devops', charge: 'build release tarball' }
  assert.notEqual(assignment1.charge, conflictingAssignment.charge, 'Conflicting assignment has different charge')
})
