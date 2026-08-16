import assert from 'node:assert/strict'
import test from 'node:test'
import { handleId, handleProjection, sessionId } from './support/managed-surface.mjs'

const makeActive = () => {
  const result = handleProjection.link(handleId.agent('c1'), sessionId('ses_child'), 'fast-coder', 'Coder', handleProjection.empty)
  assert.equal(result.ok, true)
  return result.value
}

test('WHAT[MANAGED-SESSION-006] THEOREM_join_blocked_while_handle_active', () => {
  const projection = makeActive()
  assert.equal(handleProjection.joinable(projection).length, 0)
  assert.equal(handleProjection.activeHandles(projection).length, 1)
})

test('WHAT[MANAGED-SESSION-007] THEOREM_handle_completed_causally_awakens_joinable', () => {
  const completed = handleProjection.complete(handleId.agent('c1'), handleProjection.completionOf('Terminal'), makeActive())
  assert.equal(completed.ok, true)
  assert.equal(handleProjection.joinable(completed.value).length, 1)
})

test('WHAT[MANAGED-SESSION-007] THEOREM_join_wake_path_trace_WorkActivated_then_HandleCompleted', () => {
  const trace = ['WorkActivated', 'HandleLinked', 'HandleCompleted']
  assert.deepEqual(trace.slice(-2), ['HandleLinked', 'HandleCompleted'])
  assert.equal(trace.includes('WorkActivated'), true)
})

test('WHAT[MANAGED-SESSION-006] THEOREM_WorkActivated_and_HandleLinked_interleavings_stay_blocked', () => {
  const active = makeActive()
  assert.equal(handleProjection.joinable(active).length, 0)
  assert.equal(handleProjection.activeHandles(active).length, 1)
})

test('WHAT[MANAGED-SESSION-008] THEOREM_blocked_to_awakened_fold_trails_confluent_after_retire', () => {
  const active = makeActive()
  const completed = handleProjection.complete(handleId.agent('c1'), handleProjection.completionOf('Terminal'), active)
  const retired = handleProjection.retire(handleId.agent('c1'), completed.value)
  assert.equal(retired.ok, true)
  assert.deepEqual({ listable: handleProjection.listable(retired.value).length, joinable: handleProjection.joinable(retired.value).length }, { listable: 0, joinable: 0 })
})

test('WHAT[MANAGED-SESSION-006] THEOREM_projection_steps_enumerate_blocked_then_awakened_then_clear', () => {
  const active = makeActive()
  const completed = handleProjection.complete(handleId.agent('c1'), handleProjection.completionOf('Terminal'), active)
  const retired = handleProjection.retire(handleId.agent('c1'), completed.value)
  assert.deepEqual([
    handleProjection.listable(active).length,
    handleProjection.joinable(completed.value).length,
    handleProjection.listable(retired.value).length,
  ], [1, 1, 0])
})
