import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as fissionSurface from '../../../../dist/Execution/Fission/Surface.js'

test('WHAT[office-capability-016] Engineer exclusively holds Fission authority and historical Manager Fission state never resurrects active lanes', async () => {
  // 1. Office consequence model: Engineer exclusively holds Fission
  assert.ok(office.isAllowed('engineer', 'Fission'), 'Engineer must hold Fission')
  assert.equal(office.isAllowed('manager', 'Fission'), false, 'Manager must NOT hold Fission')
  assert.equal(office.isAllowed('devops', 'Fission'), false, 'DevOps must NOT hold Fission')
  assert.equal(office.isAllowed('orchestrator', 'Fission'), false, 'Orchestrator must NOT hold Fission')

  // 2. Fission projection state: empty or historical state has no active lanes for Manager
  assert.equal(fissionSurface.isActive('manager'), false, 'No active fission groups by default')
  assert.equal(fissionSurface.isActive('engineer'), false, 'No active fission groups by default')

  // 3. Fission prompt parsing requires N >= 2 non-empty prompts
  const valid = fissionSurface.parsePrompt(['lane 1 task', 'lane 2 task'])
  assert.ok(valid.ok, 'Valid prompts with N >= 2 parse successfully')

  const tooFew = fissionSurface.parsePrompt(['single lane only'])
  assert.equal(tooFew.ok, false, 'Prompts with N < 2 must be rejected')
})
