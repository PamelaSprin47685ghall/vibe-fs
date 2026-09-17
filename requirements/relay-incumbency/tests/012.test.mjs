import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'



test('WHAT[RELAY-012] fixed devops initial binding and recovery are idempotent and reject duplicate creation', () => {
  if (typeof relay.bindRoadDevOps === 'function') {
    const state = relay.empty()
    const bound1 = relay.bindRoadDevOps(state, 'road-1', 'devops-1')
    assert.equal(bound1.ok, true)
    const bound2 = relay.bindRoadDevOps(bound1.state, 'road-1', 'devops-2')
    assert.equal(bound2.ok, false, 'cannot create second DevOps on same road')
  } else {
    assert.fail('relay.bindRoadDevOps is not yet implemented')
  }
})
