// tests/unit/execution/handle-abandoned.test.mjs — EXEC-009 Abandoned lifecycle.
//
// HandleLifecycle gains Abandoned. Active|CompletedAwaitingJoin → Abandoned is
// single-assignment; Abandoned is never joinable; retire tombstone stays separate.

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentFactCaseOf,
  agentJournal,
  caseNameOf,
  clockAt,
  envelope,
  fact,
  fold,
  forkRuntime,
  handleAbandonReason,
  handleController,
  handleId,
  handleOwnership,
  handleProjection,
  journal,
  readPayload,
  roles,
  sessionId,
  stream,
  utcOffset,
} from './support/managed-surface.mjs'

const PARENT = sessionId('ses_p')
const CHILD = sessionId('ses_c')
const HANDLE = handleId.agent('h1')

const linkOn = (projection, { handle = HANDLE, child = CHILD, agent = 'fast-coder', role = 'Coder' } = {}) => {
  const applied = handleProjection.link(handle, child, agent, roles.of(role), projection)
  assert.equal(applied.ok, true, applied.ok ? '' : `link refused: ${applied.error}`)
  return applied.value
}

const abandonOn = (projection, { handle = HANDLE, reason = 'ParentCancelled' } = {}) => {
  const applied = handleProjection.abandon(handle, reason, projection)
  assert.equal(applied.ok, true, applied.ok ? '' : `abandon refused: ${applied.error}`)
  return applied.value
}

const completeOn = (projection, { handle = HANDLE, kind = 'Terminal' } = {}) => {
  const applied = handleProjection.complete(handle, handleProjection.completionOf(kind), projection)
  assert.equal(applied.ok, true, applied.ok ? '' : `complete refused: ${applied.error}`)
  return applied.value
}

const stateOf = (projection, handle = HANDLE) => handleProjection.read(handleProjection.tryFind(handle, projection))

const views = (projection) => ({
  listable: handleProjection.listable(projection).map((r) => handleProjection.read(r).handle).sort(),
  joinable: handleProjection.joinable(projection).map((r) => handleProjection.read(r).handle).sort(),
  active: handleProjection.activeHandles(projection).map((r) => handleProjection.read(r).handle).sort(),
})

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-abandon-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    created.dispose()
  }
}

test('WHAT[MANAGED-SESSION-009] EXEC_009_HandleAbandoned_serializes_round_trip', () => {
  const value = fact('HandleAbandoned', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Reason: handleAbandonReason.parentCancelled(),
    AbandonedAt: utcOffset('2026-03-01T12:00:00Z'),
  })
  const line = journal.serializeFact(value)
  assert.equal(line.includes('HandleAbandoned'), true)
  assert.equal(line.includes('ParentCancelled'), true)

  const decoded = journal.deserializeFact(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  // DSL-003: Fact → Agent dispatch → Execution family dispatch → payload.
  assert.equal(agentFactCaseOf(decoded.value), 'HandleAbandoned')
  const payload = readPayload(readPayload(decoded.value))
  assert.equal(handleId.describe(payload.Handle), 'agent:h1')
  assert.equal(caseNameOf(payload.Reason), 'ParentCancelled')
  assert.equal(journal.serializeFact(decoded.value), line)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_Active_to_Abandoned_fold_and_projection', () => {
  const abandoned = abandonOn(linkOn(handleProjection.empty))
  assert.deepEqual(stateOf(abandoned), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'fast-coder',
    role: 'Coder',
    lifecycle: 'Abandoned',
    creationOrder: 0,
    completion: undefined,
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: 'ParentCancelled',
  })
  assert.equal(handleProjection.isAbandoned(HANDLE, abandoned), true)
  assert.equal(handleProjection.isRetired(HANDLE, abandoned), false)
  assert.deepEqual(views(abandoned), { listable: [], joinable: [], active: [] })
  // EXEC-009: Abandoned is reportable once via join batch, not via joinable completion cell.
  assert.equal(handleProjection.reportableAbandoned(abandoned).length, 1)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_CompletedAwaitingJoin_can_abandon', () => {
  const abandoned = abandonOn(completeOn(linkOn(handleProjection.empty)), { reason: 'DeadlineExceeded' })
  assert.equal(stateOf(abandoned).lifecycle, 'Abandoned')
  assert.equal(stateOf(abandoned).abandonReason, 'DeadlineExceeded')
  assert.deepEqual(views(abandoned).joinable, [])
  assert.equal(handleProjection.reportableAbandoned(abandoned).length, 1)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_Abandoned_is_not_joinable_and_cannot_complete', () => {
  const abandoned = abandonOn(linkOn(handleProjection.empty))
  assert.deepEqual(
    handleProjection.complete(HANDLE, handleProjection.completionOf('Terminal'), abandoned),
    { ok: false, error: 'AlreadyAbandoned' },
  )
  // Single-report path: Abandoned → Retired (join consume), not AlreadyAbandoned.
  const retired = handleProjection.retire(HANDLE, abandoned)
  assert.equal(retired.ok, true)
  assert.equal(handleProjection.lifecycleOf(handleProjection.tryFind(HANDLE, retired.value)), 'Retired')
  assert.equal(handleProjection.reportableAbandoned(retired.value).length, 0)
  assert.deepEqual(
    handleProjection.link(HANDLE, CHILD, 'fast-coder', roles.of('Coder'), abandoned),
    { ok: false, error: 'AlreadyAbandoned' },
  )
  // EXEC-009: Retired handles reopen on link for agent reuse. The tombstone is
  // the prior LastCompletion, not a permanent ban on further Labor.
  const reopened = handleProjection.link(HANDLE, CHILD, 'fast-coder', roles.of('Coder'), retired.value)
  assert.equal(reopened.ok, true, `Retired handle must be reopenable, got ${JSON.stringify(reopened)}`)
  assert.equal(handleProjection.lifecycleOf(handleProjection.tryFind(HANDLE, reopened.value)), 'Active')
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_recordAbandon_CAS_first_wins', async () => {
  await withJournal(async (j) => {
    const linked = await handleController.link(j, PARENT, 'h1', CHILD, 'fast-coder', forkRuntime.role('Coder'))
    assert.equal(linked.ok, true, linked.ok ? '' : linked.error)

    const first = await handleController.recordAbandon(
      j,
      PARENT,
      'h1',
      handleAbandonReason.parentCancelled(),
      utcOffset('2026-03-01T12:00:00Z'),
    )
    assert.equal(first.ok, true, first.ok ? '' : first.error)

    const second = await handleController.recordAbandon(
      j,
      PARENT,
      'h1',
      handleAbandonReason.deadlineExceeded(),
      utcOffset('2026-03-01T12:01:00Z'),
    )
    // Journal accepts the line; fold absorbs AlreadyAbandoned (idempotent replay).
    assert.equal(second.ok, true, second.ok ? '' : second.error)

    const projection = agentJournal.handleProjection(j, PARENT)
    assert.equal(stateOf(projection).lifecycle, 'Abandoned')
    assert.equal(stateOf(projection).abandonReason, 'ParentCancelled')
    assert.deepEqual(views(projection).joinable, [])
  })
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_fold_replays_HandleAbandoned_idempotent', () => {
  const linked = fact('HandleLinked', {
    ParentSessionId: PARENT,
    ChildSessionId: CHILD,
    Handle: HANDLE,
    TargetAgent: 'fast-coder',
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
  })
  const abandoned = fact('HandleAbandoned', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Reason: handleAbandonReason.hostSessionGone(),
    AbandonedAt: clockAt('2026-03-01T12:00:00Z'),
  })
  const folded = fold.apply(fold.empty, [
    envelope({ seq: 1, stream: stream.session(PARENT), fact: linked }),
    envelope({ seq: 2, stream: stream.session(PARENT), fact: abandoned }),
    envelope({ seq: 3, stream: stream.session(PARENT), fact: abandoned }),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const handles = fold.session(folded.value, 'ses_p').Handles
  assert.equal(stateOf(handles).lifecycle, 'Abandoned')
  assert.equal(stateOf(handles).abandonReason, 'HostSessionGone')
  assert.deepEqual(views(handles), { listable: [], joinable: [], active: [] })
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_retire_tombstone_unaffected_by_abandon_path', () => {
  const retired = (() => {
    let p = linkOn(handleProjection.empty)
    p = completeOn(p)
    const r = handleProjection.retire(HANDLE, p)
    assert.equal(r.ok, true)
    return r.value
  })()
  assert.equal(handleProjection.isRetired(HANDLE, retired), true)
  assert.equal(handleProjection.isAbandoned(HANDLE, retired), false)
  assert.deepEqual(handleProjection.abandon(HANDLE, 'ParentCancelled', retired), {
    ok: false,
    error: 'HandleIsRetired',
  })
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_projection_CAS_duplicate_abandon_refused', () => {
  const abandoned = abandonOn(linkOn(handleProjection.empty))
  assert.deepEqual(handleProjection.abandon(HANDLE, 'DeadlineExceeded', abandoned), {
    ok: false,
    error: 'AlreadyAbandoned',
  })
  assert.equal(stateOf(abandoned).abandonReason, 'ParentCancelled')
})
