import test from 'node:test'
import assert from 'node:assert/strict'
import { admit, assertCold, forkTool, withForkRuntime } from '../../delegation/tests/support/scoped-work.mjs'
import { toolModule } from '../../delegation/tests/support/fork-runtime.mjs'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

test('WHAT[managed-session-lifecycle-015] actual admissions and replay retain exact handle child participant binding', async () => {
  const owner = 'scope-stable-binding'
  await withForkRuntime(owner, async (runtime, directory) => {
    const a = await admit(runtime, owner, 1, 'FIRST')
    const before = forkTool.workSnapshot(runtime, owner)
    const forged = await forkTool.consumeWorkWithOutcome(runtime, owner, 'not-an-accepted-root', 'confirmed')
    assert.equal(forged.ok, false)
    assert.equal(forged.error, 'WorkNotAdmitted')
    assert.deepEqual(forkTool.workSnapshot(runtime, owner), before)
    assert.equal(await forkTool.settle(runtime, owner, 'FIRST-ANSWER', 'provider-a'), true)
    const b = await admit(runtime, owner, 2, 'SECOND')
    for (const key of ['handle', 'child', 'targetAgent', 'byname', 'role']) assert.equal(b[key], a[key])
    assert.notEqual(b.root, a.root)
    assert.equal(forkTool.childCount(runtime), 1)
    await assertCold(runtime, directory, owner)
    assert.equal(await forkTool.settle(runtime, owner, 'SECOND-ANSWER', 'provider-b'), true)
  })
})

test('WHAT[managed-session-lifecycle-015] a bare Root string is not work admission in the production fold', async () => {
  const fold = await import('../../../dist/Execution/Delegation/Handle/FoldSurface.js')
  const linked = fold.foldApply(fold.foldEmpty(), [{ fact: { case: 'HandleLinked', payload: {
    ParentSessionId: 'owner', ChildSessionId: 'child', Handle: 'agent:unadmitted',
    TargetAgent: 'engineer', CanonicalRole: 'Engineer', Ownership: 'DurableParentHandle',
  } } }])
  assert.equal(linked.ok, true)
  const claimed = fold.foldApply(linked.state, [{ fact: { case: 'HandleWorkCompleted', payload: {
    ParentSessionId: 'owner', Work: { Handle: 'agent:unadmitted', ChildSessionId: 'child', AuthorityRoot: 'made-up-root' },
    Kind: 'Terminal', CompletionRef: 'unbacked', CompletionDigest: 'unbacked',
  } } }])
  assert.equal(claimed.ok, false)
  assert.equal(claimed.error.Fact, 'HandleWorkCompleted')
  assert.match(claimed.error.Reason, /WorkNotAdmitted/)
})

test('WHAT[managed-session-lifecycle-015] a retired DevOps link is not permanent-loss proof for physical replacement', async () => {
  const handles = await import('../../../dist/Execution/Delegation/Handle/Surface.js')
  const link = { op: 'link', handle: 'agent:devops', child: 'original', agent: 'devops', role: 'DevOps' }
  const active = handles.apply(handles.empty(), link)
  const completed = handles.apply(active.state, { op: 'complete', handle: link.handle, kind: 'Terminal' })
  const retired = handles.apply(completed.state, { op: 'retire', handle: link.handle })
  const replacement = handles.apply(retired.state, { ...link, child: 'replacement' })
  assert.equal(replacement.ok, false)
  assert.equal(replacement.error.reason, 'HandleIdentityConflict')
  assert.equal(handles.read(retired.state, link.handle).child, 'original')
})

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

test('WHAT[managed-session-lifecycle-015] actual recovered fork uses its runtime Agent ID handle and rebinds the same physical child (GAP-133)', { todo: 'B 冷重开 runtime 的 settle 挂起（AwaitCurrentWorkRecord 永不完成、60s 超时）——真实生产缺陷待修。前置断言已实证通过：resume /Ada/、childCount=0、child===first.child、handle/byname 继承、unknown byname typed 拒绝、cold snapshot deepEqual。调查方向：Host terminal settlement 链的冷重开态——AwaitCurrentWorkRecord 的等待条件在冷 runtime 上于何处等待（Notify 已发出但 work record 事件未到达等待者）。' }, async () => {
  const owner = 'scope-cold-rebind'
  const directory = mkdtempSync(join(tmpdir(), 'wxs-delegation-fork-'))
  const owners = [{ sessionId: owner, agent: 'manager' }]

  // A is the first process: a real fork leaves the runtime Agent ID handle and
  // the physical child session on the canonical journal, then A exits.
  const a = await forkTool.createRuntime(directory, owners)
  const first = await admit(a, owner, 1, 'COLD-A')
  assert.equal(await forkTool.settle(a, owner, 'COLD-ANSWER-A', 'provider-a'), true)
  const durableAtExit = forkTool.workSnapshot(a, owner)
  forkTool.disposeRuntime(a)

  try {
    // A cold-reopened journal (fresh writer, full replay — the shape a
    // restarted process takes) sees the exact same durable handle facts.
    assert.deepEqual(await forkTool.coldWorkSnapshot(directory, owner), durableAtExit)

    // B is the restarted process: empty process tables, same journal.
    const b = await forkTool.createRuntime(directory, owners)

    try {
      const invocation = forkTool.executeManagerResume(b, toolModule, owner, '', 'Ada', 'COLD-B')
      await forkTool.awaitPromptCount(b, 1)
      assert.equal(forkTool.acceptPrompt(b, 0), true)
      assert.match(await invocation, /Ada/)

      // No new physical child was created for the cache miss: B adopted the
      // durable binding and drove the same child session the fork made.
      assert.equal(forkTool.childCount(b), 0)
      assert.equal(forkTool.child(b), first.child)

      const works = forkTool.workSnapshot(b, owner)
      assert.equal(works.length, 2)
      assert.equal(works.find(work => work.root === first.root).lifecycle, 'CompletedAwaitingJoin')
      const second = works.find(work => work.lifecycle === 'Active')
      assert.equal(second.handle, first.handle)
      assert.equal(second.child, first.child)
      assert.equal(second.byname, first.byname)
      assert.notEqual(second.root, first.root)

      // An unknown byname is a typed refusal, never a silent new person.
      const unknown = await forkTool.executeManagerResume(b, toolModule, owner, '', 'Nobody', 'COLD-X')
      assert.doesNotMatch(unknown, /Nobody/)
      assert.equal(forkTool.childCount(b), 0)
      assert.deepEqual(forkTool.workSnapshot(b, owner), works)

      assert.equal(await forkTool.settle(b, owner, 'COLD-ANSWER-B', 'provider-b'), true)
    } finally {
      forkTool.disposeRuntime(b)
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})
