// Split from tests/unit/execution/handle.test.mjs (cutover Wave 2a);
// owner: managed-session-lifecycle. EXEC-004/005/009 handle 生命周期：typed
// handle 身份、completion cell 单赋值、三视图、tombstone 永久、Abandoned 单赋值、
// fold 事实重放/拒绝、blob 负载与 0.5.1 codec 迁移（MANAGED-SESSION-006/007/008/
// 009/015）。EXEC-011/012 deadline/estimate 代数 → time-capability；
// EXEC-011 kill-ack/oneshot/EXEC-010 process request → process-execution；
// EXEC-008 child background → delegation；ENFORCER stopPhysicalRun →
// behavior-diagnosis。
//
// A handle's durable lifecycle has three states and they must stay
// distinguishable. The model this replaced held two maps (linked / unlinked),
// which cannot express completed-awaiting-join — so a child that had finished but
// nobody had joined was reported as still running.
//
// The tombstone is the other load-bearing idea. A retired id stays in the map
// forever, because removing it would make "retired" indistinguishable from "never
// existed", and EXEC-009 names the consequence: the caller degrades into treating
// the input as an agent name and forks a second child.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  completionKind,
  fact,
  fold,
  envelope,
  handleId,
  handleOwnership,
  handleProjection,
  isSome,
  journal,
  roles,
  sessionId,
  stream,
} from './support/managed-surface.mjs'

const PARENT = sessionId('ses_p')
const CHILD = sessionId('ses_c')
const HANDLE = handleId.agent('h1')

const linkOn = (projection, { handle = HANDLE, child = CHILD, agent = 'fast-coder', role = 'Coder' } = {}) => {
  const applied = handleProjection.link(handle, child, agent, roles.of(role), projection)
  assert.equal(applied.ok, true, applied.ok ? '' : `link refused: ${applied.error}`)
  return applied.value
}

const completeOn = (projection, { handle = HANDLE, kind = 'Terminal', ref, digest } = {}) => {
  const applied = handleProjection.complete(
    handle,
    handleProjection.completionOf(kind, ref, digest),
    projection,
  )
  assert.equal(applied.ok, true, applied.ok ? '' : `complete refused: ${applied.error}`)
  return applied.value
}

const retireOn = (projection, { handle = HANDLE } = {}) => {
  const applied = handleProjection.retire(handle, projection)
  assert.equal(applied.ok, true, applied.ok ? '' : `retire refused: ${applied.error}`)
  return applied.value
}

const stateOf = (projection, handle = HANDLE) => handleProjection.read(handleProjection.tryFind(handle, projection))

/** Which handles each derived view returns, as sorted describe() strings. */
const views = (projection) => ({
  listable: handleProjection.listable(projection).map((r) => handleProjection.read(r).handle).sort(),
  joinable: handleProjection.joinable(projection).map((r) => handleProjection.read(r).handle).sort(),
  active: handleProjection.activeHandles(projection).map((r) => handleProjection.read(r).handle).sort(),
})

// ── EXEC-009: typed handles are distinct identities ──────────────────────────

test('WHAT[MANAGED-SESSION-006] EXEC_009_agent_pty_and_manager_job_handles_are_separate_identities', () => {
  // The same string in three handle kinds must be three map keys. Collapsing them
  // to the raw string would let retiring an agent handle retire the PTY that
  // happens to share its id.
  let projection = linkOn(handleProjection.empty, { handle: handleId.agent('x'), child: sessionId('ses_a') })
  projection = linkOn(projection, {
    handle: handleId.pty('x'),
    child: sessionId('ses_b'),
    agent: 'fast-devops',
    role: 'DevOps',
  })
  projection = linkOn(projection, {
    handle: handleId.managerJob('x'),
    child: sessionId('ses_j'),
    agent: 'fast-manager',
    role: 'Manager',
  })

  assert.deepEqual(views(projection).active, ['agent:x', 'manager-job:x', 'pty:x'])

  // Retiring one leaves the other two untouched.
  const retired = retireOn(completeOn(projection, { handle: handleId.agent('x') }), { handle: handleId.agent('x') })

  assert.equal(handleProjection.isRetired(handleId.agent('x'), retired), true)
  assert.equal(handleProjection.isRetired(handleId.pty('x'), retired), false)
  assert.equal(stateOf(retired, handleId.pty('x')).lifecycle, 'Active')
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_only_an_agent_handle_answers_the_agent_question', () => {
  // `tryAgent` exists so a caller that needs an AgentHandleId cannot silently
  // accept a PTY handle by string coercion.
  assert.equal(isSome(handleId.tryAgent(handleId.agent('h1'))), true)
  assert.equal(isSome(handleId.tryAgent(handleId.pty('h1'))), false)
  assert.equal(isSome(handleId.tryAgent(handleId.managerJob('h1'))), false)

  // And `describe` names the kind, so a diagnostic cannot confuse the three.
  assert.deepEqual(
    [handleId.agent('x'), handleId.pty('x'), handleId.managerJob('x')].map(handleId.describe),
    ['agent:x', 'pty:x', 'manager-job:x'],
  )
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_a_linked_handle_records_the_child_session_it_drives', () => {
  // The field this pins was missing until package F: `HandleLinked` carried no
  // child SessionId, so eight consumers could not get from a handle to its child
  // and every read side of EXEC-009 was dangling.
  const projection = linkOn(handleProjection.empty)

  assert.deepEqual(stateOf(projection), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'fast-coder',
    role: 'Coder',
    lifecycle: 'Active',
    creationOrder: 0,
    completion: undefined,
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: undefined,
  })
})

// ── EXEC-004: the completion cell is single-assignment ───────────────────────

test('WHAT[MANAGED-SESSION-007] EXEC_004_the_first_completion_wins_and_later_ones_are_refused', () => {
  // Terminal, send-failure and cancel race for one cell. The loser must be
  // REFUSED rather than overwrite the winner, or a cancelled child could report
  // the terminal it never reached.
  const completed = completeOn(linkOn(handleProjection.empty), { kind: 'Terminal' })
  assert.equal(stateOf(completed).completion, 'Terminal')

  for (const late of ['SendFailure', 'Cancelled', 'Terminal']) {
    assert.deepEqual(
      handleProjection.complete(HANDLE, handleProjection.completionOf(late), completed),
      { ok: false, error: 'AlreadyCompleted' },
      `a late ${late} must not overwrite the first winner`,
    )
  }

  assert.equal(stateOf(completed).completion, 'Terminal', 'the winner is unchanged')
})

test('WHAT[MANAGED-SESSION-007] EXEC_004_each_completion_kind_survives_into_the_state', () => {
  // EXEC-005 requires `list` to say WHICH completion landed, so the kind is part
  // of the lifecycle state rather than a boolean beside it.
  for (const kind of ['Terminal', 'SendFailure', 'Cancelled']) {
    const completed = completeOn(linkOn(handleProjection.empty), { kind })
    assert.deepEqual(
      { lifecycle: stateOf(completed).lifecycle, completion: stateOf(completed).completion },
      { lifecycle: 'CompletedAwaitingJoin', completion: kind },
    )
  }
})

test('WHAT[MANAGED-SESSION-007] EXEC_004_completing_an_unknown_handle_is_refused_by_name', () => {
  const projection = linkOn(handleProjection.empty)

  assert.deepEqual(handleProjection.complete(handleId.agent('never'), handleProjection.completionOf('Terminal'), projection), {
    ok: false,
    error: 'UnknownHandle',
  })
})

test('WHAT[MANAGED-SESSION-008] EXEC_004_join_may_only_retire_a_handle_that_actually_completed', () => {
  // `retire` IS join's write. Retiring an active handle would discard a child
  // that is still running and leave its completion with nowhere to land.
  const active = linkOn(handleProjection.empty)

  assert.deepEqual(handleProjection.retire(HANDLE, active), { ok: false, error: 'NotCompleted' })
  assert.deepEqual(handleProjection.retire(handleId.agent('never'), active), { ok: false, error: 'UnknownHandle' })
})

// ── EXEC-005: the three derived views, and what each excludes ────────────────

test('WHAT[MANAGED-SESSION-006] EXEC_005_the_views_partition_the_lifecycle_and_never_show_retired', () => {
  const active = linkOn(handleProjection.empty)
  const completed = completeOn(active)
  const retired = retireOn(completed)

  // Active: listable and cancellable, not yet joinable.
  assert.deepEqual(views(active), { listable: ['agent:h1'], joinable: [], active: ['agent:h1'] })

  // Completed-awaiting-join: still listable (that is the whole point of the
  // state) and now joinable, but no longer an active resource to cancel.
  assert.deepEqual(views(completed), { listable: ['agent:h1'], joinable: ['agent:h1'], active: [] })

  // Retired: invisible to all three. `list` must not offer a resource that
  // cannot be joined or cancelled.
  assert.deepEqual(views(retired), { listable: [], joinable: [], active: [] })
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_parent_abort_needs_the_handles_themselves_not_a_count', () => {
  // "Cancel every owned physical resource individually" is only expressible if
  // the caller gets the ids. A count would force it to guess which ones.
  let projection = linkOn(handleProjection.empty, { handle: handleId.agent('a'), child: sessionId('ses_1') })
  projection = linkOn(projection, { handle: handleId.agent('b'), child: sessionId('ses_2') })
  projection = linkOn(projection, { handle: handleId.pty('p'), child: sessionId('ses_3'), role: 'DevOps' })

  // One has already completed, so it is no longer an active resource.
  projection = completeOn(projection, { handle: handleId.agent('b') })

  assert.deepEqual(views(projection).active, ['agent:a', 'pty:p'])
})

// ── EXEC-009: the tombstone is permanent ────────────────────────────────────

test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_handle_answers_retired_forever', () => {
  const retired = retireOn(completeOn(linkOn(handleProjection.empty)))

  assert.equal(handleProjection.isRetired(HANDLE, retired), true)
  assert.equal(stateOf(retired).lifecycle, 'Retired')

  // Every transition is refused from here, including a second retirement.
  assert.deepEqual(handleProjection.retire(HANDLE, retired), { ok: false, error: 'HandleIsRetired' })
  assert.deepEqual(handleProjection.complete(HANDLE, handleProjection.completionOf('Terminal'), retired), {
    ok: false,
    error: 'HandleIsRetired',
  })
  // Retired handles are reusable — same agent id reopens on the same child session
  // (HandleLinked re-append → Active) for the next work unit. The tombstone is
  // the prior LastCompletion blob, not a ban on further Labor.
  const reopened = handleProjection.link(HANDLE, CHILD, 'fast-coder', roles.of('Coder'), retired)
  assert.equal(reopened.ok, true, `Retired handle must be reopenable for reuse, got ${JSON.stringify(reopened)}`)
  assert.equal(handleProjection.isRetired(HANDLE, reopened.ok ? reopened.value : retired), false)
  assert.deepEqual(reopened.ok ? handleProjection.read(handleProjection.tryFind(HANDLE, reopened.value)).lifecycle : null, 'Active')
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_id_is_distinguishable_from_one_that_never_existed', () => {
  // The exact confusion the tombstone prevents. If the record were deleted on
  // retire, these two lookups would be identical and `fork` would treat a spent
  // handle id as an agent name.
  const retired = retireOn(completeOn(linkOn(handleProjection.empty)))

  assert.equal(isSome(handleProjection.tryFind(HANDLE, retired)), true)
  assert.equal(isSome(handleProjection.tryFind(handleId.agent('never'), retired)), false)

  assert.deepEqual(
    {
      retiredId: handleProjection.isRetired(HANDLE, retired),
      unknownId: handleProjection.isRetired(handleId.agent('never'), retired),
    },
    { retiredId: true, unknownId: false },
  )
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_child_session_is_still_recognised_as_a_child', () => {
  // "Is this session one of mine" must answer yes for a child that already
  // finished — otherwise a late event from it looks like it came from a stranger.
  const retired = retireOn(completeOn(linkOn(handleProjection.empty)))
  const found = handleProjection.tryFindByChildSession(CHILD, retired)

  assert.equal(isSome(found), true)
  assert.equal(handleProjection.read(found).lifecycle, 'Retired')
  assert.equal(isSome(handleProjection.tryFindByChildSession(sessionId('ses_other'), retired)), false)
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_relinking_a_live_handle_rebinds_it_rather_than_duplicating', () => {
  // Restart recovery re-links the same handle id to the same session. That must
  // be idempotent for a live handle — and refused only once retired.
  const projection = linkOn(handleProjection.empty)
  const relinked = linkOn(projection)

  assert.equal(handleProjection.linkedChildren(relinked).length, 1)
  assert.deepEqual(stateOf(relinked), stateOf(projection))
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_linked_children_lists_every_child_ever_linked', () => {
  // Replaces the old live-only `LinkedChildren` map, which forced restart
  // recovery and the retired-handle check to use two different structures.
  let projection = linkOn(handleProjection.empty, { handle: handleId.agent('a'), child: sessionId('ses_1') })
  projection = linkOn(projection, { handle: handleId.agent('b'), child: sessionId('ses_2') })
  projection = retireOn(completeOn(projection, { handle: handleId.agent('a') }), { handle: handleId.agent('a') })

  assert.deepEqual(
    handleProjection.linkedChildren(projection).map((r) => handleProjection.read(r).child).sort(),
    ['ses_1', 'ses_2'],
    'a retired child stays in the list',
  )
})

// ── EXEC-009 through the fold: which refusals stop a replay ─────────────────

const handleFact = {
  linked: fact('HandleLinked', {
    ParentSessionId: PARENT,
    ChildSessionId: CHILD,
    Handle: HANDLE,
    TargetAgent: 'fast-coder',
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
  }),
  completed: fact('HandleCompleted', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Kind: completionKind.of('Terminal'),
    CompletionRef: undefined,
    CompletionDigest: undefined,
  }),
  completedWithBlob: fact('HandleCompleted', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Kind: completionKind.of('Terminal'),
    CompletionRef: blobRef('blobs/completion-h1'),
    CompletionDigest: blobDigest('sha-completion-h1'),
  }),
  retired: fact('HandleRetired', { ParentSessionId: PARENT, Handle: HANDLE }),
}

const foldFacts = (facts) =>
  fold.apply(
    fold.empty,
    facts.map((value, index) => envelope({ seq: index + 1, stream: stream.session(PARENT), fact: value })),
  )

test('WHAT[MANAGED-SESSION-006] EXEC_009_the_three_facts_replay_into_the_terminal_state', () => {
  const folded = foldFacts([handleFact.linked, handleFact.completed, handleFact.retired])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const handles = fold.session(folded.value, 'ses_p').Handles

  assert.deepEqual(handleProjection.read(handleProjection.tryFind(HANDLE, handles)), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'fast-coder',
    role: 'Coder',
    lifecycle: 'Retired',
    creationOrder: 0,
    completion: 'Terminal',
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: undefined,
  })
  assert.deepEqual(views(handles), { listable: [], joinable: [], active: [] })
})

test('WHAT[MANAGED-SESSION-008] EXEC_009_a_replayed_completion_or_retirement_is_absorbed', () => {
  // The tombstone makes both idempotent, and a journal written across a restart
  // contains exactly these repeats. Rejecting them would refuse to boot.
  const folded = foldFacts([
    handleFact.linked,
    handleFact.completed,
    handleFact.completed,
    handleFact.retired,
    handleFact.retired,
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const handles = fold.session(folded.value, 'ses_p').Handles
  assert.equal(handleProjection.read(handleProjection.tryFind(HANDLE, handles)).lifecycle, 'Retired')
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_a_completion_for_a_handle_that_was_never_linked_stops_the_replay', () => {
  // No correct writer produces this: the link is what creates the handle. The
  // journal is incomplete, so booting from it would build state on absent facts.
  const folded = foldFacts([handleFact.completed])

  assert.equal(folded.ok, false)
  assert.equal(folded.error.Fact, 'HandleCompleted')
  assert.equal(folded.error.Reason, 'handle completion or retirement for a handle that was never linked')
})

test('WHAT[MANAGED-SESSION-008] EXEC_004_a_retirement_without_a_completion_stops_the_replay', () => {
  // `retire` is join's write, and join consumes a completion. A tombstone with no
  // completion means a handle was discarded while its child was still running.
  const folded = foldFacts([handleFact.linked, handleFact.retired])

  assert.equal(folded.ok, false)
  assert.equal(folded.error.Fact, 'HandleRetired')
  assert.equal(folded.error.Reason, 'join retired a handle that had no completion (EXEC-004)')

  // The two fatal reasons must read differently: one sends an operator looking
  // for a missing link, the other for a missing completion.
  assert.notEqual(folded.error.Reason, foldFacts([handleFact.completed]).error.Reason)
})

// ── EXEC-001: fork creates a child run and list / join show its lifecycle ─────

test('WHAT[MANAGED-SESSION-006] EXEC_001_fork_creates_a_child_run', () => {
  const active = linkOn(handleProjection.empty)

  assert.deepEqual(views(active), { listable: ['agent:h1'], joinable: [], active: ['agent:h1'] })
  assert.deepEqual(stateOf(active), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'fast-coder',
    role: 'Coder',
    lifecycle: 'Active',
    creationOrder: 0,
    completion: undefined,
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: undefined,
  })

  const completed = completeOn(active, { kind: 'Terminal' })
  assert.deepEqual(views(completed), { listable: ['agent:h1'], joinable: ['agent:h1'], active: [] })

  const joined = retireOn(completed)
  assert.deepEqual(views(joined), { listable: [], joinable: [], active: [] })
  assert.equal(handleProjection.isRetired(HANDLE, joined), true)
})

// ── EXEC-007: a nudge to an active handle does not create a second run ─────────

test('WHAT[MANAGED-SESSION-006] EXEC_007_nudge_is_fire_and_forget', () => {
  const active = linkOn(handleProjection.empty)
  const nudged = linkOn(active, { child: CHILD, agent: 'fast-coder', role: 'Coder' })

  // A nudge re-uses the same handle; it does not add a new child, listener,
  // or completion cell.
  assert.deepEqual(views(nudged).active, ['agent:h1'])
  assert.equal(handleProjection.linkedChildren(nudged).length, 1)
  assert.deepEqual(stateOf(nudged).child, 'ses_c')
})

// ── EXEC-009: durable completion payload on the lifecycle ────────────────────

test('WHAT[MANAGED-SESSION-007] EXEC_009_completed_awaiting_join_carries_blob_refs', () => {
  const completed = completeOn(linkOn(handleProjection.empty), {
    kind: 'Terminal',
    ref: blobRef('blobs/completion-h1'),
    digest: blobDigest('sha-completion-h1'),
  })

  assert.deepEqual(stateOf(completed), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'fast-coder',
    role: 'Coder',
    lifecycle: 'CompletedAwaitingJoin',
    creationOrder: 0,
    completion: 'Terminal',
    completionRef: 'blobs/completion-h1',
    completionDigest: 'sha-completion-h1',
    abandonReason: undefined,
  })
  assert.deepEqual(views(completed).joinable, ['agent:h1'])
})

test('WHAT[MANAGED-SESSION-007] EXEC_009_cancelled_completion_has_no_blob', () => {
  const cancelled = completeOn(linkOn(handleProjection.empty), { kind: 'Cancelled' })
  assert.deepEqual(
    {
      lifecycle: stateOf(cancelled).lifecycle,
      completion: stateOf(cancelled).completion,
      completionRef: stateOf(cancelled).completionRef,
      completionDigest: stateOf(cancelled).completionDigest,
    },
    {
      lifecycle: 'CompletedAwaitingJoin',
      completion: 'Cancelled',
      completionRef: undefined,
      completionDigest: undefined,
    },
  )
})

test('WHAT[MANAGED-SESSION-007] EXEC_009_fold_replays_completion_blob_refs', () => {
  const folded = foldFacts([handleFact.linked, handleFact.completedWithBlob])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const handles = fold.session(folded.value, 'ses_p').Handles
  assert.deepEqual(stateOf(handles), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'fast-coder',
    role: 'Coder',
    lifecycle: 'CompletedAwaitingJoin',
    creationOrder: 0,
    completion: 'Terminal',
    completionRef: 'blobs/completion-h1',
    completionDigest: 'sha-completion-h1',
    abandonReason: undefined,
  })
})

test('WHAT[MANAGED-SESSION-007] EXEC_009_codec_migrates_0_5_1_handle_completed_missing_blob_fields', () => {
  // 0.5.1 lines lack CompletionRef/CompletionDigest. Decode must inject None rather
  // than refuse the journal — forward-compat for in-flight 0.5.1 runtimes.
  const modern = fact('HandleCompleted', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Kind: completionKind.of('Terminal'),
    CompletionRef: undefined,
    CompletionDigest: undefined,
  })
  const modernLine = journal.serializeFact(modern)
  const modernDecoded = journal.deserializeFact(modernLine)
  assert.equal(modernDecoded.ok, true, modernDecoded.ok ? '' : modernDecoded.error)

  // Strip the new keys to simulate a 0.5.1 line, then migrate on read.
  const stripped = modernLine
    .replace(/,"CompletionRef":null/g, '')
    .replace(/,"CompletionDigest":null/g, '')
    .replace(/"CompletionRef":null,/g, '')
    .replace(/"CompletionDigest":null,/g, '')
  assert.equal(stripped.includes('CompletionRef'), false, 'fixture must lack CompletionRef')
  assert.equal(stripped.includes('CompletionDigest'), false, 'fixture must lack CompletionDigest')
  const migrated = journal.deserializeFact(stripped)
  assert.equal(migrated.ok, true, migrated.ok ? '' : migrated.error)

  // Fold the migrated fact: missing refs become None, handle is still joinable.
  const folded = foldFacts([handleFact.linked, migrated.value])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = stateOf(fold.session(folded.value, 'ses_p').Handles)
  assert.equal(state.lifecycle, 'CompletedAwaitingJoin')
  assert.equal(state.completion, 'Terminal')
  assert.equal(state.completionRef, undefined)
  assert.equal(state.completionDigest, undefined)
})
