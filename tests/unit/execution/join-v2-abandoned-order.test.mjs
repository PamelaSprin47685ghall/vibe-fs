// Join v2 Reviewer fixes: EXEC-009 Abandoned batch + EXEC-018 CreationOrder drain.
//
// Production path must be called:
// - JoinDrain.orderedCandidates / stableJoinKey (sort)
// - JoinDrain.drainFromJournal (merge → sort → CAS consume)
// - HandleController.consume (Abandoned → Retired)
// Forbidden: JS re-implement of sort formula; hand-built NonEmptyBatch around drain.

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import {
  agentCompletion,
  agentJournal,
  forkRuntime,
  handleCompletionCodec,
  handleController,
  handleId,
  handleProjection,
  joinDrain,
  joinResultRenderer,
  maxJoinBatch,
  nonEmptyBatch,
  roles,
  sessionId,
  utcOffset,
} from '../support/domain.mjs'
import * as LinkageProjectionModule from '../../../dist/Journal/LinkageProjection.js'
import * as HandleControllerModule from '../../../dist/Session/HandleController.js'
import { HandleOwnership } from '../../../dist/Kernel/Fact.js'

const runtime = joinResultRenderer.stubRuntime()
const parseWire = (text) => parseToml(text)
const PARENT = sessionId('ses_parent')

const withJournal = (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-join-drain-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return fn(created.journal)
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

const durableLink = (j, parentId, agentId, child, targetAgent, role) => {
  const result = HandleControllerModule.HandleController_link(
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

const linkDurable = (j, agentId, child, targetAgent = 'fast-coder') => {
  const linked = durableLink(
    j,
    PARENT,
    agentId,
    sessionId(child),
    targetAgent,
    forkRuntime.role('Coder'),
  )
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
}

// ── EXEC-009: Abandoned wire is flat [[result]] item, not nested [error] ─────

test('EXEC_009_abandoned_item_wire_is_flat_kind_status_agent_reason', () => {
  const batch = nonEmptyBatch.ofHeadTail(
    agentCompletion.abandonedRun({
      agentId: 'h-abandoned',
      agentName: 'fast-coder',
      reason: 'ParentCancelled',
    }),
  )
  const wire = joinResultRenderer.renderCompletedBatch(runtime, batch)
  const parsed = parseWire(wire)

  assert.equal(parsed.status, 'completed')
  assert.equal(parsed.count, 1)
  assert.deepEqual(parsed.result[0], {
    ordinal: 1,
    kind: 'agent',
    status: 'abandoned',
    agent: 'fast-coder',
    reason: 'ParentCancelled',
  })
  assert.ok(!wire.includes('[error]'), 'Abandoned batch item must not nest [error]')
  assert.ok(wire.includes('status = "abandoned"'))
  assert.ok(!wire.includes('status = "failed"'))
})

// ── EXEC-009: renderer still accepts mixed batch (wire shape only) ───────────

test('EXEC_009_abandoned_and_completed_share_one_batch_wire_shape', () => {
  const batch = nonEmptyBatch.ofHeadTail(
    agentCompletion.abandonedRun({
      agentId: 'h1',
      agentName: 'fast-coder',
      reason: 'DeadlineExceeded',
    }),
    [
      agentCompletion.completedRun({
        runId: 'run-h2',
        agentId: 'h2',
        agentName: 'deep-coder',
        workRecord: 'done',
      }),
    ],
  )
  const wire = joinResultRenderer.renderCompletedBatch(runtime, batch)
  const parsed = parseWire(wire)

  assert.equal(parsed.status, 'completed')
  assert.equal(parsed.count, 2)
  assert.equal(parsed.result[0].status, 'abandoned')
  assert.equal(parsed.result[0].reason, 'DeadlineExceeded')
  assert.equal(parsed.result[0].agent, 'fast-coder')
  assert.equal(parsed.result[1].status, 'completed')
  assert.equal(parsed.result[1].agent, 'deep-coder')
  assert.equal(parsed.error, undefined)
})

// ── EXEC-009: durable mixed batch via production JoinDrain.drainFromJournal ──

test('EXEC_009_drainFromJournal_mixed_abandoned_and_completed_one_batch_no_withhold', () => {
  withJournal((j) => {
    // Link order = CreationOrder. Reverse of agent-id dictionary order so
    // id-sort would put completed first; CreationOrder must put abandoned first.
    linkDurable(j, 'z-completed', 'ses_z', 'zebra-agent') // CreationOrder 0
    linkDurable(j, 'a-abandoned', 'ses_a', 'alpha-agent') // CreationOrder 1

    // Blob body must be HandleCompletionCodec wire (encodeOutcome), not bare LWR text.
    const sealed = agentCompletion.completedRun({
      runId: 'run-z-completed',
      agentId: 'z-completed',
      agentName: 'zebra-agent',
      workRecord: 'work-record-z',
    })
    const body = handleCompletionCodec.encodeOutcome(sealed.RunId, sealed.Outcome)
    const completed = handleController.recordCompletion(
      j,
      PARENT,
      'z-completed',
      'Terminal',
      body,
      sessionId('ses_z'),
    )
    assert.equal(completed.ok, true, completed.ok ? '' : completed.error)

    const abandoned = handleController.recordAbandon(
      j,
      PARENT,
      'a-abandoned',
      'DeadlineExceeded',
      utcOffset('2026-03-01T12:00:00Z'),
    )
    assert.equal(abandoned.ok, true, abandoned.ok ? '' : abandoned.error)

    const before = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.joinable(before).length, 1)
    assert.equal(handleProjection.reportableAbandoned(before).length, 1)

    const drained = joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(drained.ok, true, drained.ok ? '' : drained.error)
    assert.equal(drained.items.length, 2, 'both abandoned + completed in one batch')

    // CreationOrder: z-completed (0) then a-abandoned (1) — not id dictionary (a before z).
    assert.deepEqual(
      drained.items.map((i) => ({ agentId: i.agentId, status: i.status, agentName: i.agentName })),
      [
        { agentId: 'z-completed', status: 'completed', agentName: 'zebra-agent' },
        { agentId: 'a-abandoned', status: 'abandoned', agentName: 'alpha-agent' },
      ],
    )
    assert.equal(drained.items[1].reason, 'DeadlineExceeded')
    assert.equal(drained.items[0].workRecord, 'work-record-z')

    // Wire: one ResultsAvailable-shaped batch, no top-level failed withhold.
    const batch = nonEmptyBatch.ofHeadTail(
      agentCompletion.completedRun({
        runId: drained.items[0].runId,
        agentId: drained.items[0].agentId,
        agentName: drained.items[0].agentName,
        workRecord: drained.items[0].workRecord,
      }),
      [
        agentCompletion.abandonedRun({
          agentId: drained.items[1].agentId,
          agentName: drained.items[1].agentName,
          reason: drained.items[1].reason,
        }),
      ],
    )
    const parsed = parseWire(joinResultRenderer.renderCompletedBatch(runtime, batch))
    assert.equal(parsed.status, 'completed')
    assert.equal(parsed.count, 2)
    assert.equal(parsed.result[0].status, 'completed')
    assert.equal(parsed.result[1].status, 'abandoned')
    assert.equal(parsed.error, undefined)

    // Each handle reported at most once: second drain empty; both retired.
    const again = joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(again.ok, true)
    assert.deepEqual(again.items, [])

    const after = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.joinable(after).length, 0)
    assert.equal(handleProjection.reportableAbandoned(after).length, 0)
    assert.equal(handleProjection.isRetired(handleId.agent('z-completed'), after), true)
    assert.equal(handleProjection.isRetired(handleId.agent('a-abandoned'), after), true)
  })
})

// ── EXEC-009: HandleController.consume Abandoned → Retired, second = AlreadyRetired ─

test('EXEC_009_consume_abandoned_writes_HandleRetired_second_AlreadyRetired', () => {
  withJournal((j) => {
    linkDurable(j, 'h1', 'ses_c', 'fast-coder')
    const abandoned = handleController.recordAbandon(
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

    const first = handleController.consume(j, PARENT, handleId.agent('h1'))
    assert.equal(first.ok, true, first.ok ? '' : first.error)
    assert.equal(handleProjection.lifecycleOf(first.record), 'Abandoned')

    p = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.isRetired(handleId.agent('h1'), p), true)
    assert.equal(handleProjection.reportableAbandoned(p).length, 0)

    const second = handleController.consume(j, PARENT, handleId.agent('h1'))
    assert.deepEqual(second, { ok: false, error: 'AlreadyRetired' })

    // Drain after consume must not re-report the same abandoned handle.
    const drained = joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(drained.ok, true)
    assert.deepEqual(drained.items, [])
  })
})

// ── EXEC-004: failed agent item still carries agent field ────────────────────

test('EXEC_004_failed_item_carries_agent_field', () => {
  const batch = nonEmptyBatch.ofHeadTail(
    agentCompletion.failedRun({
      runId: 'run-f',
      agentId: 'a1',
      agentName: 'fast-coder',
      code: 'ERROR',
      message: 'boom',
    }),
  )
  const wire = joinResultRenderer.renderCompletedBatch(runtime, batch)
  const parsed = parseWire(wire)
  assert.equal(parsed.result[0].agent, 'fast-coder')
  assert.equal(parsed.result[0].status, 'failed')
  assert.equal(parsed.result[0].code, 'ERROR')
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

// ── EXEC-018: production stableJoinKey + orderedCandidates (not JS sort re-impl) ─

test('EXEC_018_stable_sort_key_is_creation_order_then_target_agent', () => {
  let p = handleProjection.empty
  // Link reverse of id dictionary order; TargetAgent also reverse of name order.
  p = link(p, 'z-handle', 'ses_z', 'zebra-agent') // CreationOrder 0
  p = link(p, 'a-handle', 'ses_a', 'alpha-agent') // CreationOrder 1

  const hZ = handleProjection.tryFind(handleId.agent('z-handle'), p)
  const hA = handleProjection.tryFind(handleId.agent('a-handle'), p)
  assert.equal(hZ.CreationOrder, 0)
  assert.equal(hA.CreationOrder, 1)

  // Production key — not a JS re-implementation of the formula.
  assert.deepEqual(joinDrain.stableJoinKey(hZ), { creationOrder: 0, targetAgent: 'zebra-agent' })
  assert.deepEqual(joinDrain.stableJoinKey(hA), { creationOrder: 1, targetAgent: 'alpha-agent' })

  // Make both reportable so orderedCandidates merges them, then sorts.
  const abandonedZ = handleProjection.abandon(handleId.agent('z-handle'), 'ParentCancelled', p)
  assert.equal(abandonedZ.ok, true)
  p = abandonedZ.value
  const abandonedA = handleProjection.abandon(handleId.agent('a-handle'), 'DeadlineExceeded', p)
  assert.equal(abandonedA.ok, true)
  p = abandonedA.value

  const ordered = joinDrain.orderedCandidates(p)
  assert.equal(ordered.length, 2)
  // CreationOrder primary: z (0) before a (1). AgentHandleId dict would reverse.
  assert.equal(handleId.describe(ordered[0].Handle), 'agent:z-handle')
  assert.equal(handleId.describe(ordered[1].Handle), 'agent:a-handle')
  // TargetAgent dict would put alpha before zebra — proves not agent-name primary.
  assert.equal(ordered[0].TargetAgent, 'zebra-agent')
  assert.equal(ordered[1].TargetAgent, 'alpha-agent')
})

test('EXEC_018_orderedCandidates_prefers_creation_order_over_agent_name_dict', () => {
  let p = handleProjection.empty
  // Same CreationOrder direction opposite TargetAgent dictionary order.
  p = link(p, 'id-late', 'ses_late', 'aaa-first-name') // order 0, name would sort first
  p = link(p, 'id-early', 'ses_early', 'zzz-last-name') // order 1, name would sort last

  p = handleProjection.complete(
    handleId.agent('id-late'),
    handleProjection.completionOf('Terminal'),
    p,
  ).value
  p = handleProjection.complete(
    handleId.agent('id-early'),
    handleProjection.completionOf('Terminal'),
    p,
  ).value

  const ordered = joinDrain.orderedCandidates(p)
  assert.equal(ordered.length, 2)
  assert.equal(handleId.describe(ordered[0].Handle), 'agent:id-late')
  assert.equal(handleId.describe(ordered[1].Handle), 'agent:id-early')
  assert.equal(ordered[0].TargetAgent, 'aaa-first-name')
  assert.equal(ordered[1].TargetAgent, 'zzz-last-name')
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
