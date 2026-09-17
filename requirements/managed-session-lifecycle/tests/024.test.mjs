import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as RelaySurface from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[MANAGED-SESSION-024] fixed DevOps crash recovery maintains single logical authority and locks bound model', () => {
  const all = RolesSurface.allRoleLabels
  assert.ok(all.includes('engineer'), 'Role labels must contain engineer')
  assert.ok(all.includes('devops'), 'Role labels must contain devops')
  assert.equal(all.includes('coder'), false, 'Role labels must not contain coder')
  assert.equal(all.includes('inspector'), false, 'Role labels must not contain inspector')
  assert.equal(all.includes('browser'), false, 'Role labels must not contain browser')
  assert.equal(all.includes('inquiry'), false, 'Role labels must not contain inquiry')
  assert.equal(all.includes('distiller'), false, 'Role labels must not contain distiller')

  // 1. Single logical authority on DevOps crash recovery (ReplacePhysicalSession)
  const initial = HandleSurface.emptyState()
  const devopsHandle = HandleSurface.handleIdAgent('devops')

  // Initial link: binds physical session A to devops logical operator
  const linkedA = HandleSurface.apply(initial, {
    op: 'link',
    handle: devopsHandle,
    child: 'ses_devops_physical_A',
    agent: 'devops',
    role: 'DevOps',
  })
  assert.equal(linkedA.ok, true)

  // Cannot bind a second physical session to the same handle while active (maintains single authority)
  const duplicateActive = HandleSurface.apply(linkedA.state, {
    op: 'link',
    handle: devopsHandle,
    child: 'ses_devops_physical_B',
    agent: 'devops',
    role: 'DevOps',
  })
  assert.equal(duplicateActive.ok, false, 'Cannot have dual active physical executions under devops authority')

  // Crash recovery: complete then retire old handle (retire is join's write, Active cannot retire per EXEC-004)
  const completedA = HandleSurface.apply(linkedA.state, {
    op: 'complete',
    handle: devopsHandle,
    kind: 'Terminal',
  })
  assert.equal(completedA.ok, true)
  const retiredA = HandleSurface.apply(completedA.state, {
    op: 'retire',
    handle: devopsHandle,
  })
  assert.equal(retiredA.ok, true, 'Old crashed physical handle must be retired')

  const replacedB = HandleSurface.apply(retiredA.state, {
    op: 'link',
    handle: devopsHandle,
    child: 'ses_devops_physical_B',
    agent: 'devops',
    role: 'DevOps',
  })
  assert.equal(replacedB.ok, true, 'Replacement physical session B atomically takes over devops authority')

  // 2. Bound ModelTarget persistence across road DevOps binding
  const roadState = RelaySurface.openRoad('road_alpha', 'rev_1', 'msg_1', 'inc_1', 'snap_1')
  const boundRoad = RelaySurface.bindRoadDevOps(
    roadState,
    'road_alpha',
    'devops:road_alpha',
    'provider/fixed-model:high',
  )
  assert.equal(boundRoad.ok, true)
  const devopsView = RelaySurface.roadDevOps(boundRoad.state, 'road_alpha')
  assert.equal(devopsView.devopsId, 'devops:road_alpha')
  assert.equal(devopsView.modelTarget, 'provider/fixed-model:high', 'Road DevOps binding must persist immutable modelTarget')
})
