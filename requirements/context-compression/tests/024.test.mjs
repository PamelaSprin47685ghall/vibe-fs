import test from 'node:test'

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");

const KEY = 'ses-blogger-test'
const reqCtx = (reqId, toml = 'test') => runtime.main({
  requestId: reqId,
  mainSession: 'ses-main',
  bloggerSession: KEY,
  toml,
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'digest-1',
  frameEpoch: 0,
  deltaDigest: 'delta-1',
  observedEpoch: 0,
})

test('WHAT[context-compression-024] CTX_024_flight_claim_conflict_keeps_foreign_owner_and_rejects_stale_release', () => {
  const scope = runtime.scope()
  const ctx1 = reqCtx('req-owner-1', 'content-1')
  const ctx2 = reqCtx('req-owner-2', 'content-2')

  assert.equal(runtime.claimCurrentRequest(scope, KEY, ctx1), 'Claimed')
  assert.equal(runtime.claimCurrentRequest(scope, KEY, ctx2), 'Conflict:req-owner-1')
  assert.equal(runtime.currentRequest(scope, KEY).toml, 'content-1')

  // Stale release from owner-2 rejected
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'req-owner-2'), 'Conflict:req-owner-1')
  assert.equal(runtime.currentRequest(scope, KEY).toml, 'content-1')

  // Owner-1 releases successfully
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'req-owner-1'), 'Released')
  assert.equal(runtime.currentRequest(scope, KEY), null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const { operations, permutations, prerequisites, runFlightInterleaving, validPermutations } = await import("./support/blogger-flight-interleaving.mjs");

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
const deferred = () => {
  let resolve
  const promise = new Promise((done) => {
    resolve = done
  })
  return { promise, resolve }
}
const withOwner = async (work) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-blogger-flight-'))
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e9)}`
  const opened = await journal.JournalSurface_bootWithWriterId(
    dir,
    `writer-flight-${stamp}`,
    `rt-flight-${stamp}`,
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    return await work({ dir, opened, durable: opened.journal.journal })
  } finally {
    try {
      journal.JournalSurface_dispose(opened.journal)
    } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
}

test('WHAT[context-compression-024] every valid flight interleave keeps B unaffected by late A', async () => {
  assert.deepEqual(operations, [
    'A claims flight',
    'A repair in flight',
    'A superseded',
    'B claims flight',
    'A pending release returns',
    'A terminal callback returns',
    'B observes terminal',
  ])
  assert.deepEqual(permutations.length, 5040)
  assert.equal(validPermutations.length, 12)

  const observations = []
  for (const schedule of validPermutations) {
    const owner = await withOwner((resources) => runFlightInterleaving(schedule, resources))
    observations.push(owner)
  }

  assert.equal(observations.length, 12)
  assert.equal(new Set(observations.map(({ schedule }) => schedule.join(' → '))).size, 12)
  assert.ok(observations.every(({ flight }) => flight === 'req-b'))
  assert.ok(observations.every(({ providerDispatches }) => providerDispatches === 1))
  assert.ok(
    observations.every(
      ({ terminal }) => terminal?.phase === 'Terminal' && terminal?.disposition === 'Completed',
    ),
  )
})
test('WHAT[context-compression-024] superseded A callbacks are idempotent, never a second fatal', async (t) => {
  const { durable, opened, dir } = await (async () => {
    const inner = mkdtempSync(join(tmpdir(), 'wxs-blogger-flight-'))
    const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e9)}`
    const boot = await journal.JournalSurface_bootWithWriterId(
      inner,
      `writer-flight-${stamp}`,
      `rt-flight-${stamp}`,
      4242,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(boot.ok, true, boot.ok ? '' : JSON.stringify(boot.error))
    return { durable: boot.journal.journal, opened: boot.journal, dir: inner }
  })()
  t.after(() => {
    try {
      journal.JournalSurface_dispose(opened)
    } catch {}
    rmSync(dir, { recursive: true, force: true })
  })

  const key = 'ses-blog'
  const scope = runtime.createScope()
  t.after(() => {
    try {
      runtime.dispose(scope)
    } catch {}
  })
  const accepted = await dispatch.acceptHumanRoot(opened, key, 'msg-phys-flight', 'blogger')
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))

  const requestA = runtime.main({
    requestId: 'req-a',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'content-a',
  })
  const requestB = runtime.main({
    requestId: 'req-b',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'content-b',
  })
  const ports = {
    sessionPort: {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('ok'),
    },
    rootWorkspace: { TryRead: () => undefined },
    eventPort: {
      SubscribeTerminalListener: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminalListener: () => ({ Dispose: () => {} }),
      NotifyTerminal: () => false,
    },
  }
  const idle = (run) => ({
    quiescent: true,
    context: {
      sessionId: key,
      physicalUserMessageId: 'msg-phys-flight',
      authorityRoot: 'msg-phys-flight',
      providerRun: run,
    },
    sessionPort: ports.sessionPort,
    rootWorkspace: ports.rootWorkspace,
    eventPort: ports.eventPort,
  })

  const previousError = console.error
  const fatalRecords = []
  const previousNoFatalExit = process.env.WANXIANGSHU_NO_FATAL_EXIT
  process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
  console.error = (...args) => {
    fatalRecords.push(args.join(' '))
  }
  try {
    assert.equal(runtime.claimCurrentRequest(scope, key, requestA), 'Claimed')
    assert.equal((await blog.observeIdleRepair(scope, durable, requestA, idle('run-a1'))).outcome, 'NudgeSent')
    assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Released')
    assert.equal(runtime.claimCurrentRequest(scope, key, requestB), 'Claimed')

    // Same A duplicate callbacks, twice each: idempotent no-ops, not a second fatal.
    for (let round = 0; round < 2; round += 1) {
      assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Conflict:req-b')
      assert.equal(
        (await blog.observeIdleRepair(scope, durable, requestA, idle('run-a-late'))).outcome,
        'UnownedIdleIgnored',
      )
      assert.equal(
        (await blog.observeTransformRepair(scope, durable, requestA, 'run-a-late', [])).outcome,
        'SupersededIgnored',
      )
    }
    assert.equal(runtime.tryGetFlight(scope, key)?.requestId, 'req-b')
  } finally {
    console.error = previousError
    if (previousNoFatalExit === undefined) delete process.env.WANXIANGSHU_NO_FATAL_EXIT
    else process.env.WANXIANGSHU_NO_FATAL_EXIT = previousNoFatalExit
  }
  assert.deepEqual(fatalRecords, [])
})
test('WHAT[context-compression-024] stale release of A never releases B; new B producer never joins A slot', async (t) => {
  const inner = mkdtempSync(join(tmpdir(), 'wxs-blogger-flight-'))
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e9)}`
  const boot = await journal.JournalSurface_bootWithWriterId(
    inner,
    `writer-flight-${stamp}`,
    `rt-flight-${stamp}`,
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(boot.ok, true, boot.ok ? '' : JSON.stringify(boot.error))
  t.after(() => {
    try {
      journal.JournalSurface_dispose(boot.journal)
    } catch {}
    rmSync(inner, { recursive: true, force: true })
  })
  const durable = boot.journal.journal

  const key = 'ses-blog'
  const scope = runtime.createScope()
  t.after(() => {
    try {
      runtime.dispose(scope)
    } catch {}
  })
  const accepted = await dispatch.acceptHumanRoot(boot.journal, key, 'msg-phys-flight', 'blogger')
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))

  const requestA = runtime.main({
    requestId: 'req-a',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'content-a',
  })
  const requestB = runtime.main({
    requestId: 'req-b',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'content-b',
  })
  const ports = {
    sessionPort: {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('ok'),
    },
    rootWorkspace: { TryRead: () => undefined },
    eventPort: {
      SubscribeTerminalListener: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminalListener: () => ({ Dispose: () => {} }),
      NotifyTerminal: () => false,
    },
  }
  const idle = (request, run) => ({
    quiescent: true,
    context: {
      sessionId: key,
      physicalUserMessageId: 'msg-phys-flight',
      authorityRoot: 'msg-phys-flight',
      providerRun: run,
    },
    sessionPort: ports.sessionPort,
    rootWorkspace: ports.rootWorkspace,
    eventPort: ports.eventPort,
  })

  // A holds the committed slot with a live repair episode.
  assert.equal(runtime.claimCurrentRequest(scope, key, requestA), 'Claimed')
  assert.equal((await blog.observeIdleRepair(scope, durable, requestA, idle(requestA, 'run-a1'))).outcome, 'NudgeSent')

  // A new open producer for B is not claimed into the committed slot of A:
  // claim, repair-episode claim, and both repair entries fail closed on A.
  assert.equal(runtime.claimCurrentRequest(scope, key, requestB), 'Conflict:req-a')
  assert.match(
    runtime.claimRepairEpisode(scope, 'req-b', 'msg-phys-flight', 'ses-main-flight', key),
    /^Error:Claimed request req-b does not match active flight req-a/,
  )
  assert.equal(
    (await blog.observeIdleRepair(scope, durable, requestB, idle(requestB, 'run-b1'))).outcome,
    'UnownedIdleIgnored',
  )
  assert.equal(
    (await blog.observeTransformRepair(scope, durable, requestB, 'run-b1', [])).outcome,
    'SupersededIgnored',
  )
  assert.equal(runtime.tryGetFlight(scope, key)?.requestId, 'req-a')

  // After the exact-request handover, the stale A release still cannot clear B.
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Released')
  assert.equal(runtime.claimCurrentRequest(scope, key, requestB), 'Claimed')
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Conflict:req-b')
  assert.equal(runtime.tryGetFlight(scope, key)?.requestId, 'req-b')
})
test('WHAT[context-compression-024] same-request epoch refresh keeps ownership; B still cannot intrude', async (t) => {
  const inner = mkdtempSync(join(tmpdir(), 'wxs-blogger-flight-'))
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e9)}`
  const boot = await journal.JournalSurface_bootWithWriterId(
    inner,
    `writer-flight-${stamp}`,
    `rt-flight-${stamp}`,
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(boot.ok, true, boot.ok ? '' : JSON.stringify(boot.error))
  t.after(() => {
    try {
      journal.JournalSurface_dispose(boot.journal)
    } catch {}
    rmSync(inner, { recursive: true, force: true })
  })
  const durable = boot.journal.journal

  const key = 'ses-blog'
  const scope = runtime.createScope()
  t.after(() => {
    try {
      runtime.dispose(scope)
    } catch {}
  })
  const accepted = await dispatch.acceptHumanRoot(boot.journal, key, 'msg-phys-flight', 'blogger')
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))

  const ports = {
    sessionPort: {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('ok'),
    },
    rootWorkspace: { TryRead: () => undefined },
    eventPort: {
      SubscribeTerminalListener: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminalListener: () => ({ Dispose: () => {} }),
      NotifyTerminal: () => false,
    },
  }
  const idle = (run) => ({
    quiescent: true,
    context: {
      sessionId: key,
      physicalUserMessageId: 'msg-phys-flight',
      authorityRoot: 'msg-phys-flight',
      providerRun: run,
    },
    sessionPort: ports.sessionPort,
    rootWorkspace: ports.rootWorkspace,
    eventPort: ports.eventPort,
  })

  const first = runtime.main({
    requestId: 'req',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'v1',
    observedEpoch: 0,
  })
  assert.equal(runtime.claimCurrentRequest(scope, key, first), 'Claimed')
  assert.equal((await blog.observeIdleRepair(scope, durable, first, idle('run-1'))).outcome, 'NudgeSent')

  // Same-request epoch change creates a refreshed owner, not a new owner:
  // the flight refreshes in place and the repair episode continues (AABB).
  const refreshed = runtime.main({
    requestId: 'req',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'v2',
    observedEpoch: 1,
  })
  assert.equal(runtime.claimCurrentRequest(scope, key, refreshed), 'Refreshed')
  assert.equal(runtime.tryGetFlight(scope, key)?.toml, 'v2')
  assert.equal((await blog.observeIdleRepair(scope, durable, refreshed, idle('run-2'))).outcome, 'AabbSent')

  // A foreign B still cannot intrude on the refreshed same-request owner.
  const intruder = runtime.main({
    requestId: 'req-b',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'intruder',
  })
  assert.equal(runtime.claimCurrentRequest(scope, key, intruder), 'Conflict:req')
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-b'), 'Conflict:req')
  assert.equal(runtime.tryGetFlight(scope, key)?.requestId, 'req')
})
test('WHAT[context-compression-024] B repair after supersede waits for the dead episode drain, flight still exact', async (t) => {
  // Gap report (production defect, surface kept read-only per slice rules):
  // ReleaseCurrentRequest cancels A's repair episode but the rendezvous is
  // removed from the registry only when its CE unwinds (onCompleted). Until
  // the drain settles, B's repair-episode claim fails closed with a
  // conflicting-episode error and B's repair observations report
  // SupersededIgnored — even though the flight already belongs to B. The
  // flight/release half of the interleave stays exact throughout; only the
  // repair-observation half observes the drain lag. abandonEpisode (the
  // production supersede path) never drains; DrainRepairEpisodes runs only at
  // session delete / plugin dispose. Minimal fix suggestion: remove the
  // cancelled episode from the registry synchronously inside Cancel (or hand
  // the next claimant the already-cancelled slot), so a superseded episode
  // never squats the slot past its flight release.
  const inner = mkdtempSync(join(tmpdir(), 'wxs-blogger-flight-'))
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e9)}`
  const boot = await journal.JournalSurface_bootWithWriterId(
    inner,
    `writer-flight-${stamp}`,
    `rt-flight-${stamp}`,
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(boot.ok, true, boot.ok ? '' : JSON.stringify(boot.error))
  t.after(() => {
    try {
      journal.JournalSurface_dispose(boot.journal)
    } catch {}
    rmSync(inner, { recursive: true, force: true })
  })
  const durable = boot.journal.journal

  const key = 'ses-blog'
  const scope = runtime.createScope()
  t.after(() => {
    try {
      runtime.dispose(scope)
    } catch {}
  })
  const accepted = await dispatch.acceptHumanRoot(boot.journal, key, 'msg-phys-flight', 'blogger')
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))

  const requestA = runtime.main({
    requestId: 'req-a',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'content-a',
  })
  const requestB = runtime.main({
    requestId: 'req-b',
    mainSession: 'ses-main-flight',
    bloggerSession: key,
    toml: 'content-b',
  })
  const ports = {
    sessionPort: {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('ok'),
    },
    rootWorkspace: { TryRead: () => undefined },
    eventPort: {
      SubscribeTerminalListener: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminalListener: () => ({ Dispose: () => {} }),
      NotifyTerminal: () => false,
    },
  }
  const idle = (run) => ({
    quiescent: true,
    context: {
      sessionId: key,
      physicalUserMessageId: 'msg-phys-flight',
      authorityRoot: 'msg-phys-flight',
      providerRun: run,
    },
    sessionPort: ports.sessionPort,
    rootWorkspace: ports.rootWorkspace,
    eventPort: ports.eventPort,
  })

  assert.equal(runtime.claimCurrentRequest(scope, key, requestA), 'Claimed')
  assert.equal((await blog.observeIdleRepair(scope, durable, requestA, idle('run-a1'))).outcome, 'NudgeSent')
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Released')
  assert.equal(runtime.claimCurrentRequest(scope, key, requestB), 'Claimed')

  // The cancelled episode slot is reclaimed synchronously once its flight is
  // gone: B's own claim succeeds and its repair proceeds on a fresh episode,
  // while the flight and B's terminal state stay exact. Any surviving terminal
  // failure (settlement-failed episode) would still fail closed.
  assert.equal(runtime.claimRepairEpisode(scope, 'req-b', 'msg-phys-flight', 'ses-main-flight', key), 'Claimed')
  assert.equal(
    (await blog.observeIdleRepair(scope, durable, requestB, idle('run-b1'))).outcome,
    'NudgeSent',
  )
  assert.equal(runtime.tryGetFlight(scope, key)?.requestId, 'req-b')
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Conflict:req-b')

  // Once the drain settles, B's repair half proceeds on its own episode.
  await runtime.drainRepairEpisodes(scope)
  assert.equal(runtime.claimRepairEpisode(scope, 'req-b', 'msg-phys-flight', 'ses-main-flight', key), 'Claimed')
  assert.equal((await blog.observeIdleRepair(scope, durable, requestB, idle('run-b2'))).outcome, 'NudgeSent')
  assert.equal(runtime.tryGetFlight(scope, key)?.requestId, 'req-b')
})
test('WHAT[context-compression-024] scheduled promise order cannot move B terminal or capacity', async () => {
  await fc.assert(
    fc.asyncProperty(
      fc.array(fc.constantFrom('release', 'idle', 'transform', 'terminal'), { minLength: 4, maxLength: 8 }),
      async (order) => {
        await withOwner(async ({ opened, durable }) => {
          const key = 'ses-blog'
          const scope = runtime.createScope()
          try {
            const accepted = await dispatch.acceptHumanRoot(opened.journal, key, 'msg-phys-flight', 'blogger')
            assert.equal(accepted.ok, true)
            const requestA = runtime.main({
              requestId: 'req-a',
              mainSession: 'ses-main-flight',
              bloggerSession: key,
              toml: 'content-a',
            })
            const requestB = runtime.main({
              requestId: 'req-b',
              mainSession: 'ses-main-flight',
              bloggerSession: key,
              toml: 'content-b',
            })
            const ports = {
              sessionPort: {
                SubscribeTerminal: () => ({ Dispose: () => {} }),
                SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
                SendPrompt: async () => dispatch.admittedWithReceipt('ok'),
              },
              rootWorkspace: { TryRead: () => undefined },
              eventPort: {
                SubscribeTerminalListener: () => ({ Dispose: () => {} }),
                SubscribeFutureTerminalListener: () => ({ Dispose: () => {} }),
                NotifyTerminal: () => false,
              },
            }
            const idle = (run) => ({
              quiescent: true,
              context: {
                sessionId: key,
                physicalUserMessageId: 'msg-phys-flight',
                authorityRoot: 'msg-phys-flight',
                providerRun: run,
              },
              sessionPort: ports.sessionPort,
              rootWorkspace: ports.rootWorkspace,
              eventPort: ports.eventPort,
            })

            assert.equal(runtime.claimCurrentRequest(scope, key, requestA), 'Claimed')
            assert.equal(
              (await blog.observeIdleRepair(scope, durable, requestA, idle('run-a1'))).outcome,
              'NudgeSent',
            )
            assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Released')
            assert.equal(runtime.claimCurrentRequest(scope, key, requestB), 'Claimed')

            // Hand-rolled controllable scheduler: each late-A callback waits
            // on a deferred gate; gates release in the fast-check order, so
            // the production calls interleave in every generated resolution
            // order while B's observable state must stay identical.
            const gates = order.map(() => deferred())
            const runLate = async (kind, gate) => {
              await gate.promise
              if (kind === 'release') return runtime.releaseCurrentRequest(scope, key, 'req-a')
              if (kind === 'idle') {
                return (await blog.observeIdleRepair(scope, durable, requestA, idle('run-a-late'))).outcome
              }
              if (kind === 'transform') {
                return (await blog.observeTransformRepair(scope, durable, requestA, 'run-a-late', [])).outcome
              }
              return (await blog.observeIdleRepair(scope, durable, requestB, idle('run-b1'))).outcome
            }
            const pending = order.map((kind, index) => runLate(kind, gates[index]))
            for (const gate of gates) {
              gate.resolve()
              await Promise.resolve()
            }
            const outcomes = await Promise.all(pending)

            outcomes.forEach((outcome, index) => {
              const kind = order[index]
              if (kind === 'release') assert.equal(outcome, 'Conflict:req-b')
              else if (kind === 'idle') assert.equal(outcome, 'UnownedIdleIgnored')
              else if (kind === 'transform') assert.equal(outcome, 'SupersededIgnored')
              // B's own observation under B's flight: first nudge sends, a
              // same-run repeat is idempotent pending; while A's dead episode
              // still squats the slot the claim fails closed instead. All
              // three keep B's flight, capacity, binding and terminal intact.
              else assert.ok(['NudgeSent', 'PendingRepairWait', 'SupersededIgnored'].includes(outcome))
            })
            assert.equal(runtime.tryGetFlight(scope, key)?.requestId, 'req-b')
          } finally {
            try {
              runtime.dispose(scope)
            } catch {}
          }
        })
      },
    ),
    { seed: 20260914, numRuns: 100 },
  )
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const budget = failureOwner.budget
const projection = failureOwner.providerFailureProjection

test('WHAT[context-compression-024] CTX_024_request_scoped_repair_continues_only_for_the_current_request', () => {
  const base = {
    requestId: 'req-new',
    openRequestId: 'req-new',
    openPromptKey: 'prompt-new',
  }

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-repair',
      parentOrigin: 'InteractionRepair',
      parentPayloadDigest: 'req-new\u001fmsg-old\u001fblogger-missing-tool',
    }),
    'Current',
  )

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-repair-old',
      parentOrigin: 'InteractionRepair',
      parentPayloadDigest: 'req-old\u001fmsg-old\u001fblogger-missing-tool',
    }),
    'Superseded',
  )

  assert.equal(compression.terminalRequestOwnership({ requestId: 'req-unproven' }), 'Unproven')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const frames = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");

const request = (toml = 'work') => runtime.main({
  requestId: 'req-1',
  mainSession: 'ses-main',
  bloggerSession: 'ses-blog',
  toml,
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'd1',
  deltaDigest: 'sha-work',
})
const entry = (epoch, previous, next, run) => ({
  epoch,
  previous,
  next,
  previousCutoff: previous,
  nextCutoff: next,
  digest: `digest-${run}`,
  frame: frames.frame({ kind: 'Entry', digest: `digest-${run}`, ref: `blob-${run}`, coveredFrom: previous, coveredThrough: next }),
})
const commit = (state, value) => {
  const result = frames.applyEntry(value, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[context-compression-024] stale_terminal_cannot_reclaim_a_new_Blogger_request', () => {
  const base = {
    requestId: 'req-new',
    openRequestId: 'req-new',
    openPromptKey: 'prompt-new',
  }

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-old',
      parentOrigin: 'AgentOwnerRoot',
      parentPayloadDigest: 'old-payload',
    }),
    'Superseded',
  )

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-new',
      parentOrigin: 'ProviderRetryAttempt',
      parentPayloadDigest: 'retry-payload',
    }),
    'Current',
  )

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-repair',
      parentOrigin: 'InteractionRepair',
      parentPayloadDigest: 'req-new\u001fmsg-old\u001fblogger-missing-tool',
    }),
    'Current',
  )

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-repair-old',
      parentOrigin: 'InteractionRepair',
      parentPayloadDigest: 'req-old\u001fmsg-old\u001fblogger-missing-tool',
    }),
    'Superseded',
  )
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");

const ROOT = new URL('../../../', import.meta.url).pathname
const main = (toml = 'delta-1') => runtime.main({ toml })

test('WHAT[context-compression-024] CTX_024_flight_claim_never_overwrites_another_request', () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  assert.equal(
    runtime.claimCurrentRequest(scope, key, runtime.main({ requestId: 'req-a', toml: 'first' })),
    'Claimed',
  )
  assert.equal(
    runtime.claimCurrentRequest(scope, key, runtime.main({ requestId: 'req-b', toml: 'second' })),
    'Conflict:req-a',
  )
  assert.equal(runtime.currentRequest(scope, key).toml, 'first')
})
test('WHAT[context-compression-024] CTX_024_stale_release_cannot_clear_a_newer_owner', () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  assert.equal(
    runtime.claimCurrentRequest(scope, key, runtime.main({ requestId: 'req-a', toml: 'first' })),
    'Claimed',
  )
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-b'), 'Conflict:req-a')
  assert.equal(runtime.currentRequest(scope, key).toml, 'first')
  assert.equal(runtime.releaseCurrentRequest(scope, key, 'req-a'), 'Released')
  assert.equal(runtime.currentRequest(scope, key), null)
})
test('WHAT[context-compression-024] CTX_024_materialization_admission_is_cross_instance_single_flight', async () => {
  const firstScope = runtime.scope()
  const secondScope = runtime.scope()
  const key = 'ses-blogger'
  const first = await runtime.acquireMaterialization(firstScope, key)
  let secondAcquired = false

  const second = runtime.acquireMaterialization(secondScope, key).then((lease) => {
    secondAcquired = true
    return lease
  })

  await Promise.resolve()
  assert.equal(secondAcquired, false)

  runtime.releaseMaterialization(first)
  const secondLease = await second
  assert.equal(secondAcquired, true)
  runtime.releaseMaterialization(secondLease)
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const owner = await import("../../../dist/Context/Companion/RuntimeSurface.js");

const ctx = owner
const parkedTransform = owner
const main = () => ctx.main({ toml: 'work' })
const main2 = () => ctx.main({ toml: 'more' })
const mainRequest = () => ctx.main({ requestId: 'request-main', toml: 'work' })
const mainRequest2 = () => ctx.main({ requestId: 'request-more', toml: 'more' })
const KEY = 'ses-blog'

test('WHAT[context-compression-024] CTX_024_blogger_runtime_surface_claim_and_release_replaces_request_ownership', () => {
  const scope = parkedTransform.scope()
  const failed = ctx.main({ requestId: 'request-failed', toml: 'failed' })
  const replacement = ctx.main({ requestId: 'request-replacement', toml: 'replacement' })

  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, failed), 'Claimed')
  assert.equal(parkedTransform.releaseCurrentRequest(scope, KEY, 'request-failed'), 'Released')
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, replacement), 'Claimed')
  assert.equal(parkedTransform.releaseCurrentRequest(scope, KEY, 'request-failed'), 'Conflict:request-replacement')
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY)?.toml, 'replacement')
})
}
