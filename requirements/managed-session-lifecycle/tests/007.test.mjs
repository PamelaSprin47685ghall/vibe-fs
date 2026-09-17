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

test('WHAT[MANAGED-SESSION-007] LOOP_optional_string_traversal_calls_extract_zero_for_None_once_for_Some_and_propagates_failure', () => {
  const calls = []
  const extract = (value) => {
    calls.push(value)
    return `projected:${value}`
  }

  assert.equal(HandleSurface.optionalStringTraversal(false, 'absent', extract), undefined)
  assert.deepEqual(calls, [], 'None must not invoke extract')

  assert.equal(HandleSurface.optionalStringTraversal(true, 'present', extract), 'projected:present')
  assert.deepEqual(calls, ['present'], 'Some must invoke extract exactly once with its value')

  const failure = new Error('extract failed')
  assert.throws(
    () =>
      HandleSurface.optionalStringTraversal(true, 'failing', (value) => {
        calls.push(value)
        throw failure
      }),
    (error) => error === failure,
  )
  assert.deepEqual(calls, ['present', 'failing'], 'extract failure propagates after exactly one invocation')
})
test('WHAT[MANAGED-SESSION-007] EXEC_004_the_first_completion_wins_and_later_ones_are_refused', () => {
  // Terminal, send-failure and cancel race for one cell. The loser must be
  // REFUSED rather than overwrite the winner, or a cancelled child could report
  // the terminal it never reached.
  const completed = completeOn(linkOn(HandleSurface.empty()), { kind: 'Terminal' })
  assert.equal(stateOf(completed).completion, 'Terminal')

  for (const late of ['SendFailure', 'Cancelled', 'Terminal']) {
    assert.deepEqual(
      HandleSurface.apply(completed, { op: 'complete', handle: HANDLE, kind: late }),
      { ok: false, error: { kind: 'TransitionRejected', reason: 'AlreadyCompleted' } },
      `a late ${late} must not overwrite the first winner`,
    )
  }

  assert.equal(stateOf(completed).completion, 'Terminal', 'the winner is unchanged')
})
test('WHAT[MANAGED-SESSION-007] EXEC_004_each_completion_kind_survives_into_the_state', () => {
  // EXEC-005 requires `list` to say WHICH completion landed, so the kind is part
  // of the lifecycle state rather than a boolean beside it.
  for (const kind of ['Terminal', 'SendFailure', 'Cancelled']) {
    const completed = completeOn(linkOn(HandleSurface.empty()), { kind })
    assert.deepEqual(
      { lifecycle: stateOf(completed).lifecycle, completion: stateOf(completed).completion },
      { lifecycle: 'CompletedAwaitingJoin', completion: kind },
    )
  }
})
test('WHAT[MANAGED-SESSION-007] EXEC_004_completing_an_unknown_handle_is_refused_by_name', () => {
  const state = linkOn(HandleSurface.empty())

  assert.deepEqual(
    HandleSurface.apply(state, { op: 'complete', handle: handleId.agent('never'), kind: 'Terminal' }),
    { ok: false, error: { kind: 'TransitionRejected', reason: 'UnknownHandle' } },
  )
})
test('WHAT[MANAGED-SESSION-007] EXEC_009_completed_awaiting_join_carries_blob_refs', () => {
  const completed = completeOn(linkOn(HandleSurface.empty()), {
    kind: 'Terminal',
    ref: blobRef('blobs/completion-h1'),
    digest: blobDigest('sha-completion-h1'),
  })

  assert.deepEqual(stateOf(completed), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'coder',
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
  const cancelled = completeOn(linkOn(HandleSurface.empty()), { kind: 'Cancelled' })
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
  assert.deepEqual(foldStateOf(folded), {
    handle: 'agent:h1',
    child: 'ses_c',
    targetAgent: 'coder',
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
  const modernLine = HandleSurface.serializeFact(modern)
  const modernDecoded = HandleSurface.deserializeFact(modernLine)
  assert.equal(modernDecoded.ok, true, modernDecoded.ok ? '' : modernDecoded.error)

  // Strip the new keys to simulate a 0.5.1 line, then migrate on read.
  const stripped = modernLine
    .replace(/,"CompletionRef":null/g, '')
    .replace(/,"CompletionDigest":null/g, '')
    .replace(/"CompletionRef":null,/g, '')
    .replace(/"CompletionDigest":null,/g, '')
  assert.equal(stripped.includes('CompletionRef'), false, 'fixture must lack CompletionRef')
  assert.equal(stripped.includes('CompletionDigest'), false, 'fixture must lack CompletionDigest')
  const migrated = HandleSurface.deserializeFact(stripped)
  assert.equal(migrated.ok, true, migrated.ok ? '' : migrated.error)

  // Fold the migrated fact: missing refs become None, handle is still joinable.
  const folded = foldFacts([handleFact.linked, migrated.value])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = foldStateOf(folded)
  assert.equal(state.lifecycle, 'CompletedAwaitingJoin')
  assert.equal(state.completion, 'Terminal')
  assert.equal(state.completionRef, undefined)
  assert.equal(state.completionDigest, undefined)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const completionKind = fc.constantFrom('Terminal', 'SendFailure', 'Cancelled')
const token = fc
  .array(fc.constantFrom(...'abcdefghijklmnopqrstuvwxyz0123456789'), { minLength: 1, maxLength: 24 })
  .map((characters) => characters.join(''))
const handleKind = fc.constantFrom('agent', 'pty', 'manager-job')
const completionRace = fc.array(completionKind, { minLength: 2, maxLength: 64 })
const propertyOptions = { seed: 0x4d534c07, numRuns: 1_000 }
const makeHandle = (kind, value) => {
  if (kind === 'agent') return handles.handleIdAgent(value)
  if (kind === 'pty') return handles.handleIdPty(value)
  return handles.handleIdManagerJob(value)
}
const link = (state, handle, child) => {
  const result = handles.apply(state, {
    op: 'link',
    handle,
    child,
    agent: 'coder',
    role: 'Coder',
  })
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.state
}
const assertLateCompletionRejected = (result) => {
  assert.deepEqual(result, {
    ok: false,
    error: { kind: 'TransitionRejected', reason: 'AlreadyCompleted' },
  })
}

test('WHAT[MANAGED-SESSION-007] every completion race preserves the first production winner', () => {
  const empty = handles.empty()

  fc.assert(
    fc.property(handleKind, token, completionRace, (kind, suffix, arrivals) => {
      const winnerHandle = makeHandle(kind, `winner-${suffix}`)
      const decoyHandle = makeHandle(kind, `decoy-${suffix}`)
      let initial = link(empty, winnerHandle, `winner-child-${suffix}`)
      initial = link(initial, decoyHandle, `decoy-child-${suffix}`)
      const initialWinner = handles.read(initial, winnerHandle)
      const initialDecoy = handles.read(initial, decoyHandle)

      const first = handles.apply(initial, {
        op: 'complete',
        handle: winnerHandle,
        kind: arrivals[0],
      })
      assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
      assert.deepEqual(
        {
          lifecycle: handles.read(first.state, winnerHandle).lifecycle,
          completion: handles.read(first.state, winnerHandle).completion,
        },
        { lifecycle: 'CompletedAwaitingJoin', completion: arrivals[0] },
      )
      assert.deepEqual(handles.read(initial, winnerHandle), initialWinner)
      assert.deepEqual(handles.read(first.state, decoyHandle), initialDecoy)

      for (const late of arrivals.slice(1)) {
        assertLateCompletionRejected(
          handles.apply(first.state, {
            op: 'complete',
            handle: winnerHandle,
            kind: late,
          }),
        )
        assert.equal(handles.read(first.state, winnerHandle).completion, arrivals[0])
        assert.deepEqual(handles.read(first.state, decoyHandle), initialDecoy)
      }
    }),
    propertyOptions,
  )
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

test('WHAT[MANAGED-SESSION-007] THEOREM_handle_completed_causally_awakens_joinable', () => {
  const completed = complete(makeActive())
  assert.equal(completed.ok, true)
  assert.deepEqual(HandleSurface.views(completed.state).joinable, ['agent:c1'])
})
test('WHAT[MANAGED-SESSION-007] THEOREM_join_wake_path_trace_WorkActivated_then_HandleCompleted', () => {
  const folded = HandleFoldSurface.foldApply(HandleFoldSurface.foldEmpty(), [
    { fact: { case: 'HandleLinked', payload: { ParentSessionId: 'ses_parent', ChildSessionId: 'ses_child', Handle: 'agent:c1', TargetAgent: 'coder', CanonicalRole: 'Coder', Ownership: 'DurableParentHandle' } } },
    { fact: { case: 'HandleCompleted', payload: { ParentSessionId: 'ses_parent', Handle: 'agent:c1', Kind: 'Terminal' } } },
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const projection = HandleFoldSurface.foldSession(folded.state, 'ses_parent')
  assert.deepEqual(HandleSurface.views(projection).joinable, ['agent:c1'])
})
}
