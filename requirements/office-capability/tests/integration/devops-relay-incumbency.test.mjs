import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as bindingSurface from '../../../../dist/OpenCode/Host/SessionBindingSurface.js'

test('WHAT[OFF-007] Manager road resumes fixed DevOps across relay incumbency iterations without creating substitute DevOps', async () => {
  // 1. Manager consequence: has resume for existing devops, but no Fission
  const managerPerms = office.permissions('manager')
  assert.ok(managerPerms.includes('Resume'), 'Manager must have Resume permission')
  assert.equal(managerPerms.includes('Fission'), false, 'Manager must have NO Fission')
  assert.equal(managerPerms.includes('Exec'), false, 'Manager must have NO Exec')

  // 2. Road-bound DevOps binding survives relay incumbency handoff
  const roadSessionId = 'ses-manager-road-relay'
  const devopsSessionId = 'ses-devops-road-agent'
  const initialModel = { providerID: 'host', modelID: 'devops-fixed-model' }

  // Initial road binding
  bindingSurface.bind(roadSessionId, devopsSessionId, 'devops')
  bindingSurface.bindDevOpsModel(devopsSessionId, initialModel)

  assert.equal(bindingSurface.tryParent(devopsSessionId), roadSessionId)
  assert.equal(bindingSurface.tryAgent(devopsSessionId), 'devops')

  // Relay incumbency handoff: Manager iteration 1 retires (Continue), Manager iteration 2 takes over
  // Road-level DevOps binding remains unchanged and identical
  assert.equal(bindingSurface.tryParent(devopsSessionId), roadSessionId)
  assert.equal(bindingSurface.tryAgent(devopsSessionId), 'devops')

  // Model binding remains locked across iterations
  bindingSurface.verifyDevOpsModel(devopsSessionId, initialModel)

  // Attempting to substitute or drift model across iterations is rejected fail-closed
  assert.throws(
    () => {
      bindingSurface.verifyDevOpsModel(devopsSessionId, { providerID: 'host', modelID: 'deviated-model' })
    },
    /CRASH-020/,
    'DevOps model drift across relay iterations must fail-closed',
  )
})
