// Shared temporal harness contract tests.
//
// The production TemporalSurface owns timer/clock and durable trace translation;
// this suite pins only deterministic ordering, completion, persistence, and
// recorded-provider behavior.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as temporal from '../../../dist/Verification/TemporalSurface.js'
import {
  DeterministicCompletionSource,
  DeterministicEventQueue,
  DurableTraceEvents,
  createDurableWorld,
  createRecordedProviderPort,
  fallbackFacts,
  runTrace,
} from './support/temporal-harness.mjs'

// ── Deterministic queue enumerates races without waiting ────────────────────

test('WHAT[VERIFICATION-SYSTEM-007] deterministic queue enumerates races explicitly', () => {
  const a = ['A1', 'A2']
  const b = ['B1']
  const interleavings = DeterministicEventQueue.interleavings(a, b)
  assert.equal(interleavings.length, 3)
  const serialized = interleavings.map((items) => items.join(',')).sort()
  assert.deepEqual(serialized, ['A1,A2,B1', 'A1,B1,A2', 'B1,A1,A2'].sort())

  const permutations = DeterministicEventQueue.permutations(['A', 'B', 'C'])
  assert.equal(permutations.length, 6)
  for (const permutation of permutations) assert.deepEqual([...permutation].sort(), ['A', 'B', 'C'])
})

// ── DeterministicCompletionSource resolves in explicit order ────────────────

test('WHAT[VERIFICATION-SYSTEM-007] completion source order is explicit', async () => {
  const source = new DeterministicCompletionSource()
  const firstEntry = source.enqueue()
  const secondEntry = source.enqueue()
  assert.equal(source.pendingCount, 2)
  source.resolveId(secondEntry.id, 'second')
  source.resolveId(firstEntry.id, 'first')
  const [first, second] = await Promise.all([firstEntry.promise, secondEntry.promise])
  assert.equal(first, 'first')
  assert.equal(second, 'second')
  assert.equal(source.pendingCount, 0)
})

// ── runTrace composes virtual time + durable owner ──────────────────────────

const SESSION_A = 'ses_a'
const streamA = { kind: 'Session', session: SESSION_A }

const rootAgentFact = () => fallbackFacts.authorityRoot({ session: SESSION_A })

const advanceAgentFact = (run, previous, next, count) => ({
  family: 'Fallback',
  case: 'FallbackCursorAdvanced',
  payload: {
    SessionId: SESSION_A,
    LogicalRunId: 'run_L',
    AuthorityRootUserMessageId: 'msg_u1',
    ProviderRun: run,
    PreviousOffset: previous,
    NextOffset: next,
    ConsecutiveFailureCount: count,
    Reason: 'provider_error',
  },
})

const fallbackOf = (projection) => projection?.sessions?.[SESSION_A]?.fallback

test('WHAT[VERIFICATION-SYSTEM-007] runTrace advances clock and appends durably', async () => {
  const world = await createDurableWorld({ directory: 'temporal-runtrace', runtime: 'rt_trace', pid: 4242 })

  let fired = 0
  const handle = world.vt.port.delay(50)
  handle.delay().then(() => {
    fired += 1
  })

  const events = [
    DurableTraceEvents.appendAgentFact(streamA, undefined, rootAgentFact()),
    DurableTraceEvents.advanceClock(30),
    DurableTraceEvents.appendAgentFact(streamA, 'run_1', advanceAgentFact('run_1', 0, 1, 1)),
    DurableTraceEvents.advanceClock(20),
  ]
  await runTrace(world, events)
  await handle.delay()
  assert.equal(fired, 1, '50ms timer must fire after two advances totalling 50ms')

  const snapshot = temporal.journalSnapshot(world.journal)
  const fallback = fallbackOf(snapshot)
  assert.ok(fallback, 'fallback must exist after runTrace appends')
  assert.equal(fallback.failures, 1)
  world.dispose()
})

// ── RecordedProviderPort stub is deterministic ───────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-007] recorded provider port replays in enqueued order', async () => {
  const port = createRecordedProviderPort()
  port.enqueue({ text: 'first' })
  port.enqueue({ text: 'second' })
  assert.equal(port.pendingCount, 2)
  const first = await port.request({})
  const second = await port.request({})
  assert.deepEqual(first, { text: 'first' })
  assert.deepEqual(second, { text: 'second' })
  assert.equal(port.pendingCount, 0)
})

// ── Resource-owner drain proofs ─────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-007] journal release drains accepted append prefix and rejects later admission', async () => {
  const result = await temporal.writerReleaseDrainScenario()
  assert.deepEqual(result, {
    acceptedPrefix: 'Committed',
    afterClose: 'WriterDisposed',
    appendCalls: 2,
    closeBlockedOnAcceptedAppend: true,
    duringClose: 'WriterClosing',
  })
})

test('WHAT[VERIFICATION-SYSTEM-007] journal poison preserves the first physical failure and stops storage traffic', async () => {
  const result = await temporal.writerPoisonPreservesFirstFailureScenario()
  assert.deepEqual(result, {
    appendCalls: 2,
    first: 'CommitUnknown:append failed: disk exploded',
    second: 'WriterPoisoned:append failed: disk exploded',
  })
})

test('WHAT[VERIFICATION-SYSTEM-007] reconcile shutdown closes admission and waits for the running pass', async () => {
  const result = await temporal.reconcileSchedulerStopDrainScenario()
  assert.deepEqual(result, {
    blockedOnRunningPass: true,
    drained: true,
    rejectedKickDidNotRun: true,
    snapshotReads: 1,
  })
})

test('WHAT[VERIFICATION-SYSTEM-007] poisoned durable substrate rejects new reconcile admission', async () => {
  const result = await temporal.reconcileSchedulerDurableUnavailableScenario()
  assert.deepEqual(result, {
    rejectedWhileFirstPassBlocked: true,
    snapshotReads: 1,
  })
})

test('WHAT[VERIFICATION-SYSTEM-007] plugin scope drains reconcile and admitted Host work before disposal', async () => {
  const result = await temporal.pluginScopeStopDrainScenario()
  assert.deepEqual(result, {
    blockedBeforeRelease: true,
    disposed: true,
    lateBackgroundRejected: true,
    lateOwnedRejected: true,
    stillWaitingForReconcile: true,
    stillWaitingForOwnedWork: true,
  })
})

test('WHAT[VERIFICATION-SYSTEM-007] plugin scope preserves detached background failure instead of swallowing it', async () => {
  const result = await temporal.pluginScopeBackgroundFailureScenario()
  assert.deepEqual(result, {
    error: 'background exploded',
    lateBackgroundRejected: true,
  })
})
