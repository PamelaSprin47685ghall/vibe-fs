import test from 'node:test'
import assert from 'node:assert/strict'
import { join } from 'node:path'
import {
  childTurn,
  coldWorkSnapshot,
  forkCall,
  observeTerminals,
  resumeCall,
  withRealFork,
  withForkHost,
} from '../../delegation/tests/support/real-fork.mjs'
import { acceptAuthorityRoot, openIncumbency, withRestartablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

/**
 * The cold recovery surface: a fresh writer re-opens the durable event log of
 * the same workspace and settles against the durable facts alone. It never
 * writes a fabricated fact.
 */
const coldConsume = (directory, owner, root, commitment) =>
  forkTool.coldConsumeWorkWithOutcome(join(directory, '.Herdchestra'), owner, root, commitment)

test('WHAT[managed-session-lifecycle-015] actual admissions and replay retain exact handle child participant binding', async () => {
  const owner = 'scope-stable-binding'
  await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const first = await forkCall(hooks, owner, 'Ada', 'FIRST')
      assert.match(first, /Ada/)
      const child = createdIds[0]
      const rootA = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id
      const before = await coldWorkSnapshot(directory, owner)

      // A root that was never admitted is not work: consuming it refuses, and
      // nothing about the admitted work changes.
      const forged = await coldConsume(directory, owner, 'not-an-accepted-root', 'confirmed')
      assert.equal(forged.ok, false)
      assert.equal(forged.error, 'WorkNotAdmitted')
      assert.deepEqual(await coldWorkSnapshot(directory, owner), before)

      await childTurn(hooks, runtime, child, `run-${child}-1`, 'PROSE A', 'FIRST-ANSWER', terminals.awaitRun)

      // The second admission keeps the exact same binding — same handle, same
      // physical child, same participant — and only the root differs.
      await resumeCall(hooks, owner, 'Ada', 'SECOND')
      const rootB = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id
      const works = await coldWorkSnapshot(directory, owner)
      const a = works.find((work) => work.root === rootA)
      const b = works.find((work) => work.root === rootB)
      for (const key of ['handle', 'child', 'targetAgent', 'byname', 'role']) assert.equal(b[key], a[key])
      assert.notEqual(b.root, a.root)
      assert.equal(createdIds.length, 1)

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'PROSE B', 'SECOND-ANSWER', terminals.awaitRun)
    } finally {
      terminals.dispose()
    }
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

test('WHAT[managed-session-lifecycle-015] live resume keeps the runtime Agent ID handle and cold journal binding', async () => {
  const owner = 'scope-cold-rebind'
  await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
    const terminals = await observeTerminals(runtime)
    try {
      // The real plugin writes the runtime Agent ID and physical child to the journal.
      const first = await forkCall(hooks, owner, 'Ada', 'COLD-A')
      assert.match(first, /Ada/)
      const child = createdIds[0]
      const rootA = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id
      await childTurn(hooks, runtime, child, `run-${child}-1`, 'PROSE A', 'COLD-ANSWER-A', terminals.awaitRun)
      const durableAtExit = await coldWorkSnapshot(directory, owner)

      // This read uses a fresh writer, but the plugin runtime remains alive.
      assert.deepEqual(await coldWorkSnapshot(directory, owner), durableAtExit)

      // A live resume addresses the same child without creating a replacement.
      await resumeCall(hooks, owner, 'Ada', 'COLD-B')
      assert.equal(createdIds.length, 1)
      assert.equal(createdIds[0], child)

      const works = await coldWorkSnapshot(directory, owner)
      assert.equal(works.length, 2)
      assert.equal(works.find((work) => work.root === rootA).lifecycle, 'CompletedAwaitingJoin')
      const second = works.find((work) => work.lifecycle === 'Active')
      assert.equal(second.handle, durableAtExit[0].handle)
      assert.equal(second.child, durableAtExit[0].child)
      assert.equal(second.byname, durableAtExit[0].byname)
      assert.notEqual(second.root, rootA)

      // An unknown byname is a typed refusal, never a silent new person.
      const unknown = await resumeCall(hooks, owner, 'Nobody', 'COLD-X')
      assert.match(unknown, /ok = false/)
      assert.doesNotMatch(unknown, /Nobody/)
      assert.equal(createdIds.length, 1)
      assert.deepEqual(await coldWorkSnapshot(directory, owner), works)

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'PROSE B', 'COLD-ANSWER-B', terminals.awaitRun)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[managed-session-lifecycle-015] fresh runtime adopts the exact durable handle and physical child after the original runtime closes (GAP-133)', async () => {
  const owner = 'scope-fresh-runtime-rebind'
  await withRestartablePlugin(async (start, directory, shared) => {
    const firstHooks = await start()
    let first
    let durableAtExit
    await shared.withRuntime(async journal => {
      const runtime = { ...shared, ...journal }
      await acceptAuthorityRoot(runtime, owner, 'manager')
      await openIncumbency(runtime, owner)
      await withForkHost(firstHooks, runtime, owner, async () => {
        assert.match(await forkCall(firstHooks, owner, 'Ada', 'COLD-A', 'fresh-fork'), /Ada/)
        first = (await coldWorkSnapshot(directory, owner))[0]
        const terminals = await observeTerminals(runtime)
        try {
          await childTurn(firstHooks, runtime, first.child, 'fresh-provider-a', 'PROSE A', 'COLD-ANSWER-A', terminals.awaitRun)
        } finally { terminals.dispose() }
        durableAtExit = await coldWorkSnapshot(directory, owner)
      })
    })
    await shared.stop(firstHooks)
    assert.deepEqual(await coldWorkSnapshot(directory, owner), durableAtExit)
    const createdBeforeRestart = shared.createdIds.length
    const secondHooks = await start()
    assert.notEqual(secondHooks, firstHooks, 'the first plugin is disposed before a new incarnation starts')
    await shared.withRuntime(async journal => {
      const runtime = { ...shared, ...journal }
      await withForkHost(secondHooks, runtime, owner, async () => {
        assert.match(await resumeCall(secondHooks, owner, 'Ada', 'COLD-B', 'fresh-resume'), /Ada/)
        assert.equal(shared.createdIds.length, createdBeforeRestart, 'restart adopts the durable physical child')
        const works = await coldWorkSnapshot(directory, owner)
        assert.equal(works.length, 2)
        assert.equal(works.find(work => work.root === first.root).lifecycle, 'CompletedAwaitingJoin')
        const second = works.find(work => work.lifecycle === 'Active')
        for (const key of ['handle', 'child', 'byname', 'targetAgent', 'role']) assert.equal(second[key], first[key])
        assert.notEqual(second.root, first.root)
        const unknown = await resumeCall(secondHooks, owner, 'Nobody', 'COLD-X', 'fresh-unknown')
        assert.match(unknown, /ok = false/)
        assert.doesNotMatch(unknown, /Nobody/)
        assert.equal(shared.createdIds.length, createdBeforeRestart)
        assert.deepEqual(await coldWorkSnapshot(directory, owner), works)
        const terminals = await observeTerminals(runtime)
        try {
          await childTurn(secondHooks, runtime, first.child, 'fresh-provider-b', 'PROSE B', 'COLD-ANSWER-B', terminals.awaitRun)
        } finally { terminals.dispose() }
      })
    })
    await shared.stop(secondHooks)
  })
})
