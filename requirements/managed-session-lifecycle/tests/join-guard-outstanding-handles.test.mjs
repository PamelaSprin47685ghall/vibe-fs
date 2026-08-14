// Split from tests/unit/execution/join-guard.test.mjs (cutover Wave 2a);
// owner: managed-session-lifecycle. EXEC-016 outstandingBackground 的 durable 半边：
// Manager/DevOps 的 listable = Active ∪ CompletedAwaitingJoin（handle 视图，
// MANAGED-SESSION-005/007；join 义务谓词 → delegation）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { handleId, handleProjection, roles, sessionId } from '../../verification-system/tests/support/domain.mjs'
import * as LinkageProjectionModule from '../../../dist/Journal/LinkageProjection.js'
import { HandleOwnership } from '../../../dist/Kernel/Fact.js'

/** Production HandleProjection.link takes Ownership (GREEN-7); the domain.mjs
 *  facade bind is stale, so tests call the dist entry directly. */
const link = (handle, child, targetAgent, role, current) => {
  const result = LinkageProjectionModule.HandleProjection_link(
    handle,
    child,
    targetAgent,
    role,
    HandleOwnership.DurableParentHandle,
    current,
  )
  return result.tag === 0
    ? { ok: true, value: result.fields[0] }
    : { ok: false, error: result.fields[0].cases()[result.fields[0].tag] }
}

test('EXEC_016_listable_handles_are_outstanding_for_manager', () => {
  // Durable half of outstandingBackground for Manager/DevOps: listable = Active ∪ CompletedAwaitingJoin.
  let projection = handleProjection.empty
  const handle = handleId.agent('child-1')
  const linked = link(handle, sessionId('ses_child'), 'fast-coder', roles.of('Coder'), projection)
  assert.equal(linked.ok, true)
  projection = linked.value

  assert.equal(handleProjection.listable(projection).length, 1)
  assert.equal(handleProjection.joinable(projection).length, 0)

  const completed = handleProjection.complete(handle, handleProjection.completionOf('Terminal'), projection)
  assert.equal(completed.ok, true)
  projection = completed.value
  assert.equal(handleProjection.listable(projection).length, 1)
  assert.equal(handleProjection.joinable(projection).length, 1)

  const retired = handleProjection.retire(handle, projection)
  assert.equal(retired.ok, true)
  projection = retired.value
  assert.equal(handleProjection.listable(projection).length, 0)
})
