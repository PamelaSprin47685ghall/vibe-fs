// Split from tests/unit/execution/join-v2-abandoned-order.test.mjs (cutover Wave 2a);
// owner: managed-session-lifecycle. EXEC-009 Abandoned 生命周期的 consume 与投影半边：
// consume 唯一写 retire（Abandoned → HandleRetired，二次 AlreadyRetired）；
// CreationOrder 来自 HandleLinked fold 顺序；Abandoned retire 清 reportable 单次投递。
// drain 顺序/批次与 wire → delegation。

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentJournal,
  forkRuntime,
  handleController,
  handleId,
  handleProjection,
  joinDrain,
  maxJoinBatch,
  roles,
  sessionId,
  utcOffset,
} from '../../verification-system/tests/support/domain.mjs'
import * as LinkageProjectionModule from '../../../dist/Journal/LinkageProjection.js'
import * as HandleControllerModule from '../../../dist/Session/HandleController.js'
import { HandleOwnership } from '../../../dist/Kernel/Fact.js'

const PARENT = sessionId('ses_parent')

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-join-drain-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    created.dispose()
  }
}

/** Production link entries take Ownership (GREEN-7); the domain.mjs facade binds
 *  are stale, so tests call the dist entries directly with DurableParentHandle. */
const projectionLink = (handle, child, targetAgent, role, current) => {
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

const durableLink = async (j, parentId, agentId, child, targetAgent, role) => {
  const result = await HandleControllerModule.HandleController_link(
    j,
    parentId,
    agentId,
    child,
    targetAgent,
    role,
    HandleOwnership.DurableParentHandle,
  )
  return result.tag === 0 ? { ok: true, value: result.fields[0] } : { ok: false, error: result.fields[0] }
}

const link = (projection, agentId, child, targetAgent = 'fast-coder') => {
  const applied = projectionLink(
    handleId.agent(agentId),
    sessionId(child),
    targetAgent,
    roles.of('Coder'),
    projection,
  )
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  return applied.value
}

const linkDurable = async (j, agentId, child, targetAgent = 'fast-coder') => {
  const linked = await durableLink(
    j,
    PARENT,
    agentId,
    sessionId(child),
    targetAgent,
    forkRuntime.role('Coder'),
  )
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
}

// ── EXEC-009: HandleController.consume Abandoned → Retired, second = AlreadyRetired ─

test('EXEC_009_consume_abandoned_writes_HandleRetired_second_AlreadyRetired', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'h1', 'ses_c', 'fast-coder')
    const abandoned = await handleController.recordAbandon(
      j,
      PARENT,
      'h1',
      'ParentCancelled',
      utcOffset('2026-03-01T12:00:00Z'),
    )
    assert.equal(abandoned.ok, true, abandoned.ok ? '' : abandoned.error)

    let p = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.reportableAbandoned(p).length, 1)
    assert.equal(handleProjection.lifecycleOf(handleProjection.tryFind(handleId.agent('h1'), p)), 'Abandoned')

    const first = await handleController.consume(j, PARENT, handleId.agent('h1'))
    assert.equal(first.ok, true, first.ok ? '' : first.error)
    assert.equal(handleProjection.lifecycleOf(first.record), 'Abandoned')

    p = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.isRetired(handleId.agent('h1'), p), true)
    assert.equal(handleProjection.reportableAbandoned(p).length, 0)

    const second = await handleController.consume(j, PARENT, handleId.agent('h1'))
    assert.deepEqual(second, { ok: false, error: 'AlreadyRetired' })

    // Drain after consume must not re-report the same abandoned handle.
    const drained = await joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(drained.ok, true)
    assert.deepEqual(drained.items, [])
  })
})

// ── EXEC-018: CreationOrder from HandleLinked fold order ─────────────────────

test('EXEC_018_creation_order_follows_HandleLinked_fold_sequence', () => {
  let p = handleProjection.empty
  p = link(p, 'later-id-zzz', 'ses_z', 'zebra-agent')
  p = link(p, 'earlier-id-aaa', 'ses_a', 'alpha-agent')
  p = link(p, 'mid-id-mmm', 'ses_m', 'mid-agent')

  const children = handleProjection.linkedChildren(p).map((r) => handleProjection.read(r))
  assert.equal(children.find((c) => c.handle === 'agent:later-id-zzz').creationOrder, 0)
  assert.equal(children.find((c) => c.handle === 'agent:earlier-id-aaa').creationOrder, 1)
  assert.equal(children.find((c) => c.handle === 'agent:mid-id-mmm').creationOrder, 2)
})

// ── EXEC-009: Abandoned single-report via retire after reportable ────────────

test('EXEC_009_abandoned_retire_clears_reportable_single_report', () => {
  let p = link(handleProjection.empty, 'h1', 'ses_c')
  const abandoned = handleProjection.abandon(handleId.agent('h1'), 'ParentCancelled', p)
  assert.equal(abandoned.ok, true)
  p = abandoned.value
  assert.equal(handleProjection.reportableAbandoned(p).length, 1)

  const retired = handleProjection.retire(handleId.agent('h1'), p)
  assert.equal(retired.ok, true)
  p = retired.value
  assert.equal(handleProjection.reportableAbandoned(p).length, 0)
  assert.equal(handleProjection.isRetired(handleId.agent('h1'), p), true)
  assert.deepEqual(handleProjection.retire(handleId.agent('h1'), p), {
    ok: false,
    error: 'HandleIsRetired',
  })
})
