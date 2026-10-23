import test from 'node:test'
import assert from 'node:assert/strict'
import { join } from 'node:path'
import {
  childTurn,
  coldWorkSnapshot,
  forkCall,
  joinCall,
  observeTerminals,
  resumeCall,
  withRealFork,
} from '../../delegation/tests/support/real-fork.mjs'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

/**
 * The cold recovery surface: a fresh writer re-opens the durable event log of
 * the same workspace and settles against the durable facts alone. It is the
 * only place where a **failing append** can be injected (the production join
 * never exposes one), and it never writes a fabricated fact — the commit
 * failure is a real append failure.
 */
const coldConsume = (directory, owner, root, commitment) =>
  forkTool.coldConsumeWorkWithOutcome(join(directory, '.Herdchestra'), owner, root, commitment)

/** The work unit of one root in a cold (re-folded) snapshot. */
const workOf = (works, root) => works.find((work) => work.root === root)

for (const commitment of ['before', 'after']) {
  test('WHAT[managed-session-lifecycle-008] exact consume with ' + commitment + ' commit failure never releases an unconfirmed payload', async () => {
    const owner = 'scope-consume-' + commitment
    await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
      const terminals = await observeTerminals(runtime)
      try {
        const first = await forkCall(hooks, owner, 'Ada', 'FIRST')
        assert.match(first, /Ada/)
        const child = createdIds[0]
        const root = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id

        // The work delivers for real: the durable completion exists before any
        // consume is attempted.
        await childTurn(hooks, runtime, child, `run-${child}-1`, 'PROSE', 'FIRST-ANSWER', terminals.awaitRun)
        assert.equal(workOf(await coldWorkSnapshot(directory, owner), root).lifecycle, 'CompletedAwaitingJoin')

        // The consume whose commit fails releases nothing: it reports no
        // outcome and hands out no payload.
        const withheld = await coldConsume(directory, owner, root, commitment)
        assert.equal(withheld.ok, false)
        assert.equal(Object.hasOwn(withheld, 'view'), false)

        const state = workOf(await coldWorkSnapshot(directory, owner), root)
        assert.equal(state.lifecycle, commitment === 'before' ? 'CompletedAwaitingJoin' : 'Retired')

        // A later confirmed consume succeeds only when the failed commit left
        // the work unconsumed.
        const next = await coldConsume(directory, owner, root, 'confirmed')
        assert.equal(next.ok, commitment === 'before')
        if (next.ok) assert.match(next.view, /FIRST-ANSWER/)

        // The work is single-assignment: consuming it again fails.
        assert.equal((await coldConsume(directory, owner, root, 'confirmed')).ok, false)
      } finally {
        terminals.dispose()
      }
    })
  })
}

test('WHAT[managed-session-lifecycle-008] concurrent exact consumers deliver a work at most once', async () => {
  const owner = 'scope-concurrent-consume'
  await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const first = await forkCall(hooks, owner, 'Ada', 'FIRST')
      assert.match(first, /Ada/)
      const child = createdIds[0]
      const root = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id
      await childTurn(hooks, runtime, child, `run-${child}-1`, 'PROSE', 'FIRST-ANSWER', terminals.awaitRun)

      const outcomes = await Promise.all([
        joinCall(hooks, owner, 'call-join-one'),
        joinCall(hooks, owner, 'call-join-two'),
      ])
      const delivered = outcomes.filter((outcome) => /ok = true/.test(outcome))
      assert.equal(delivered.length, 1, 'exactly one consumer may deliver the work')
      assert.match(delivered[0], /FIRST-ANSWER/)
      assert.equal(workOf(await coldWorkSnapshot(directory, owner), root).lifecycle, 'Retired')
    } finally {
      terminals.dispose()
    }
  })
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

test('WHAT[managed-session-lifecycle-008] EXEC_004_join_may_only_retire_a_handle_that_actually_completed', () => {
  // `retire` IS join's write. Retiring an active handle would discard a child
  // that is still running and leave its completion with nowhere to land.
  const active = linkOn(HandleSurface.empty())

  assert.deepEqual(
    HandleSurface.apply(active, { op: 'retire', handle: HANDLE }),
    { ok: false, error: { kind: 'TransitionRejected', reason: 'NotCompleted' } },
  )
  assert.deepEqual(
    HandleSurface.apply(active, { op: 'retire', handle: handleId.agent('never') }),
    { ok: false, error: { kind: 'TransitionRejected', reason: 'UnknownHandle' } },
  )
})
test('WHAT[managed-session-lifecycle-008] EXEC_009_a_replayed_completion_or_retirement_is_absorbed', () => {
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
  assert.equal(foldStateOf(folded).lifecycle, 'Retired')
})
test('WHAT[managed-session-lifecycle-008] EXEC_004_a_retirement_without_a_completion_stops_the_replay', () => {
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
test('WHAT[managed-session-lifecycle-008] fold_refuses_unknown_fact_case', () => {
  const folded = foldFacts([fact('HandleExploded', { ParentSessionId: PARENT, Handle: HANDLE })])
  assert.equal(folded.ok, false)
  assert.equal(folded.error.kind, 'UnknownFactCase')
  assert.equal(folded.error.value, 'HandleExploded')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
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

test('WHAT[managed-session-lifecycle-008] retirement removes completed handle from all parent views', () => {
  const active = makeActive()
  const completed = complete(active)
  const retired = retire(completed.state)
  assert.equal(retired.ok, true)
  assert.deepEqual(HandleSurface.views(retired.state), { listable: [], joinable: [], active: [] })
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

test('WHAT[managed-session-lifecycle-008] EXEC_009_consume_abandoned_writes_HandleRetired_second_AlreadyRetired', () => {
  let projection = link(HandleSurface.empty(), 'h1', 'ses_c')
  projection = HandleSurface.apply(projection, { op: 'abandon', handle: 'agent:h1', reason: 'ParentCancelled' }).state
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 1)
  const consumed = HandleSurface.apply(projection, { op: 'retire', handle: 'agent:h1' })
  assert.equal(consumed.ok, true)
  projection = consumed.state
  assert.equal(HandleSurface.isRetired(projection, 'agent:h1'), true)
  assert.equal(HandleSurface.reportableAbandonedCount(projection), 0)
  assert.deepEqual(HandleSurface.apply(projection, { op: 'retire', handle: 'agent:h1' }).error, { kind: 'TransitionRejected', reason: 'HandleIsRetired' })
})
}

test('WHAT[managed-session-lifecycle-008] actual join withholds payload until retirement append confirms and survives each crash cut with one delivery (GAP-133)', async () => {
  const owner = 'scope-consume-crash-cut'
  await withRealFork(owner, async ({ hooks, createdIds, runtime, directory }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const first = await forkCall(hooks, owner, 'Ada', 'CRASH-CUT-A')
      assert.match(first, /Ada/)
      const child = createdIds[0]
      const rootA = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id
      await childTurn(hooks, runtime, child, `run-${child}-1`, 'PROSE A', 'CRASH-ANSWER-A', terminals.awaitRun)

      await resumeCall(hooks, owner, 'Ada', 'CRASH-CUT-B')
      const rootB = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1).id
      await childTurn(hooks, runtime, child, `run-${child}-2`, 'PROSE B', 'CRASH-ANSWER-B', terminals.awaitRun)

      let deliveries = 0

      // Crash cut before the retirement append: the dying caller attempted a
      // consume whose append never committed, so it received nothing and no
      // tombstone reached the disk. A cold-reopened journal (fresh writer, full
      // replay — the shape a restarted process takes) still finds the work
      // unconsumed and must deliver exactly once, only after its own append
      // confirms.
      const dyingBefore = await coldConsume(directory, owner, rootA, 'before')
      assert.equal(dyingBefore.ok, false)
      assert.equal(Object.hasOwn(dyingBefore, 'view'), false)
      assert.equal(workOf(await coldWorkSnapshot(directory, owner), rootA).lifecycle, 'CompletedAwaitingJoin')
      const recovered = await coldConsume(directory, owner, rootA, 'confirmed')
      assert.equal(recovered.ok, true)
      assert.match(recovered.view, /CRASH-ANSWER-A/)
      deliveries += 1
      assert.equal(workOf(await coldWorkSnapshot(directory, owner), rootA).lifecycle, 'Retired')
      const repeat = await coldConsume(directory, owner, rootA, 'confirmed')
      assert.equal(repeat.ok, false)
      assert.match(repeat.error, /AlreadyRetired/)

      // Crash cut after the retirement append: this time the tombstone did
      // commit before the caller died, but the receipt never came back and the
      // payload was never delivered. The unknown commit stays conservative — no
      // fabricated re-claim — and every later cold consumer meets the durable
      // tombstone instead of a second delivery.
      const dyingAfter = await coldConsume(directory, owner, rootB, 'after')
      assert.equal(dyingAfter.ok, false)
      assert.equal(Object.hasOwn(dyingAfter, 'view'), false)
      assert.equal(workOf(await coldWorkSnapshot(directory, owner), rootB).lifecycle, 'Retired')
      const reclaimer = await coldConsume(directory, owner, rootB, 'confirmed')
      assert.equal(reclaimer.ok, false)
      assert.match(reclaimer.error, /AlreadyRetired/)

      assert.equal(deliveries, 1, 'exactly one payload delivery reaches an independent caller across both crash cuts')
    } finally {
      terminals.dispose()
    }
  })
})


{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const HandleJournalSurface = await import("../../../dist/Execution/Delegation/Handle/JournalSurface.js");
const { HandleLifecycleLaw } = await import("wanxiangli/handle-lifecycle");
const { withAdmittedChildren } = await import("./support/admitted-child-work.mjs");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const { hostPort } = await import("../../interaction-authority/tests/support/authority.mjs");

const PARENT = 'ses_parent_consume_cell'
const CHILD = 'ses_child_consume_cell'
const AGENT = 'h1'
const law = new HandleLifecycleLaw()
const hexToken = fc
  .array(fc.constantFrom(...'abcdef0123456789'), { minLength: 8, maxLength: 24 })
  .map((characters) => characters.join(''))
const kindArb = fc.constantFrom('Terminal', 'SendFailure')
const propertyOptions = { seed: 0x48434f4e, numRuns: 2_000 }

/** Fold-level consume of a completed work cell: the production controller must
 *  only ever emit a fact that this pure transition would accept. */
const lawConsume = (expected, cellEqualsExpected, consumptionId = 'c'.repeat(32)) =>
  law.work({
    event: 'consume',
    phase: 'CompletedAwaitingJoin',
    legacy: false,
    consumptionId,
    expected,
    cellEqualsExpected,
  })

test('WHAT[managed-session-lifecycle-008] fast-check: matching durable cell always retires; mismatched cell is only ConsumptionMismatch', () => {
  fc.assert(
    fc.property(kindArb, hexToken, hexToken, hexToken, (kind, ref, digest, other) => {
      fc.pre(ref !== other)
      const expected = { kind, hasRef: true, hasDigest: true }
      assert.deepEqual(lawConsume(expected, true), { verdict: 'set', phase: 'Retired' })
      assert.deepEqual(lawConsume(expected, false), { verdict: 'reject', rejection: 'ConsumptionMismatch' })
      assert.deepEqual(lawConsume({ kind, hasRef: false, hasDigest: true }, true), {
        verdict: 'set',
        phase: 'Retired',
      }, 'law trusts the caller-reported cellEqualsExpected flag; the F# owner must set it from durable equality')
      // Blank consumption id is always mismatch, even with a true cell flag.
      assert.deepEqual(lawConsume(expected, true, '   '), { verdict: 'reject', rejection: 'ConsumptionMismatch' })
      assert.deepEqual(lawConsume(expected, true, ''), { verdict: 'reject', rejection: 'ConsumptionMismatch' })
      // Abandoned shape without equality flag stays closed.
      assert.deepEqual(
        law.work({
          event: 'consume',
          phase: 'Abandoned',
          legacy: false,
          consumptionId: 'a'.repeat(32),
          expected: { kind: 'Terminal', hasRef: true, hasDigest: true },
          cellEqualsExpected: false,
        }),
        { verdict: 'reject', rejection: 'ConsumptionMismatch' },
      )
      assert.deepEqual(
        law.work({
          event: 'consume',
          phase: 'Abandoned',
          legacy: false,
          consumptionId: 'a'.repeat(32),
          expected: { kind: 'Cancelled', hasRef: false, hasDigest: false },
          cellEqualsExpected: false,
        }),
        { verdict: 'set', phase: 'Retired' },
      )
      void other
      void digest
    }),
    propertyOptions,
  )
})

test('WHAT[managed-session-lifecycle-008] production consume ignores a stale caller cell and never journal-cuts on ConsumptionMismatch', async () => {
  await withAdmittedChildren(
    'consume-stale-cell',
    PARENT,
    [{ agentId: AGENT, sessionId: CHILD, role: 'engineer' }],
    async (journal, profiles) => {
      const samples = fc.sample(
        fc
          .record({
            kind: kindArb,
            ref: hexToken,
            digest: hexToken,
            staleRef: hexToken,
            staleDigest: hexToken,
          })
          .filter((cell) => cell.ref !== cell.staleRef || cell.digest !== cell.staleDigest),
        64,
      )

      let profile = profiles.get(AGENT)
      for (const [index, cell] of samples.entries()) {
        const root = profile.authorityRoot
        const completed = await HandleJournalSurface.recordWorkCompletion(
          journal,
          PARENT,
          AGENT,
          CHILD,
          root,
          cell.kind,
          cell.ref,
          cell.digest,
        )
        assert.equal(completed.ok, true, `complete[${index}]: ${JSON.stringify(completed)}`)
        assert.equal(
          HandleJournalSurface.snapshot(journal, PARENT, AGENT, CHILD, root).record.lifecycle,
          'CompletedAwaitingJoin',
        )

        const mismatched = await HandleJournalSurface.consumeWork(journal, PARENT, AGENT, CHILD, root, {
          kind: cell.kind,
          completionRef: cell.staleRef,
          completionDigest: cell.staleDigest,
        })
        assert.equal(
          mismatched.ok,
          true,
          `stale-cell consume[${index}] must ignore caller cell: ${JSON.stringify(mismatched)}`,
        )
        assert.equal(
          HandleJournalSurface.snapshot(journal, PARENT, AGENT, CHILD, root).record.lifecycle,
          'Retired',
        )

        const again = await HandleJournalSurface.consumeWork(journal, PARENT, AGENT, CHILD, root, null)
        assert.equal(again.ok, false)
        assert.match(String(again.error), /AlreadyRetired/)

        // Next Active work on the same road for the next sample.
        const sent = await dispatch.sendAgentOwnerRootAwait(
          hostPort(async () => dispatch.admittedWithReceipt(`consume-stale-next-${index}`)),
          journal,
          CHILD,
          `NEXT-${index}`,
          profile.identitySeed,
        )
        assert.equal(sent.ok, true, sent.error)
        const accepted = await dispatch.acceptAgentOwnerRoot(
          journal,
          CHILD,
          sent.key,
          `physical-consume-stale-${index}`,
        )
        assert.equal(accepted.ok, true, JSON.stringify(accepted.error))
        profile = accepted.profile
      }
    },
  )
})
}
