import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const FactCodecSurface = await import("../../../dist/Persistence/Journal/FactCodecSurface.js");
const HandleFoldSurface = await import("../../../dist/Execution/Delegation/Handle/FoldSurface.js");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");
const HandleJournalSurface = await import("../../../dist/Execution/Delegation/Handle/JournalSurface.js");
const { withAdmittedChildren } = await import('./support/admitted-child-work.mjs')
const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
const journalSurface = await import('../../../dist/Persistence/Journal/Surface.js')
const { withJournal, hostPort } = await import('../../interaction-authority/tests/support/authority.mjs')
const { assertJsData } = await import('../../verification-system/tests/support/js-contract.mjs')

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

test('WHAT[managed-session-lifecycle-009] EXEC_009_HandleAbandoned_serializes_round_trip', () => {
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
test('WHAT[managed-session-lifecycle-009] EXEC_009_Active_to_Abandoned_fold_and_projection', () => {
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
test('WHAT[managed-session-lifecycle-009] EXEC_009_CompletedAwaitingJoin_can_abandon', () => {
  const abandoned = abandonOn(completeOn(linkOn(HandleSurface.empty())), { reason: 'DeadlineExceeded' })
  assert.equal(stateOf(abandoned).lifecycle, 'Abandoned')
  assert.equal(stateOf(abandoned).abandonReason, 'DeadlineExceeded')
  assert.deepEqual(views(abandoned).joinable, [])
  assert.equal(HandleSurface.reportableAbandonedCount(abandoned), 1)
})
test('WHAT[managed-session-lifecycle-009] EXEC_009_Abandoned_is_not_joinable_and_cannot_complete', () => {
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
  const replayed = HandleSurface.apply(abandoned, { op: 'link', handle: HANDLE, child: CHILD, agent: 'coder', role: 'Coder' })
  assert.equal(replayed.ok, true)
  assert.deepEqual(stateOf(replayed.state), stateOf(abandoned), 'stable binding replay preserves the abandonment')
  const reopened = HandleSurface.apply(retired.state, { op: 'link', handle: HANDLE, child: CHILD, agent: 'coder', role: 'Coder' })
  assert.equal(reopened.ok, true)
  assert.deepEqual(stateOf(reopened.state), stateOf(retired.state), 'binding replay cannot revive a retired work')
})
test('WHAT[managed-session-lifecycle-009] EXEC_009_recordAbandon_CAS_first_wins', async () => {
  await withAdmittedChildren('abandon-direct', PARENT, [{ agentId: 'h1', sessionId: CHILD, role: 'engineer' }], async (j, profiles, directory, reopen) => {
    const root = profiles.get('h1').authorityRoot
    const first = await HandleJournalSurface.recordAbandon(j, PARENT, 'h1', CHILD, root, 'ParentCancelled')
    assert.equal(first.ok, true, first.ok ? '' : first.error)
    const before = HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, root)
    const second = await HandleJournalSurface.recordAbandon(j, PARENT, 'h1', CHILD, root, 'DeadlineExceeded')
    assert.equal(second.ok, true, second.ok ? '' : second.error)
    const projection = HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, root)
    assert.equal(projection.record.lifecycle, 'Abandoned')
    assert.equal(projection.record.abandonReason, 'ParentCancelled')
    assert.deepEqual(projection.views.joinable, [])
    assert.equal(projection.revision, before.revision, 'same exact work retry must not append a second terminal fact')
    const rejected = await HandleJournalSurface.recordAbandon(j, PARENT, 'h1', 'foreign-child', root, 'HostSessionGone')
    assert.deepEqual(rejected, { ok: false, error: 'WorkNotAdmitted' })
    assert.deepEqual(HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, root), projection)
    const sent = await dispatch.sendAgentOwnerRootAwait(hostPort(async () => dispatch.admittedWithReceipt('new-child-work')), j, CHILD, 'SECOND-CHARGE', profiles.get('h1').identitySeed)
    assert.equal(sent.ok, true, sent.error)
    const accepted = await dispatch.acceptAgentOwnerRoot(j, CHILD, sent.key, 'physical-second-child-charge')
    assert.equal(accepted.ok, true, JSON.stringify(accepted.error))
    const nextRoot = accepted.profile.authorityRoot
    const nextBefore = HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, nextRoot)
    assert.equal(nextBefore.record.lifecycle, 'Active')
    assert.equal((await HandleJournalSurface.recordAbandon(j, PARENT, 'h1', CHILD, root, 'HostSessionGone')).ok, true)
    assert.deepEqual(HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, nextRoot), nextBefore, 'old work retry cannot affect the next work or append another fact')
    assert.deepEqual(HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, root).record, projection.record)
    const reopened = await reopen()
    assert.deepEqual(HandleJournalSurface.snapshot(reopened, PARENT, 'h1', CHILD, root).record, projection.record)
    assert.equal(HandleJournalSurface.snapshot(reopened, PARENT, 'h1', CHILD, nextRoot).record.lifecycle, 'Active')
  })
})
test('WHAT[managed-session-lifecycle-009] an unscoped historical binding cannot authorize a new abandonment', async () => {
  await withJournal('historical-abandon-rejected', async j => {
    const linked = await journalSurface.JournalSurface_appendAgent(j, { kind: 'Session', session: PARENT }, null, {
      family: 'Execution', case: 'HandleLinked', payload: {
        ParentSessionId: PARENT, ChildSessionId: CHILD, Handle: 'h1', TargetAgent: 'engineer',
        Byname: 'h1', CanonicalRole: 'Engineer', Ownership: 'DurableParentHandle',
      },
    })
    assert.equal(linked.ok, true)
    const before = HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, 'untrusted-root-material')
    assert.equal(before.record, null, 'a stable road is not a scoped admitted work')
    assert.deepEqual(await HandleJournalSurface.recordAbandon(j, PARENT, 'h1', CHILD, 'untrusted-root-material', 'ParentCancelled'), { ok: false, error: 'WorkNotAdmitted' })
    assert.deepEqual(HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, 'untrusted-root-material'), before, 'rejection writes nothing and grants no authority')
  })
})
test('WHAT[managed-session-lifecycle-009] public scoped work snapshot is lossless JSON data', async () => {
  await withAdmittedChildren('work-snapshot-json', PARENT, [{ agentId: 'h1', sessionId: CHILD, role: 'engineer' }], async (j, profiles) => {
    const observed = HandleJournalSurface.snapshot(j, PARENT, 'h1', CHILD, profiles.get('h1').authorityRoot)
    assertJsData(observed, 'scoped work snapshot')
    assert.doesNotThrow(() => JSON.stringify(observed), 'public snapshot must serialize without compiler values or BigInt')
    assert.match(observed.revision, /^(0|[1-9]\d*)$/, 'revision retains the exact canonical decimal value')
    assert.deepEqual(JSON.parse(JSON.stringify(observed)), {
      ...observed, record: { lifecycle: 'Active', child: CHILD },
    })
  })
})
test('WHAT[managed-session-lifecycle-009] EXEC_009_fold_replays_HandleAbandoned_idempotent', () => {
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
test('WHAT[managed-session-lifecycle-009] EXEC_009_retire_tombstone_unaffected_by_abandon_path', () => {
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
test('WHAT[managed-session-lifecycle-009] EXEC_009_projection_CAS_duplicate_abandon_refused', () => {
  const abandoned = abandonOn(linkOn(HandleSurface.empty()))
  assert.deepEqual(HandleSurface.apply(abandoned, { op: 'abandon', handle: HANDLE, reason: 'DeadlineExceeded' }).error, {
    kind: 'TransitionRejected', reason: 'AlreadyAbandoned',
  })
  assert.equal(stateOf(abandoned).abandonReason, 'ParentCancelled')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");
const HandleFoldSurface = await import("../../../dist/Execution/Delegation/Handle/FoldSurface.js");

const blobRef = (value) => String(value)
const blobDigest = (value) => String(value)
const sessionId = (value) => String(value)
const roles = { of: (value) => String(value) }
const handleOwnership = {
  durableParentHandle: () => 'DurableParentHandle',
  hostOwnedHidden: () => 'HostOwnedHidden',
}
const completionKind = { of: (value) => String(value) }
const isSome = (value) => value !== undefined && value !== null
const fact = (caseName, payload) => ({ case: caseName, payload })
const envelope = ({ seq, stream, fact: value }) => ({ seq, stream, fact: value })
const stream = { session: (value) => `session:${value}` }
const handleId = {
  agent: HandleSurface.handleIdAgent,
  pty: HandleSurface.handleIdPty,
  managerJob: HandleSurface.handleIdManagerJob,
  describe: HandleSurface.handleIdDescribe,
  tryAgent: HandleSurface.handleIdTryAgent,
}
const PARENT = sessionId('ses_p')
const CHILD = sessionId('ses_c')
const HANDLE = handleId.agent('h1')
const linkOn = (state, { handle = HANDLE, child = CHILD, agent = 'coder', role = 'Coder' } = {}) => {
  const applied = HandleSurface.apply(state, { op: 'link', handle, child, agent, role })
  assert.equal(applied.ok, true, applied.ok ? '' : `link refused: ${JSON.stringify(applied.error)}`)
  return applied.state
}
const completeOn = (state, { handle = HANDLE, kind = 'Terminal', ref, digest } = {}) => {
  const command = { op: 'complete', handle, kind }
  if (ref !== undefined) command.ref = ref
  if (digest !== undefined) command.digest = digest
  const applied = HandleSurface.apply(state, command)
  assert.equal(applied.ok, true, applied.ok ? '' : `complete refused: ${JSON.stringify(applied.error)}`)
  return applied.state
}
const abandonOn = (state, { handle = HANDLE, reason = 'ParentCancelled' } = {}) => {
  const applied = HandleSurface.apply(state, { op: 'abandon', handle, reason })
  assert.equal(applied.ok, true, applied.ok ? '' : `abandon refused: ${JSON.stringify(applied.error)}`)
  return applied.state
}
const retireOn = (state, { handle = HANDLE } = {}) => {
  const applied = HandleSurface.apply(state, { op: 'retire', handle })
  assert.equal(applied.ok, true, applied.ok ? '' : `retire refused: ${JSON.stringify(applied.error)}`)
  return applied.state
}
const stateOf = (state, handle = HANDLE) => HandleSurface.read(state, handle)
const views = (state) => {
  const v = HandleSurface.views(state)
  return { listable: [...v.listable].sort(), joinable: [...v.joinable].sort(), active: [...v.active].sort() }
}
const handleFact = {
  linked: fact('HandleLinked', {
    ParentSessionId: PARENT,
    ChildSessionId: CHILD,
    Handle: HANDLE,
    TargetAgent: 'coder',
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
  HandleFoldSurface.foldApply(
    HandleFoldSurface.foldEmpty(),
    facts.map((value, index) => envelope({ seq: index + 1, stream: stream.session(PARENT), fact: value })),
  )
const foldStateOf = (folded, handle = HANDLE) => {
  const handles = HandleFoldSurface.foldSession(folded.state, 'ses_p')
  return HandleSurface.read(handles, handle)
}
const foldViews = (folded) => {
  const handles = HandleFoldSurface.foldSession(folded.state, 'ses_p')
  return views(handles)
}

test('WHAT[managed-session-lifecycle-009] EXEC_009_parent_abort_needs_the_handles_themselves_not_a_count', () => {
  // "Cancel every owned physical resource individually" is only expressible if
  // the caller gets the ids. A count would force it to guess which ones.
  let state = linkOn(HandleSurface.empty(), { handle: handleId.agent('a'), child: sessionId('ses_1') })
  state = linkOn(state, { handle: handleId.agent('b'), child: sessionId('ses_2') })
  state = linkOn(state, { handle: handleId.pty('p'), child: sessionId('ses_3'), role: 'DevOps' })

  // One has already completed, so it is no longer an active resource.
  state = completeOn(state, { handle: handleId.agent('b') })

  assert.deepEqual(views(state).active, ['agent:a', 'pty:p'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const link = (projection, agentId, child, targetAgent = 'coder') => {
  const result = HandleSurface.apply(projection, {
    op: 'link', handle: `agent:${agentId}`, child, agent: targetAgent, role: 'Coder',
  })
  assert.equal(result.ok, true)
  return result.state
}

test('WHAT[managed-session-lifecycle-009] EXEC_009_abandoned_retire_clears_reportable_single_report', () => {
  let projection = link(HandleSurface.empty(), 'h1', 'ses_c')
  projection = HandleSurface.apply(projection, { op: 'abandon', handle: 'agent:h1', reason: 'ParentCancelled' }).state
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 1)
  projection = HandleSurface.apply(projection, { op: 'retire', handle: 'agent:h1' }).state
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 0)
  assert.equal(HandleSurface.isRetired(projection, 'agent:h1'), true)
})
}

test.todo('WHAT[managed-session-lifecycle-009] authorized parent termination awaits every durable abandonment and held physical child cleanup before publishing parent terminal (GAP-133)')

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const SyncDelegateSurface = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

const owner = 'managed-session-owner'
const create = async () => SyncDelegateSurface.create(
  await mkdtemp(join(tmpdir(), 'wxs-managed-sync-')),
  [{ sessionId: owner, agent: 'manager' }],
)
const admit = async (runtime, count) => {
  await SyncDelegateSurface.awaitPromptCount(runtime, owner, 'Engineer', count)
  assert.equal(SyncDelegateSurface.acceptPrompt(runtime, owner, 'Engineer', count - 1), true)
}
const invokeAndSettle = async (runtime, charge, answer, promptCount, runId) => {
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Engineer', charge)
  await admit(runtime, promptCount)
  assert.equal(await SyncDelegateSurface.settle(runtime, owner, 'Engineer', answer, runId), true)
  const result = await pending
  assert.equal(result.ok, true)
  return result
}

test('WHAT[managed-session-lifecycle-009] G2_delegate_cancel_owner_fails_pending_invoke_no_extra_child', async () => {
  const runtime = await create()
  const pending = SyncDelegateSurface.invoke(runtime, owner, 'Engineer', 'pending')
  await admit(runtime, 1)
  SyncDelegateSurface.cancelSession(runtime, owner)
  assert.deepEqual(await pending, { ok: false, error: 'Sync delegate call was cancelled' })
  assert.equal(SyncDelegateSurface.child(runtime, owner, 'Engineer'), null)
  assert.equal(SyncDelegateSurface.childCount(runtime), 1)
  SyncDelegateSurface.dispose(runtime)
})
}
