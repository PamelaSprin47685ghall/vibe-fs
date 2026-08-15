// Split from tests/unit/temporal/fallback-aabb-confluence.test.mjs (cutover Wave 2a);
// owner: verification-system (MECHANISM — shared temporal harness contract tests)
//
// The G4R-1 temporal kernel harness moved to support/temporal-harness.mjs because
// ≥2 target packages consume it (finality / change-integration / managed-session-
// lifecycle / effect-accounting / provider-attempt-recovery). These tests pin the
// harness's own deterministic primitives: explicit race enumeration, explicit
// completion order, durable runTrace composition, recorded provider replay.
// The virtual-clock contract tests moved to time-capability
// (temporal-virtual-clock.test.mjs); the fallback theorems moved to
// provider-attempt-recovery (fallback-aabb-confluence.test.mjs).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentFact,
  agentJournal,
  authorityRoot,
  fallbackProjection,
  fold,
  logicalRunId,
  providerRun,
  sessionId,
  stream,
} from './support/domain.mjs'
import {
  DeterministicCompletionSource,
  DeterministicEventQueue,
  DurableTraceEvents,
  createRecordedProviderPort,
  createVirtualClock,
  runTrace,
} from './support/temporal-harness.mjs'

// ── Deterministic queue enumerates races without waiting ────────────────────

test('WHAT[VERIFICATION-SYSTEM-007] deterministic queue enumerates races explicitly', () => {
  const a = ['A1', 'A2']
  const b = ['B1']
  const interleavings = DeterministicEventQueue.interleavings(a, b)
  // 3 choose 1 = 3 interleavings.
  assert.equal(interleavings.length, 3)
  const serialized = interleavings.map((xs) => xs.join(',')).sort()
  assert.deepEqual(serialized, ['A1,A2,B1', 'A1,B1,A2', 'B1,A1,A2'].sort())

  const perms = DeterministicEventQueue.permutations(['A', 'B', 'C'])
  assert.equal(perms.length, 6)
  // Every permutation contains same elements.
  for (const p of perms) assert.deepEqual([...p].sort(), ['A', 'B', 'C'])
})

// ── DeterministicCompletionSource resolves in explicit order ────────────────

test('WHAT[VERIFICATION-SYSTEM-007] completion source order is explicit', async () => {
  const src = new DeterministicCompletionSource()
  const e1 = src.enqueue()
  const e2 = src.enqueue()
  assert.equal(src.pendingCount, 2)
  // Resolve out of order by id — proves order is algebra, not queue lottery.
  src.resolveId(e2.id, 'second')
  src.resolveId(e1.id, 'first')
  const [first, second] = await Promise.all([e1.promise, e2.promise])
  assert.equal(first, 'first')
  assert.equal(second, 'second')
  assert.equal(src.pendingCount, 0)
})

// ── runTrace composes VirtualClock + durable port ───────────────────────────

const SES_A = 'ses_a'
const SESSION_A = sessionId(SES_A)

const rootAgentFact = () =>
  agentFact('AuthorityRootAccepted', {
    SessionId: SESSION_A,
    LogicalRunId: logicalRunId('run_L'),
    AuthorityRootUserMessageId: authorityRoot('msg_u1'),
    AuthorityKind: 'HumanRoot',
    SelectedAgent: 'fast-coder',
    PeerAgent: 'deep-coder',
    CanonicalRole: 'coder',
    SelectedTier: 'fast',
  })

const advanceAgentFact = (run, previous, next, count) =>
  agentFact('FallbackCursorAdvanced', {
    SessionId: SESSION_A,
    LogicalRunId: logicalRunId('run_L'),
    AuthorityRootUserMessageId: authorityRoot('msg_u1'),
    ProviderRun: providerRun(run),
    PreviousOffset: previous,
    NextOffset: next,
    ConsecutiveFailureCount: count,
    Reason: 'provider_error',
  })

const fallbackOf = (projection) => fallbackProjection.read(fold.session(projection, SES_A).Fallback)

test('WHAT[VERIFICATION-SYSTEM-007] runTrace advances clock and appends durably', async () => {
  const vt = createVirtualClock()
  const dir = `temporal-runtrace-${Date.now()}-${Math.random().toString(16).slice(2)}`
  const created = await agentJournal.create({ directory: dir, runtime: 'rt_trace', pid: 4242 })
  assert.equal(created.ok, true)
  const world = { vt, journal: created.journal, raw: created.raw, directory: dir, dispose: created.dispose }

  let fired = 0
  const handle = vt.port.delay(50)
  handle.delay().then(() => {
    fired += 1
  })

  const streamA = stream.session(SESSION_A)
  const events = [
    DurableTraceEvents.appendAgentFact(streamA, undefined, rootAgentFact()),
    DurableTraceEvents.advanceClock(30),
    DurableTraceEvents.appendAgentFact(streamA, providerRun('run_1'), advanceAgentFact('run_1', 0, 1, 1)),
    DurableTraceEvents.advanceClock(20),
  ]
  await runTrace(world, events)
  await handle.delay()
  assert.equal(fired, 1, '50ms timer must fire after two advances totalling 50ms')
  const snap = agentJournal.snapshot(world.journal)
  const f = fallbackOf(snap)
  assert.ok(f, 'fallback must exist after runTrace appends')
  assert.equal(f.failures, 1)
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
