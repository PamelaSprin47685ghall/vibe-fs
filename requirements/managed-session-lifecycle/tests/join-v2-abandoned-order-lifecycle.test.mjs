import assert from 'node:assert/strict'
import test from 'node:test'
import { handleId, handleProjection, sessionId } from './support/managed-surface.mjs'

const link = (projection, agentId, child, targetAgent = 'fast-coder') => {
  const result = handleProjection.link(handleId.agent(agentId), sessionId(child), targetAgent, 'Coder', projection)
  assert.equal(result.ok, true)
  return result.value
}

test('WHAT[MANAGED-SESSION-008] EXEC_009_consume_abandoned_writes_HandleRetired_second_AlreadyRetired', () => {
  let projection = link(handleProjection.empty, 'h1', 'ses_c')
  projection = handleProjection.abandon(handleId.agent('h1'), 'ParentCancelled', projection).value
  assert.equal(handleProjection.reportableAbandoned(projection).length, 1)
  const consumed = handleProjection.retire(handleId.agent('h1'), projection)
  assert.equal(consumed.ok, true)
  projection = consumed.value
  assert.equal(handleProjection.isRetired(handleId.agent('h1'), projection), true)
  assert.equal(handleProjection.reportableAbandoned(projection).length, 0)
  assert.deepEqual(handleProjection.retire(handleId.agent('h1'), projection), { ok: false, error: 'HandleIsRetired' })
})

test('WHAT[MANAGED-SESSION-015] EXEC_018_creation_order_follows_HandleLinked_fold_sequence', () => {
  const projection = link(link(link(handleProjection.empty, 'later-id-zzz', 'ses_z', 'zebra-agent'), 'earlier-id-aaa', 'ses_a', 'alpha-agent'), 'mid-id-mmm', 'ses_m', 'mid-agent')
  const children = handleProjection.linkedChildren(projection).map(handleProjection.read)
  assert.equal(children.find((item) => item.handle === 'agent:later-id-zzz').creationOrder, 0)
  assert.equal(children.find((item) => item.handle === 'agent:earlier-id-aaa').creationOrder, 1)
  assert.equal(children.find((item) => item.handle === 'agent:mid-id-mmm').creationOrder, 2)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_abandoned_retire_clears_reportable_single_report', () => {
  let projection = link(handleProjection.empty, 'h1', 'ses_c')
  projection = handleProjection.abandon(handleId.agent('h1'), 'ParentCancelled', projection).value
  assert.equal(handleProjection.reportableAbandoned(projection).length, 1)
  projection = handleProjection.retire(handleId.agent('h1'), projection).value
  assert.equal(handleProjection.reportableAbandoned(projection).length, 0)
  assert.equal(handleProjection.isRetired(handleId.agent('h1'), projection), true)
})
