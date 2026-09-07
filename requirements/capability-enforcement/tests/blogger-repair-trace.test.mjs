// requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs
// ENF-021 single repair owner trace through the compiled F# owner.
//
// Every repair outcome and every physical effect count comes from
// BloggerCoordinator.observeTransformRepair / observeIdleRepair driven via
// dist/Enforcer/BlogSurface.js against a real process-local PluginRuntimeScope.
// Quiescence comes from a real SessionQuiescenceGate created inside BlogSurface:
// the observation states `quiescent: true|false` explicitly, and only a
// quiescent observation mints a provider attempt + idle permit for the exact
// session/physical message. The test stubs ONLY the three physical ports
// (session port, root workspace reader, event observation port) over primitive
// identity strings and records every call without dedupe. Journal authority
// comes from the production boot/accept surfaces; the exact live flight comes
// from the production claim entry. No fake owner.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'

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
  return { ids, durable, scope, request, ports, calls }
}

// quiescent: false => the turn carries no permit => the tool-not-quiescent
// observation. quiescent: true (default) => BlogSurface begins the exact
// provider attempt on a real gate and observes its idle for this session.
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

test('WHAT[ENF-021] repeat_terminal_idle_is_idempotent_no_duplicate_nudge', async (t) => {
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

test('WHAT[ENF-021] idle_without_quiescence_permit_spends_no_budget', async (t) => {
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

test('WHAT[ENF-021] next_terminal_sends_at_most_one_aabb_then_abandons', async (t) => {
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

test('WHAT[ENF-021] transform_and_idle_interleave_resolves_to_single_owner', async (t) => {
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

test('WHAT[ENF-021] transform_on_aabb_claimed_terminal_waits_without_double_spend', async (t) => {
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

test('WHAT[ENF-021] repair_without_journal_abandons_without_physical_sends', async (t) => {
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

test('WHAT[ENF-021] shutdown_rejects_new_repair_episode_before_drain', async (t) => {
  const { ids, scope } = await setupOwner(t)

  runtime.beginBloggerShutdown(scope)

  assert.equal(
    runtime.claimRepairEpisode(scope, ids.request, ids.physical, ids.main, ids.blogger),
    'Error:Blogger runtime is shutting down',
  )

  await runtime.drainRepairEpisodes(scope)
})
