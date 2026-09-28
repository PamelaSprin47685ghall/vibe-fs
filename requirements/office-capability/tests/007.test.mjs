import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed, managerForkableOffices } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as binding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'

test('WHAT[office-capability-007] Manager entitlement includes review reading and delegation but excludes mutation execution and fission', () => {
  assert.deepEqual(managerForkableOffices(), ['engineer'])
  for (const permission of ['Resume', 'Read', 'Glob', 'Grep']) {
    assert.equal(isAllowed('manager', permission), true, permission)
  }
  for (const permission of ['Write', 'Edit', 'Move', 'Remove', 'Exec', 'Pty', 'Fission']) {
    assert.equal(isAllowed('manager', permission), false, permission)
  }
})

test('WHAT[office-capability-007] an existing DevOps model binding accepts the same model and rejects drift', () => {
  const road = 'office-road'
  const devops = 'office-devops'
  const model = { providerID: 'host', modelID: 'fixed-model' }
  try {
    binding.bind(road, devops, 'devops')
    binding.bindDevOpsModel(devops, model)
    binding.verifyDevOpsModel(devops, model)
    assert.throws(() => binding.verifyDevOpsModel(devops, { ...model, modelID: 'other' }), /CRASH-020/)
    assert.equal(binding.tryParent(devops), road)
    assert.equal(binding.tryAgent(devops), 'devops')
    binding.verifyDevOpsModel(devops, model)
  } finally {
    binding.drop(devops)
    binding.drop(road)
  }
})

test.todo('WHAT[office-capability-007] actual Manager review reading and native-tool refusal require the capability-enforcement-025 runtime entry; entitlement alone does not prove the current window')
