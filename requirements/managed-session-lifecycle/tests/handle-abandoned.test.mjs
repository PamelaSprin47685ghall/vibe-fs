// tests/unit/execution/handle-abandoned.test.mjs — EXEC-009 Abandoned lifecycle.
//
// HandleLifecycle gains Abandoned. Active|CompletedAwaitingJoin → Abandoned is
// single-assignment; Abandoned is never joinable; retire tombstone stays separate.

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as FactCodecSurface from '../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as HandleFoldSurface from '../../../dist/Execution/Delegation/Handle/FoldSurface.js'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as HandleJournalSurface from '../../../dist/Execution/Delegation/Handle/JournalSurface.js'

const PARENT = 'ses_p'
const CHILD = 'ses_c'
const HANDLE = 'agent:h1'
const fact = (caseName, payload) => ({ case: caseName, payload })

const linkOn = (projection, { handle = HANDLE, child = CHILD, agent = 'coder', role = 'Coder' } = {}) => {
  const applied = HandleSurface.apply(projection, { op: 'link', handle, child, agent, role })
  assert.equal(applied.ok, true, applied.ok ? '' : `link refused: ${JSON.stringify(applied.error)}`)
  return applied.state
}

const abandonOn = (projection, { handle = HANDLE, reason = 'ParentCancelled' } = {}) => {
  const applied = HandleSurface.apply(projection, { op: 'abandon', handle, reason })
  assert.equal(applied.ok, true, applied.ok ? '' : `abandon refused: ${JSON.stringify(applied.error)}`)
  return applied.state
}

const completeOn = (projection, { handle = HANDLE, kind = 'Terminal' } = {}) => {
  const applied = HandleSurface.apply(projection, { op: 'complete', handle, kind })
  assert.equal(applied.ok, true, applied.ok ? '' : `complete refused: ${JSON.stringify(applied.error)}`)
  return applied.state
}

const stateOf = (projection, handle = HANDLE) => HandleSurface.read(projection, handle)
const views = (projection) => HandleSurface.views(projection)

test('WHAT[MANAGED-SESSION-009] EXEC_009_HandleAbandoned_serializes_round_trip', () => {
  const value = fact('HandleAbandoned', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Reason: 'ParentCancelled',
    AbandonedAt: '2026-03-01T12:00:00Z',
  })
  const line = FactCodecSurface.encode({ family: 'Execution', ...value })
  assert.equal(line.includes('HandleAbandoned'), true)
  assert.equal(line.includes('ParentCancelled'), true)

  const decoded = FactCodecSurface.decode(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.case, 'HandleAbandoned')
  assert.equal(decoded.line, line)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_Active_to_Abandoned_fold_and_projection', () => {
  const abandoned = abandonOn(linkOn(HandleSurface.empty()))
  assert.deepEqual(stateOf(abandoned), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'coder',
    role: 'Coder',
    lifecycle: 'Abandoned',
    creationOrder: 0,
    completion: undefined,
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: 'ParentCancelled',
  })
  assert.equal(HandleSurface.isAbandoned(abandoned, HANDLE), true)
  assert.equal(HandleSurface.isRetired(abandoned, HANDLE), false)
  assert.deepEqual(views(abandoned), { listable: [], joinable: [], active: [] })
  // EXEC-009: Abandoned is reportable once via join batch, not via joinable completion cell.
  assert.equal(HandleSurface.reportableAbandonedCount(abandoned), 1)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_CompletedAwaitingJoin_can_abandon', () => {
  const abandoned = abandonOn(completeOn(linkOn(HandleSurface.empty())), { reason: 'DeadlineExceeded' })
  assert.equal(stateOf(abandoned).lifecycle, 'Abandoned')
  assert.equal(stateOf(abandoned).abandonReason, 'DeadlineExceeded')
  assert.deepEqual(views(abandoned).joinable, [])
  assert.equal(HandleSurface.reportableAbandonedCount(abandoned), 1)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_Abandoned_is_not_joinable_and_cannot_complete', () => {
  const abandoned = abandonOn(linkOn(HandleSurface.empty()))
  assert.deepEqual(
    HandleSurface.apply(abandoned, { op: 'complete', handle: HANDLE, kind: 'Terminal' }).error,
    { kind: 'TransitionRejected', reason: 'AlreadyAbandoned' },
  )
  // Single-report path: Abandoned → Retired (join consume), not AlreadyAbandoned.
  const retired = HandleSurface.apply(abandoned, { op: 'retire', handle: HANDLE })
  assert.equal(retired.ok, true)
  assert.equal(stateOf(retired.state).lifecycle, 'Retired')
  assert.equal(HandleSurface.reportableAbandonedCount(retired.state), 0)
  assert.deepEqual(
    HandleSurface.apply(abandoned, { op: 'link', handle: HANDLE, child: CHILD, agent: 'coder', role: 'Coder' }).error,
    { kind: 'TransitionRejected', reason: 'AlreadyAbandoned' },
  )
  // EXEC-009: Retired handles reopen on link for agent reuse. The tombstone is
  // the prior LastCompletion, not a permanent ban on further Labor.
  const reopened = HandleSurface.apply(retired.state, { op: 'link', handle: HANDLE, child: CHILD, agent: 'coder', role: 'Coder' })
  assert.equal(reopened.ok, true, `Retired handle must be reopenable, got ${JSON.stringify(reopened)}`)
  assert.equal(stateOf(reopened.state).lifecycle, 'Active')
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_recordAbandon_CAS_first_wins', async (context) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-abandon-direct-'))
  const created = await HandleJournalSurface.JournalSurface_openJournal(
    dir,
    'managed-session-abandon-direct',
    1,
    '2026-03-01T12:00:00Z',
  )
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  context.after(() => HandleJournalSurface.JournalSurface_dispose(created.journal))
  const j = created.journal
    const linked = await HandleJournalSurface.JournalSurface_link(j, PARENT, 'h1', CHILD, 'coder', 'Coder')
    assert.equal(linked.ok, true, linked.ok ? '' : linked.error)

    const first = await HandleJournalSurface.JournalSurface_recordAbandon(
      j,
      PARENT,
      'h1',
      'ParentCancelled',
      '2026-03-01T12:00:00Z',
    )
    assert.equal(first.ok, true, first.ok ? '' : first.error)

    const second = await HandleJournalSurface.JournalSurface_recordAbandon(
      j,
      PARENT,
      'h1',
      'DeadlineExceeded',
      '2026-03-01T12:01:00Z',
    )
    // Journal accepts the line; fold absorbs AlreadyAbandoned (idempotent replay).
    assert.equal(second.ok, true, second.ok ? '' : second.error)

    const projection = HandleJournalSurface.JournalSurface_snapshot(j, PARENT, HANDLE)
    assert.equal(projection.record.lifecycle, 'Abandoned')
    assert.equal(projection.record.abandonReason, 'ParentCancelled')
    assert.deepEqual(projection.views.joinable, [])
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_fold_replays_HandleAbandoned_idempotent', () => {
  const linked = fact('HandleLinked', {
    ParentSessionId: PARENT,
    ChildSessionId: CHILD,
    Handle: HANDLE,
    TargetAgent: 'coder',
    CanonicalRole: 'Coder',
    Ownership: 'DurableParentHandle',
  })
  const abandoned = fact('HandleAbandoned', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Reason: 'HostSessionGone',
    AbandonedAt: '2026-03-01T12:00:00Z',
  })
  const folded = HandleFoldSurface.foldApply(HandleFoldSurface.foldEmpty(), [
    { seq: 1, stream: `session:${PARENT}`, fact: linked },
    { seq: 2, stream: `session:${PARENT}`, fact: abandoned },
    { seq: 3, stream: `session:${PARENT}`, fact: abandoned },
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const handles = HandleFoldSurface.foldSession(folded.state, PARENT)
  assert.equal(stateOf(handles).lifecycle, 'Abandoned')
  assert.equal(stateOf(handles).abandonReason, 'HostSessionGone')
  assert.deepEqual(views(handles), { listable: [], joinable: [], active: [] })
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_retire_tombstone_unaffected_by_abandon_path', () => {
  const retired = (() => {
    let p = linkOn(HandleSurface.empty())
    p = completeOn(p)
    const r = HandleSurface.apply(p, { op: 'retire', handle: HANDLE })
    assert.equal(r.ok, true)
    return r.state
  })()
  assert.equal(HandleSurface.isRetired(retired, HANDLE), true)
  assert.equal(HandleSurface.isAbandoned(retired, HANDLE), false)
  assert.deepEqual(HandleSurface.apply(retired, { op: 'abandon', handle: HANDLE, reason: 'ParentCancelled' }).error, {
    kind: 'TransitionRejected', reason: 'HandleIsRetired',
  })
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_projection_CAS_duplicate_abandon_refused', () => {
  const abandoned = abandonOn(linkOn(HandleSurface.empty()))
  assert.deepEqual(HandleSurface.apply(abandoned, { op: 'abandon', handle: HANDLE, reason: 'DeadlineExceeded' }).error, {
    kind: 'TransitionRejected', reason: 'AlreadyAbandoned',
  })
  assert.equal(stateOf(abandoned).abandonReason, 'ParentCancelled')
})
