import test from 'node:test'

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

test('WHAT[MANAGED-SESSION-006] EXEC_009_agent_pty_and_manager_job_handles_are_separate_identities', () => {
  // The same string in three handle kinds must be three map keys. Collapsing them
  // to the raw string would let retiring an agent handle retire the PTY that
  // happens to share its id.
  let state = linkOn(HandleSurface.empty(), { handle: handleId.agent('x'), child: sessionId('ses_a') })
  state = linkOn(state, {
    handle: handleId.pty('x'),
    child: sessionId('ses_b'),
    agent: 'devops',
    role: 'DevOps',
  })
  state = linkOn(state, {
    handle: handleId.managerJob('x'),
    child: sessionId('ses_j'),
    agent: 'manager',
    role: 'Manager',
  })

  assert.deepEqual(views(state).active, ['agent:x', 'manager-job:x', 'pty:x'])

  // Retiring one leaves the other two untouched.
  const retired = retireOn(completeOn(state, { handle: handleId.agent('x') }), { handle: handleId.agent('x') })

  assert.equal(HandleSurface.isRetired(retired, handleId.agent('x')), true)
  assert.equal(HandleSurface.isRetired(retired, handleId.pty('x')), false)
  assert.equal(stateOf(retired, handleId.pty('x')).lifecycle, 'Active')
})
test('WHAT[MANAGED-SESSION-006] EXEC_005_the_views_partition_the_lifecycle_and_never_show_retired', () => {
  const active = linkOn(HandleSurface.empty())
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
test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_handle_answers_retired_forever', () => {
  const retired = retireOn(completeOn(linkOn(HandleSurface.empty())))

  assert.equal(HandleSurface.isRetired(retired, HANDLE), true)
  assert.equal(stateOf(retired).lifecycle, 'Retired')

  // Every transition is refused from here, including a second retirement.
  assert.deepEqual(
    HandleSurface.apply(retired, { op: 'retire', handle: HANDLE }),
    { ok: false, error: { kind: 'TransitionRejected', reason: 'HandleIsRetired' } },
  )
  assert.deepEqual(
    HandleSurface.apply(retired, { op: 'complete', handle: HANDLE, kind: 'Terminal' }),
    { ok: false, error: { kind: 'TransitionRejected', reason: 'HandleIsRetired' } },
  )
  const reopened = HandleSurface.apply(retired, {
    op: 'link',
    handle: HANDLE,
    child: CHILD,
    agent: 'coder',
    role: 'Coder',
  })
  assert.equal(reopened.ok, true, `same binding must accept a new work unit: ${JSON.stringify(reopened)}`)
  assert.equal(HandleSurface.isRetired(reopened.ok ? reopened.state : retired, HANDLE), false)
  assert.equal(stateOf(reopened.ok ? reopened.state : retired).lifecycle, 'Active')
})
test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_id_is_distinguishable_from_one_that_never_existed', () => {
  // The exact confusion the tombstone prevents. If the record were deleted on
  // retire, these two lookups would be identical and `fork` would treat a spent
  // handle id as an agent name.
  const retired = retireOn(completeOn(linkOn(HandleSurface.empty())))

  assert.equal(isSome(HandleSurface.tryFind(retired, HANDLE)), true)
  assert.equal(isSome(HandleSurface.tryFind(retired, handleId.agent('never'))), false)

  assert.deepEqual(
    {
      retiredId: HandleSurface.isRetired(retired, HANDLE),
      unknownId: HandleSurface.isRetired(retired, handleId.agent('never')),
    },
    { retiredId: true, unknownId: false },
  )
})
test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_child_session_is_still_recognised_as_a_child', () => {
  // "Is this session one of mine" must answer yes for a child that already
  // finished — otherwise a late event from it looks like it came from a stranger.
  const retired = retireOn(completeOn(linkOn(HandleSurface.empty())))
  const found = HandleSurface.tryFindByChildSession(retired, CHILD)

  assert.equal(isSome(found), true)
  assert.equal(HandleSurface.read(retired, HANDLE).lifecycle, 'Retired')
  assert.equal(isSome(HandleSurface.tryFindByChildSession(retired, sessionId('ses_other'))), false)
})
test('WHAT[MANAGED-SESSION-006] EXEC_009_linked_children_lists_every_child_ever_linked', () => {
  // Replaces the old live-only `LinkedChildren` map, which forced restart
  // recovery and the retired-handle check to use two different structures.
  let state = linkOn(HandleSurface.empty(), { handle: handleId.agent('z'), child: sessionId('ses_1') })
  state = linkOn(state, { handle: handleId.agent('a'), child: sessionId('ses_2') })
  state = retireOn(completeOn(state, { handle: handleId.agent('z') }), { handle: handleId.agent('z') })

  assert.deepEqual(
    HandleSurface.linkedChildren(state).map((r) => r.child),
    ['ses_1', 'ses_2'],
    'linked children retain creation order rather than handle-key order',
  )
  assert.deepEqual(
    HandleSurface.linkedChildren(state).map((r) => r.creationOrder),
    [0, 1],
  )
})
test('WHAT[MANAGED-SESSION-006] EXEC_009_the_three_facts_replay_into_the_terminal_state', () => {
  const folded = foldFacts([handleFact.linked, handleFact.completed, handleFact.retired])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  assert.deepEqual(foldStateOf(folded), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'coder',
    role: 'Coder',
    lifecycle: 'Retired',
    creationOrder: 0,
    completion: 'Terminal',
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: undefined,
  })
  assert.deepEqual(foldViews(folded), { listable: [], joinable: [], active: [] })
})
test('WHAT[MANAGED-SESSION-006] EXEC_001_fork_creates_a_child_run', () => {
  const active = linkOn(HandleSurface.empty())

  assert.deepEqual(views(active), { listable: ['agent:h1'], joinable: [], active: ['agent:h1'] })
  assert.deepEqual(stateOf(active), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'coder',
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
  assert.equal(HandleSurface.isRetired(joined, HANDLE), true)
})
test('WHAT[MANAGED-SESSION-006] EXEC_007_nudge_is_fire_and_forget', () => {
  const active = linkOn(HandleSurface.empty())
  const nudged = linkOn(active, { child: CHILD, agent: 'coder', role: 'Coder' })

  // A nudge re-uses the same handle; it does not add a new child, listener,
  // or completion cell.
  assert.deepEqual(views(nudged).active, ['agent:h1'])
  assert.equal(HandleSurface.linkedChildren(nudged).length, 1)
  assert.deepEqual(stateOf(nudged).child, 'ses_c')
})
test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_role', () => {
  const result = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link',
    handle: HANDLE,
    child: CHILD,
    agent: 'coder',
    role: 'Plumber',
  })
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'UnknownRole')
  assert.equal(result.error.value, 'Plumber')
})
test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_completion_kind', () => {
  const state = linkOn(HandleSurface.empty())
  const result = HandleSurface.apply(state, { op: 'complete', handle: HANDLE, kind: 'Exploded' })
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'UnknownCompletionKind')
  assert.equal(result.error.value, 'Exploded')
})
test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_abandon_reason', () => {
  const state = linkOn(HandleSurface.empty())
  const result = HandleSurface.apply(state, { op: 'abandon', handle: HANDLE, reason: 'Boredom' })
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'UnknownAbandonReason')
  assert.equal(result.error.value, 'Boredom')
})
test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_ownership', () => {
  const result = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link',
    handle: HANDLE,
    child: CHILD,
    agent: 'coder',
    role: 'Coder',
    ownership: 'AlienOwned',
  })
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'UnknownOwnership')
  assert.equal(result.error.value, 'AlienOwned')
})
test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_command_op', () => {
  const result = HandleSurface.apply(HandleSurface.empty(), { op: 'frobnicate', handle: HANDLE })
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'UnknownCommand')
  assert.equal(result.error.value, 'frobnicate')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const linkedProjection = () => {
  const linked = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link', handle: 'agent:child-1', child: 'ses_child', agent: 'coder', role: 'Coder',
  })
  assert.equal(linked.ok, true)
  return linked.state
}

test('WHAT[MANAGED-SESSION-006] EXEC_016_listable_handles_are_outstanding_for_manager', () => {
  const projection = linkedProjection()
  assert.deepEqual(HandleSurface.views(projection), { listable: ['agent:child-1'], joinable: [], active: ['agent:child-1'] })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HandleFoldSurface = await import("../../../dist/Execution/Delegation/Handle/FoldSurface.js");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const makeActive = () => {
  const result = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link', handle: 'agent:c1', child: 'ses_child', agent: 'coder', role: 'Coder',
  })
  assert.equal(result.ok, true)
  return result.state
}
const complete = (state) => HandleSurface.apply(state, { op: 'complete', handle: 'agent:c1', kind: 'Terminal' })
const retire = (state) => HandleSurface.apply(state, { op: 'retire', handle: 'agent:c1' })

test('WHAT[MANAGED-SESSION-006] THEOREM_join_blocked_while_handle_active', () => {
  const projection = makeActive()
  assert.deepEqual(HandleSurface.views(projection), { listable: ['agent:c1'], joinable: [], active: ['agent:c1'] })
})
test('WHAT[MANAGED-SESSION-006] THEOREM_WorkActivated_and_HandleLinked_interleavings_stay_blocked', () => {
  const active = makeActive()
  assert.deepEqual(HandleSurface.views(active), { listable: ['agent:c1'], joinable: [], active: ['agent:c1'] })
})
test('WHAT[MANAGED-SESSION-006] THEOREM_projection_steps_enumerate_blocked_then_awakened_then_clear', () => {
  const active = makeActive()
  const completed = complete(active)
  const retired = retire(completed.state)
  assert.deepEqual([
    HandleSurface.views(active).listable.length,
    HandleSurface.views(completed.state).joinable.length,
    HandleSurface.views(retired.state).listable.length,
  ], [1, 1, 0])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");
const TerminalPolicySurface = await import("../../../dist/OpenCode/Host/TerminalPolicySurface.js");


test('WHAT[MANAGED-SESSION-006] TPOL_sessionDead_false_without_journal', () => {
  assert.equal(TerminalPolicySurface.sessionDeadWithoutJournal('ses_main'), false)
})
test('WHAT[MANAGED-SESSION-006] TPOL_outstanding_without_durable_work_is_role_closed', () => {
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('Manager', false, 'ses_main'), false)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('DevOps', true, 'ses_devops'), true)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('Orchestrator', false, 'ses_orchestrator'), false)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('Coder', true, 'ses_coder'), false)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('unknown', true, 'ses_unknown'), false)
})
}
