import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[relay-incumbency-010] an absent Road has no DevOps binding', () => {
  assert.equal(relay.roadDevOps(relay.empty(), 'missing-road'), null)
})

test('WHAT[relay-incumbency-010] Road fold reports an explicit DevOps binding and current incumbent', () => {
  const first = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1')
  assert.equal(first.ok, true)
  const bound = relay.bindRoadDevOps(first.state, 'road-1', 'devops-session-1', 'provider/model')
  assert.equal(bound.ok, true)
  assert.deepEqual(relay.roadDevOps(bound.state, 'road-1'), {
    devopsId: 'devops-session-1', incumbentId: 'inc-1', modelTarget: 'provider/model',
  })
})

test('WHAT[relay-incumbency-010] actual retired and current Managers receive exclusive DevOps invocation authority', {todo: 'GAP-192: Road projection does not exercise resume, join or horizon authorization'})
