import assert from 'node:assert/strict'
import test from 'node:test'
import { handleId, handleProjection, roles, sessionId } from './support/managed-surface.mjs'

const linkedProjection = () => {
  const linked = handleProjection.link(handleId.agent('child-1'), sessionId('ses_child'), 'fast-coder', roles.of('Coder'), handleProjection.empty)
  assert.equal(linked.ok, true)
  return linked.value
}

test('WHAT[MANAGED-SESSION-006] EXEC_016_listable_handles_are_outstanding_for_manager', () => {
  const projection = linkedProjection()
  assert.equal(handleProjection.listable(projection).length, 1)
  assert.equal(handleProjection.joinable(projection).length, 0)
})
