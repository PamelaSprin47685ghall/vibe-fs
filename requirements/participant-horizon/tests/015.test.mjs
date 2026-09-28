import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[participant-horizon-015] Manager capability projection denies Fission while Engineer retains it', () => {
  assert.equal(isAllowed('manager', 'Fission'), false)
  assert.equal(isAllowed('engineer', 'Fission'), true)
})

test.todo('WHAT[participant-horizon-015] actual multiple-Engineer roster preserves each byname without Manager replicas or shared-DevOps fiction; two chooseRoad objects do not prove a roster (GAP-079)')
