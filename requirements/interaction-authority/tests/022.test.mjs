import assert from 'node:assert/strict'
import test from 'node:test'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[interaction-authority-022] actual road decision preserves an established DevOps model on a conflicting rebind', () => {
  const opened = relay.openRoad('road-fixed', 'revision', 'message', 'incumbent', 'snapshot')
  const bound = relay.bindRoadDevOps(opened, 'road-fixed', 'devops:road-fixed', 'provider/fixed-model:high')
  assert.equal(bound.ok, true)
  const original = relay.roadDevOps(bound.state, 'road-fixed')
  assert.equal(original.devopsId, 'devops:road-fixed')
  assert.equal(original.modelTarget, 'provider/fixed-model:high')
  const repeated = relay.bindRoadDevOps(bound.state, 'road-fixed', 'devops:road-fixed', 'provider/other-model:low')
  assert.equal(repeated.ok, true)
  assert.deepEqual(relay.roadDevOps(repeated.state, 'road-fixed'), original)
})

test('WHAT[interaction-authority-022] pure handle transition rejects a second child on the occupied DevOps handle', () => {
  const handle = handles.handleIdAgent('devops')
  const first = handles.apply(handles.emptyState(), { op: 'link', handle, child: 'devops-child-1', agent: 'devops', role: 'DevOps' })
  assert.equal(first.ok, true)
  const second = handles.apply(first.state, { op: 'link', handle, child: 'devops-child-2', agent: 'devops', role: 'DevOps' })
  assert.equal(second.ok, false)
})

test.todo('WHAT[interaction-authority-022] GAP-122 actual DevOps resume and crash recovery preserve model Persona and one logical authority through concurrent physical attempts')
