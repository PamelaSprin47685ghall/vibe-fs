import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const FactCodecSurface = await import("../../../dist/Persistence/Journal/FactCodecSurface.js");
const HandleFoldSurface = await import("../../../dist/Execution/Delegation/Handle/FoldSurface.js");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");
const HandleJournalSurface = await import("../../../dist/Execution/Delegation/Handle/JournalSurface.js");

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
test('WHAT[managed-session-lifecycle-009] EXEC_009_recordAbandon_CAS_first_wins', async (context) => {
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

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[managed-session-lifecycle-009] provider transform is admitted into plugin shutdown ownership', () => {
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')
  const hooks = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')
  const interop = read('src/Wanxiangshu/OpenCode/Host/PluginHostInterop.fs')
  const policy = read('src/Wanxiangshu/OpenCode/Host/HookPolicy.fs')

  assert.match(scope, /member this\.RunOwnedWork\(start: unit -> Task\) : Task/)
  assert.match(scope, /let ownedWorkDrain = this\.StopOwnedWorkAndDrain\(\)/)
  assert.match(hooks, /let ownedTransform[\s\S]{0,220}scope\.RunOwnedWork\(fun \(\) -> transform inObj outObj\)/)
  assert.match(
    hooks,
    /let messagesTransform\s*=\s*registeredHook HookKey\.MessagesTransform \(curriedHook \(box ownedTransform\)\)/,
  )
  assert.match(
    policy,
    /\| HookKey\.MessagesTransform ->\s*\{ HostKey = "experimental\.chat\.messages\.transform"\s*DiagnosticOperation = "plugin-hook-messages-transform-failed"/,
  )
  assert.match(
    interop,
    /let registeredHook \(key: HookKey\) \(adaptedHook: obj\) : string \* obj =\s*let metadata = HookPolicy\.metadata key \|> HookPolicy\.validate\s*metadata\.HostKey, policyAwareHook metadata\.DiagnosticOperation adaptedHook/,
  )
})
}

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
