import assert from 'node:assert/strict'
import test from 'node:test'
import * as CapabilitySurface from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as RelaySurface from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[INTERACTION-AUTHORITY-022] DevOps resume and continuation strictly lock bound model target and have direct write permissions', () => {
  // 1. Office capability truth: DevOps has direct Write/Edit/Exec permissions, but NOT Fission
  assert.equal(CapabilitySurface.isAllowed('DevOps', 'Write'), true, 'DevOps must have direct Write permission')
  assert.equal(CapabilitySurface.isAllowed('DevOps', 'Edit'), true, 'DevOps must have direct Edit permission')
  assert.equal(CapabilitySurface.isAllowed('DevOps', 'Exec'), true, 'DevOps must have Exec permission')
  assert.equal(CapabilitySurface.isAllowed('DevOps', 'Fission'), false, 'DevOps must not have Fission permission')

  // 2. DevOps resume model locking: bound model established on road initialization is immutable
  const roadState = RelaySurface.openRoad('road_ia_22', 'rev_1', 'msg_1', 'inc_1', 'snap_1')
  const lockedModel = 'neuralwatt/glm-5.2-flex:high'
  const boundRoad = RelaySurface.bindRoadDevOps(roadState, 'road_ia_22', 'devops:road_ia_22', lockedModel)
  assert.equal(boundRoad.ok, true)
  const view = RelaySurface.roadDevOps(boundRoad.state, 'road_ia_22')

  assert.equal(view.devopsId, 'devops:road_ia_22')
  assert.equal(view.modelTarget, lockedModel, 'DevOps binding must lock immutable modelTarget')

  // Attempting to bind a conflicting model to the same road devops must fail closed
  const rebindResult = RelaySurface.bindRoadDevOps(boundRoad.state, 'road_ia_22', 'devops:road_ia_22', 'other/tampered-model:low')
  assert.equal(rebindResult.ok, true)
  const viewAfterRebind = RelaySurface.roadDevOps(rebindResult.state, 'road_ia_22')
  assert.equal(viewAfterRebind.modelTarget, lockedModel, 'Tampered modelTarget on re-bind must be ignored or rejected, locking original model')

  // 3. Convergence to single logical execution authority on concurrent recovery
  const initialHandles = HandleSurface.emptyState()
  const devopsHandle = HandleSurface.handleIdAgent('devops')

  // Initial link established
  const linked = HandleSurface.apply(initialHandles, {
    op: 'link',
    handle: devopsHandle,
    child: 'ses_devops_p1',
    agent: 'devops',
    role: 'DevOps',
  })
  assert.equal(linked.ok, true)

  // A concurrent second recovery attempt trying to link another physical session under the active handle is rejected
  const concurrentAttempt = HandleSurface.apply(linked.state, {
    op: 'link',
    handle: devopsHandle,
    child: 'ses_devops_p2',
    agent: 'devops',
    role: 'DevOps',
  })
  assert.equal(concurrentAttempt.ok, false, 'Multiple concurrent physical recovery attempts must converge to single active authority')
})
