// Abandoned ordering and completion classification through owner surfaces.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

test('WHAT[DELEG-015] JOIN_V2_abandoned_order_is_stable_after_completed_items', () => {
  const wire = join.renderBatch('english', [
    { kind: 'completed', agentId: 'a1', agentName: 'first', workRecord: 'one' },
    { kind: 'abandoned', agentId: 'a2', agentName: 'second', reason: 'gone' },
  ])
  assert.ok(wire.indexOf('first') < wire.indexOf('second'))
  assert.doesNotMatch(wire, /second has returned/)
})
test('WHAT[DELEG-015] JOIN_V2_duplicate_completed_is_absorbed_by_handle_projection', () => {
  assert.deepEqual(handles.crashScenario('replayed-completed'), handles.crashScenario('completed'))
})
test('WHAT[DELEG-015] JOIN_V2_abandoned_is_not_joinable', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a1', agentName: 'Ada', reason: 'gone' }])
  assert.doesNotMatch(wire, /has returned/)
})
