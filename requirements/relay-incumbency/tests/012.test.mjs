import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[relay-incumbency-012] explicit DevOps binding replays idempotently and rejects a second identity', () => {
  const first = relay.bindRoadDevOps(relay.empty(), 'road-1', 'devops-1', 'provider/model')
  assert.equal(first.ok, true)
  const replay = relay.bindRoadDevOps(first.state, 'road-1', 'devops-1', 'provider/model')
  assert.equal(replay.ok, true)
  assert.deepEqual(relay.roadDevOps(replay.state, 'road-1'), relay.roadDevOps(first.state, 'road-1'))
  assert.deepEqual(relay.bindRoadDevOps(replay.state, 'road-1', 'devops-2', 'provider/model'), {ok: false, error: 'RoadDevOpsAlreadyBound'})
  const otherRoad = relay.bindRoadDevOps(replay.state, 'road-2', 'devops-2', 'provider/model')
  assert.equal(otherRoad.ok, true)
  assert.equal(relay.roadDevOps(otherRoad.state, 'road-1').devopsId, 'devops-1')
  assert.equal(relay.roadDevOps(otherRoad.state, 'road-2').devopsId, 'devops-2')
})

test('WHAT[relay-incumbency-012] uncertain Host acceptance and crash recovery retain one DevOps and original PromptKey', {todo: 'GAP-192: direct binding replay does not cross Host acceptance or a crash'})
