import test from 'node:test'

{
const { default: assert } = await import('node:assert/strict')
const { default: fc } = await import('fast-check')
const { chmodSync, mkdtempSync, rmSync } = await import('node:fs')
const { tmpdir } = await import('node:os')
const { join } = await import('node:path')
const blog = await import('../../../dist/Enforcer/BlogSurface.js')
const journal = await import('../../../dist/Persistence/Journal/Surface.js')
const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
const runtime = await import('../../../dist/Context/Companion/RuntimeSurface.js')

const NUDGE_KIND = 'blogger-missing-tool'
const AABB_KIND = 'blogger-aabb'
const freshCalls = () => ({
  sendPrompt: [],
  subscribe: [],
  subscribeFuture: [],
  rootRead: [],
  eventSubscribe: [],
  eventFuture: [],
  eventNotify: [],
})
const sessionPort = (calls) => ({
  SubscribeTerminal: (...args) => {
    calls.subscribe.push(args)
    return { Dispose: () => {} }
  },
  SubscribeFutureTerminal: (...args) => {
    calls.subscribeFuture.push(args)
    return { Dispose: () => {} }
  },
  SendPrompt: async (sessionId, text, options) => {
    calls.sendPrompt.push({ sessionId, text, options })
    return dispatch.admittedWithReceipt(`accepted-${calls.sendPrompt.length}`)
  },
})
const rootPort = (calls) => ({
  TryRead: () => {
    calls.rootRead.push([])
    return undefined
  },
})
const eventPort = (calls) => ({
  SubscribeTerminalListener: (...args) => {
    calls.eventSubscribe.push(args)
    return { Dispose: () => {} }
  },
  SubscribeFutureTerminalListener: (...args) => {
    calls.eventFuture.push(args)
    return { Dispose: () => {} }
  },
  NotifyTerminal: (...args) => {
    calls.eventNotify.push(args)
    return false
  },
})
let ownerCounter = 0
const setupOwner = async (t) => {
  ownerCounter += 1
  const n = ownerCounter
  const ids = {
    main: `ses-main-repair-${n}`,
    blogger: `ses-blogger-repair-${n}`,
    request: `req-blog-${n}`,
    physical: `msg-phys-blog-${n}`,
  }
  const dir = mkdtempSync(join(tmpdir(), 'wxs-blogger-repair-'))
  const opened = await journal.JournalSurface_bootWithWriterId(
    dir,
    `writer-blog-${n}`,
    `rt-blog-${n}`,
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const accepted = await dispatch.acceptHumanRoot(opened.journal, ids.blogger, ids.physical, 'blogger')
  assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)
  // Real process-local owner scope (also isolates the shared flight registry)
  // and the exact live flight for this request.
  const scope = runtime.createScope()
  const request = runtime.main({
    requestId: ids.request,
    mainSession: ids.main,
    bloggerSession: ids.blogger,
    toml: 'repair-toml',
  })
  assert.equal(runtime.claimCurrentRequest(scope, ids.blogger, request), 'Claimed')
  t.after(() => {
    try {
      runtime.dispose(scope)
    } catch {}
    try {
      journal.JournalSurface_dispose(opened.journal)
    } catch {}
    rmSync(dir, { recursive: true, force: true })
  })
  const calls = freshCalls()
  const ports = {
    session: sessionPort(calls),
    root: rootPort(calls),
    events: eventPort(calls),
  }
  // BlogSurface.journalOf reads a structural `Journal` member or the raw
  // AgentJournal; the boot handle wraps the live AgentJournal in `.journal`.
  const durable = opened.journal.journal
  return {
    ids,
    durable,
    handle: opened.journal,
    scope,
    request,
    ports,
    calls,
    dir,
    writerFile: join(dir, 'wanxiang', 'events', `writer-blog-${n}.ndjson`),
  }
}
const idleObservation = (ports, ids, run, { quiescent = true } = {}) => ({
  quiescent,
  context: {
    sessionId: ids.blogger,
    physicalUserMessageId: ids.physical,
    authorityRoot: ids.physical,
    providerRun: run,
  },
  sessionPort: ports.session,
  rootWorkspace: ports.root,
  eventPort: ports.events,
})
const captureFatal = async (work) => {
  const previousExit = process.env.WANXIANGSHU_NO_FATAL_EXIT
  const previousError = console.error
  const records = []
  process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
  console.error = (...args) => records.push(args.join(' '))
  try {
    return { value: await work(), records }
  } finally {
    console.error = previousError
    if (previousExit === undefined) delete process.env.WANXIANGSHU_NO_FATAL_EXIT
    else process.env.WANXIANGSHU_NO_FATAL_EXIT = previousExit
  }
}

test('WHAT[behavior-diagnosis-017] repeat_terminal_idle_is_idempotent_no_duplicate_nudge', async (t) => {
  const { ids, durable, scope, request, ports, calls } = await setupOwner(t)

  const first = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-1'),
  )
  assert.equal(first.outcome, 'NudgeSent')
  assert.equal(typeof first.promptKey, 'string')
  assert.equal(calls.sendPrompt.length, 1)
  assert.equal(blog.repairClaimedForKind(durable, ids.blogger, ids.request, 'run-1', NUDGE_KIND), true)
  assert.equal(blog.repairIssuedForKind(durable, ids.blogger, ids.request, 'run-1', NUDGE_KIND), true)

  const second = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-1'),
  )
  assert.equal(second.outcome, 'PendingRepairWait')
  assert.equal(calls.sendPrompt.length, 1)
})

test('WHAT[behavior-diagnosis-017] idle_without_quiescence_permit_spends_no_budget', async (t) => {
  const { ids, durable, scope, request, ports, calls } = await setupOwner(t)

  const res = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-1', { quiescent: false }),
  )
  assert.equal(res.outcome, 'PendingRepairWait')
  assert.equal(calls.sendPrompt.length, 0)
  assert.equal(blog.repairClaimedForKind(durable, ids.blogger, ids.request, 'run-1', NUDGE_KIND), false)
})

test('WHAT[behavior-diagnosis-017] next_terminal_sends_at_most_one_aabb_then_abandons', async (t) => {
  const { ids, durable, scope, request, ports, calls } = await setupOwner(t)

  const nudge = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-1'),
  )
  assert.equal(nudge.outcome, 'NudgeSent')
  assert.equal(calls.sendPrompt.length, 1)

  const aabb = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-2'),
  )
  assert.equal(aabb.outcome, 'AabbSent')
  assert.equal(typeof aabb.promptKey, 'string')
  assert.equal(calls.sendPrompt.length, 2)
  assert.equal(blog.repairClaimedForKind(durable, ids.blogger, ids.request, 'run-2', AABB_KIND), true)

  const repeat = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-2'),
  )
  assert.equal(repeat.outcome, 'PendingRepairWait')
  assert.equal(calls.sendPrompt.length, 2)

  const exhausted = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-3'),
  )
  assert.equal(exhausted.outcome, 'AbandonedExhausted')
  assert.equal(calls.sendPrompt.length, 2)
  assert.equal(calls.eventNotify.length, 1)
  assert.equal(typeof calls.eventNotify[0][0], 'string')
  assert.equal(calls.eventNotify[0][0], ids.blogger)
  assert.equal(runtime.tryGetFlight(scope, ids.blogger), null)
})

test('WHAT[behavior-diagnosis-017] exhausted repair stops its real continuation without a process fatal', async (t) => {
  const { ids, durable, scope, request, ports, calls } = await setupOwner(t)
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-1'))
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-2'))
  const messages = [{ info: { id: 'run-3', role: 'assistant', time: { completed: 1 } }, parts: [] }]

  const { value, records } = await captureFatal(() =>
    blog.continueTerminal(scope, durable, request, 'run-3', 0, messages),
  )

  assert.equal(value.kind, 'StopPhysicalRun')
  assert.deepEqual(value.messages, messages)
  assert.deepEqual(records, [])
  assert.equal(calls.sendPrompt.length, 2)
  assert.equal(calls.eventNotify.length, 1)
  assert.equal(runtime.tryGetFlight(scope, ids.blogger), null)
})

test('WHAT[behavior-diagnosis-017] terminal without provider identity stops only the exact request', async (t) => {
  const { durable, scope, request, ids } = await setupOwner(t)
  const messages = [{ info: { role: 'assistant', time: { completed: 1 } }, parts: [] }]
  const { value, records } = await captureFatal(() =>
    blog.continueTerminal(scope, durable, request, '', 0, messages),
  )
  assert.equal(value.kind, 'StopPhysicalRun')
  assert.deepEqual(records, [])
  assert.equal(runtime.tryGetFlight(scope, ids.blogger), null)
})

test('WHAT[behavior-diagnosis-017] failed durable abandon retains its flight and never reports settlement', async (t) => {
  const { durable, handle, scope, request, ids } = await setupOwner(t)
  journal.JournalSurface_dispose(handle)
  const { records } = await captureFatal(async () => {
    await assert.rejects(() => blog.continueTerminal(scope, durable, request, '', 0, []))
  })
  assert.deepEqual(records, [])
  assert.equal(runtime.tryGetFlight(scope, ids.blogger).requestId, ids.request)
})

test('WHAT[behavior-diagnosis-017] repair settlement failure rejects every waiting observer without fatal or release', async (t) => {
  const { durable, handle, scope, request, ids, ports, calls } = await setupOwner(t)
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-1'))
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-2'))
  journal.JournalSurface_dispose(handle)
  const { value, records } = await captureFatal(() => Promise.allSettled([
    blog.observeTransformRepair(scope, durable, request, 'run-3', []),
    blog.observeTransformRepair(scope, durable, request, 'run-4', []),
  ]))
  assert.deepEqual(value.map(result => result.status), ['rejected', 'rejected'])
  assert.equal(value[0].reason, value[1].reason, 'all observers receive the exact same failure')
  assert.deepEqual(records, [])
  assert.equal(calls.eventNotify.length, 0)
  assert.equal(runtime.tryGetFlight(scope, ids.blogger).requestId, ids.request)
})

test('WHAT[behavior-diagnosis-017] unknown abandon commit still rejects every observer without release', async (t) => {
  const { durable, scope, request, ids, ports, calls, writerFile } = await setupOwner(t)
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-1'))
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-2'))
  // Physical write failure mid-append: the durable outcome is Unknown, not
  // NotAttempted — the abandon may or may not be recorded.
  chmodSync(writerFile, 0o400)
  const { value, records } = await captureFatal(() => Promise.allSettled([
    blog.observeTransformRepair(scope, durable, request, 'run-3', []),
    blog.observeTransformRepair(scope, durable, request, 'run-4', []),
  ]))
  assert.deepEqual(value.map(result => result.status), ['rejected', 'rejected'])
  assert.equal(value[0].reason, value[1].reason, 'all observers receive the exact same failure')
  assert.match(String(value[0].reason), /append outcome unknown/)
  assert.deepEqual(records, [])
  assert.equal(calls.eventNotify.length, 0)
  assert.equal(runtime.tryGetFlight(scope, ids.blogger).requestId, ids.request)
})

test('WHAT[behavior-diagnosis-017] late observers after settlement failure get the same failure and no new budget', async (t) => {
  const { durable, handle, scope, request, ids, ports, calls } = await setupOwner(t)
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-1'))
  await blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-2'))
  journal.JournalSurface_dispose(handle)
  const { value: first, records } = await captureFatal(() => Promise.allSettled([
    blog.observeTransformRepair(scope, durable, request, 'run-3', []),
  ]))
  assert.equal(first[0].status, 'rejected')
  const failure = first[0].reason

  const late = await Promise.allSettled([
    blog.observeTransformRepair(scope, durable, request, 'run-5', []),
    blog.observeIdleRepair(scope, durable, request, idleObservation(ports, ids, 'run-5')),
  ])
  assert.deepEqual(late.map(result => result.status), ['rejected', 'rejected'])
  assert.equal(late[0].reason, failure, 'late transform posts replay the stored failure')
  assert.equal(late[1].reason, failure, 'late idle posts replay the stored failure')
  assert.match(
    runtime.claimRepairEpisode(scope, 'req-blog-superseding', ids.physical, ids.main, ids.blogger),
    /^Error:Claimed request req-blog-superseding does not match active flight/,
  )
  assert.deepEqual(records, [])
  assert.equal(calls.sendPrompt.length, 2, 'a failed episode never re-opens the repair budget')
  assert.equal(calls.eventNotify.length, 0)
  assert.equal(runtime.tryGetFlight(scope, ids.blogger).requestId, ids.request)
})

test('WHAT[behavior-diagnosis-017] generated duplicate and interleaved repair observations preserve bounded effects', async () => {
  await fc.assert(fc.asyncProperty(
    fc.array(fc.record({ idle: fc.boolean(), duplicate: fc.boolean(), quiescent: fc.boolean() }), { maxLength: 20 }),
    async (trace) => {
      const cleanups = []
      const owner = await setupOwner({ after: fn => cleanups.push(fn) })
      const { durable, scope, request, ids, ports, calls } = owner
      try {
        const { records } = await captureFatal(async () => {
          let run = 0
          for (const observation of trace) {
            if (!observation.duplicate) run += 1
            const providerRun = `generated-run-${run}`
            if (observation.idle) {
              await blog.observeIdleRepair(scope, durable, request,
                idleObservation(ports, ids, providerRun, observation))
            } else {
              const messages = [{ info: { id: providerRun, role: 'assistant', time: { completed: 1 } }, parts: [] }]
              await blog.continueTerminal(scope, durable, request, providerRun, 0, messages)
            }
            assert.ok(calls.sendPrompt.length <= 2, 'one nudge and at most one physical AABB')
            assert.ok(calls.eventNotify.length <= 1, 'one terminal per exact repair episode')
            if (calls.eventNotify.length > 0) assert.equal(runtime.tryGetFlight(scope, ids.blogger), null)
          }
        })
        assert.deepEqual(records, [])
      } finally {
        for (const cleanup of cleanups.reverse()) await cleanup()
      }
    },
  ), { seed: 20260914, numRuns: 60 })
})

test('WHAT[behavior-diagnosis-017] transform_and_idle_interleave_resolves_to_single_owner', async (t) => {
  const { ids, durable, scope, request, ports, calls } = await setupOwner(t)

  const before = await blog.observeTransformRepair(scope, durable, request, 'run-1', [])
  assert.equal(before.outcome, 'PendingRepairWait')
  assert.equal(calls.sendPrompt.length, 0)

  const nudge = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-1'),
  )
  assert.equal(nudge.outcome, 'NudgeSent')
  assert.equal(calls.sendPrompt.length, 1)

  const sameRun = await blog.observeTransformRepair(scope, durable, request, 'run-1', [])
  assert.equal(sameRun.outcome, 'PendingRepairWait')
  assert.equal(calls.sendPrompt.length, 1)

  const injected = await blog.observeTransformRepair(scope, durable, request, 'run-2', [])
  assert.equal(injected.outcome, 'RepairInjected')
  assert.equal(injected.messages.length, 1)
  assert.equal(calls.sendPrompt.length, 1)

  const settled = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-2'),
  )
  assert.equal(settled.outcome, 'PendingRepairWait')
  assert.equal(calls.sendPrompt.length, 1)

  const exhausted = await blog.observeTransformRepair(scope, durable, request, 'run-3', [])
  assert.equal(exhausted.outcome, 'AbandonedExhausted')
  assert.equal(calls.sendPrompt.length, 1)
  assert.equal(calls.eventNotify.length, 1)
})

test('WHAT[behavior-diagnosis-017] transform_on_aabb_claimed_terminal_waits_without_double_spend', async (t) => {
  const { ids, durable, scope, request, ports, calls } = await setupOwner(t)

  const nudge = await blog.observeIdleRepair(
    scope,
    durable,
    request,
    idleObservation(ports, ids, 'run-1'),
  )
  assert.equal(nudge.outcome, 'NudgeSent')

  const injected = await blog.observeTransformRepair(scope, durable, request, 'run-2', [])
  assert.equal(injected.outcome, 'RepairInjected')
  assert.equal(injected.messages.length, 1)

  const pending = await blog.observeTransformRepair(scope, durable, request, 'run-2', injected.messages)
  assert.equal(pending.outcome, 'PendingRepairWait')
  assert.equal(calls.sendPrompt.length, 1)

  const exhausted = await blog.observeTransformRepair(scope, durable, request, 'run-3', injected.messages)
  assert.equal(exhausted.outcome, 'AbandonedExhausted')
  assert.equal(calls.sendPrompt.length, 1)
})

test('WHAT[behavior-diagnosis-017] repair_without_journal_abandons_without_physical_sends', async (t) => {
  const { ids, scope, request, ports, calls } = await setupOwner(t)

  const idle = await blog.observeIdleRepair(
    scope,
    null,
    request,
    idleObservation(ports, ids, 'run-1'),
  )
  assert.equal(idle.outcome, 'AbandonedExhausted')

  const transform = await blog.observeTransformRepair(scope, null, request, 'run-1', [])
  assert.equal(transform.outcome, 'AbandonedExhausted')

  assert.equal(calls.sendPrompt.length, 0)
})

test('WHAT[behavior-diagnosis-017] shutdown_rejects_new_repair_episode_before_drain', async (t) => {
  const { ids, scope } = await setupOwner(t)

  runtime.beginBloggerShutdown(scope)

  assert.equal(
    runtime.claimRepairEpisode(scope, ids.request, ids.physical, ids.main, ids.blogger),
    'Error:Blogger runtime is shutting down',
  )

  await runtime.drainRepairEpisodes(scope)
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const turns = await import("../../../dist/Interaction/Repair/CompletedTurnSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const text = (value) => [{ type: 'text', text: value }]
const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, payload, options) => {
    captured.push({ session, text: payload, options })
    return dispatch.admittedWithReceipt('receipt-153')
  },
})
const managerOwner = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

test('WHAT[behavior-diagnosis-017] repeated invalid turns re-open repair and stable terminals complete it', () => {
  // decideRepairDefect is exercised only through the registered
  // CompletedTurnSurface name mapping: in-flight/currentRepair attempts await
  // terminal, fresh invalid terminals re-request, repairs never exhaust.
  assert.equal(turns.repairDefectDecision(false, false, null, []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, false, null, []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, false, 'tool-calls', []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, true, 'length', []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'stop', text('done')), 'NoRepair')
})
test('WHAT[behavior-diagnosis-017] concurrent gate nudge deduplicates at the dispatch boundary', async () => {
  // Two nudges on the same terminal occasion must collapse to exactly one
  // physical send; a second admission observes the first result. This is the
  // AlreadyAdmitted-not-Failed contract proven at the physical boundary.
  const base = mkdtempSync(join(tmpdir(), 'wxs-enf153-nudge-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-153',
      'rt-153',
      4242,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const owner = await dispatch.acceptHumanRootSelection(
        opened.journal,
        'ses_153_owner',
        'msg-153-owner',
        managerOwner,
      )
      assert.equal(owner.ok, true, owner.ok ? '' : owner.error)

      const captured = []
      const results = await dispatch.sendGateNudgesConcurrently(
        capturingPort(captured),
        opened.journal,
        'ses_153',
        'nudge text',
        'BusyAgentNudge',
        'interaction-repair',
        'run-153',
        owner.profile,
      )
      assert.equal(results.length, 2)
      assert.equal(results[0].ok, true, JSON.stringify(results[0]))
      assert.equal(results[1].ok, true, 'second nudge on same occasion joins, never fails')
      assert.equal(captured.length, 1, `a deduplicated nudge sends once, got ${captured.length}`)
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
test('WHAT[behavior-diagnosis-017] authority gate-nudge admission is required before any physical send', async () => {
  // Without an agent-owner profile the surface's profileOf resolves an error:
  // the nudge is refused before any physical SendPrompt reaches the port.
  const base = mkdtempSync(join(tmpdir(), 'wxs-enf153-gate-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-153g',
      'rt-153g',
      4243,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true)
    try {
      const captured = []
      const results = await dispatch.sendGateNudgesConcurrently(
        capturingPort(captured),
        opened.journal,
        'ses_noroot_153',
        'nudge',
        'BusyAgentNudge',
        'interaction-repair',
        'run-153',
        { authorityKind: 'AgentOwnerRoot', identitySeed: { kind: 'NoSuchSeed' } },
      )
      assert.equal(results.every((r) => !r.ok), true)
      assert.ok(results.every((r) => /identity seed|seed kind/i.test(r.error ?? '')), JSON.stringify(results))
      assert.equal(captured.length, 0, 'no profile → no physical send')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");
const { call, decode, message } = await import('./support/cycle.mjs')

test('WHAT[behavior-diagnosis-017] actual decoder ignores malformed messages and selects the last assistant', () => {
  const malformed = [null, {}, { info: { role: 'user', id: 'user' } }, { info: { role: 'assistant' } }]
  assert.equal(blog.decodeCycle(malformed), null)
  const result = blog.decodeCycle([message([call()], 'first'), ...malformed, message([], 'last')])
  assert.equal(result.messageId, 'last')
  assert.equal(result.decodedCalls, 0)
  assert.equal(result.decision.ok, false)
})
test('WHAT[behavior-diagnosis-017] actual decoder consumes only completed chronicle calls with valid tip evidence', () => {
  for (const part of [
    null, call({ tool: 'blog' }), call({ type: 'text' }),
    call({ state: { status: 'completed', input: { entry: 'work' } } }),
    ...['pending', 'running', 'error', 'unknown', undefined].map((status) => call({ state: { status, input: call().state.input } })),
  ]) {
    const result = decode([part])
    assert.equal(result.decodedCalls, 0)
    assert.equal(result.decision.ok, false)
  }
  assert.equal(decode([call()]).decision.ok, true)
})
}
