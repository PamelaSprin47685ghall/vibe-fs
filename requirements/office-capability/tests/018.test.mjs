import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[office-capability-018] sphinx is programmatic workflow using standard Engineer without its own role persona or fission identity', () => {
  // 1. Non-consequence: Sphinx is not a forkable office/persona
  const forkable = office.managerForkableOffices()
  assert.equal(forkable.includes('sphinx'), false)

  // 2. Non-consequence: Sphinx does not own independent Fission identity
  assert.equal(office.isAllowed('sphinx', 'Fission'), false)

  // 3. Non-consequence: Sphinx is not a mutable workspace worker role
  assert.equal(office.isAllowed('sphinx', 'Write'), false)
  assert.equal(office.isAllowed('sphinx', 'Exec'), false)

  // 4. Decommissioned Inquiry role: inquiry model driving layer is completely removed
  assert.equal(office.isAllowed('inquiry', 'Read'), false)
  assert.equal(office.isAllowed('inquiry', 'Write'), false)

  // 5. Exclusive Fission entitlement: Engineer is the sole office entitled to Fission
  assert.equal(office.isAllowed('engineer', 'Fission'), true)
  assert.equal(office.isAllowed('engineer', 'Write'), true)
  assert.equal(office.isAllowed('engineer', 'Edit'), true)
  assert.equal(office.isAllowed('engineer', 'Sphinx'), true)
  assert.equal(office.isAllowed('manager', 'Fission'), false)
  assert.equal(office.isAllowed('devops', 'Fission'), false)
})
