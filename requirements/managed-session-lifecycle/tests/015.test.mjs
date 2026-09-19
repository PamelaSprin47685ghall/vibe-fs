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

test('WHAT[managed-session-lifecycle-015] EXEC_009_only_an_agent_handle_answers_the_agent_question', () => {
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
test('WHAT[managed-session-lifecycle-015] EXEC_009_a_linked_handle_records_the_child_session_it_drives', () => {
  // The field this pins was missing until package F: `HandleLinked` carried no
  // child SessionId, so eight consumers could not get from a handle to its child
  // and every read side of EXEC-009 was dangling.
  const state = linkOn(HandleSurface.empty())

  assert.deepEqual(stateOf(state), {
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
})
test('WHAT[managed-session-lifecycle-015] EXEC_009_replaying_the_exact_live_link_is_idempotent', () => {
  const state = linkOn(HandleSurface.empty())
  const relinked = linkOn(state)

  assert.equal(HandleSurface.linkedChildren(relinked).length, 1)
  assert.deepEqual(stateOf(relinked), stateOf(state))
})
test('WHAT[managed-session-lifecycle-015] EXEC_009_one_durable_handle_cannot_be_rebound_to_another_child', () => {
  const state = linkOn(HandleSurface.empty())

  assert.deepEqual(
    HandleSurface.apply(state, {
      op: 'link',
      handle: HANDLE,
      child: sessionId('ses_other'),
      agent: 'coder',
      role: 'Coder',
    }),
    { ok: false, error: { kind: 'TransitionRejected', reason: 'HandleIdentityConflict' } },
  )
  assert.equal(stateOf(state).child, 'ses_c')
})
test('WHAT[managed-session-lifecycle-015] EXEC_009_a_completion_for_a_handle_that_was_never_linked_stops_the_replay', () => {
  // No correct writer produces this: the link is what creates the handle. The
  // journal is incomplete, so booting from it would build state on absent facts.
  const folded = foldFacts([handleFact.completed])

  assert.equal(folded.ok, false)
  assert.equal(folded.error.Fact, 'HandleCompleted')
  assert.equal(folded.error.Reason, 'handle completion or retirement for a handle that was never linked')
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

test('WHAT[managed-session-lifecycle-015] EXEC_018_creation_order_follows_HandleLinked_fold_sequence', () => {
  const projection = link(link(link(HandleSurface.empty(), 'later-id-zzz', 'ses_z', 'zebra-agent'), 'earlier-id-aaa', 'ses_a', 'alpha-agent'), 'mid-id-mmm', 'ses_m', 'mid-agent')
  const children = HandleSurface.linkedChildren(projection)
  assert.equal(children.find((item) => item.handle === 'agent:later-id-zzz').creationOrder, 0)
  assert.equal(children.find((item) => item.handle === 'agent:earlier-id-aaa').creationOrder, 1)
  assert.equal(children.find((item) => item.handle === 'agent:mid-id-mmm').creationOrder, 2)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");
const TerminalPolicySurface = await import("../../../dist/OpenCode/Host/TerminalPolicySurface.js");


test('WHAT[managed-session-lifecycle-015] TPOL_linked_child_keeps_exact_handle_and_target', () => {
  const linked = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link', handle: 'agent:h1', child: 'ses_child', agent: 'coder', role: 'Coder',
  })
  assert.equal(linked.ok, true)
  assert.deepEqual(HandleSurface.tryFindByChildSession(linked.state, 'ses_child'), {
    handle: 'agent:h1',
    child: 'ses_child',
    targetAgent: 'coder',
    role: 'Coder',
    lifecycle: 'Active',
    creationOrder: 0,
    completion: undefined,
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: undefined,
  })
  assert.equal(HandleSurface.tryFindByChildSession(linked.state, 'ses_missing'), null)
})
}
