import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { runVerificationToolProbe } from '../../../scripts/lib/verification-tool-probe.mjs'
import { withFinitePhysicalWait } from '../../verification-system/tests/support/finite-physical-wait.mjs'
import { writeRoutingHome } from '../../verification-system/tests/support/routing-home.mjs'

test('WHAT[provider-attempt-recovery-003] duplicate recovery observations in one live Journal share a physical send and settle', async t => {
  const root = realpathSync(mkdtempSync(join(tmpdir(), 'recovery-single-send-')))
  const store = join(root, 'store')
  mkdirSync(store)
  const home = writeRoutingHome(join(root, 'home'))
  const env = { ...process.env, HOME: home, USERPROFILE: home }
  delete env.NODE_TEST_CONTEXT
  delete env.NODE_OPTIONS
  delete env.WANXIANGSHU_NO_FATAL_EXIT
  const fixture = fileURLToPath(new URL('./support/concurrent-recovery.fixture.mjs', import.meta.url))
  const product = fileURLToPath(new URL('../../../', import.meta.url))
  const receipt = join(root, 'physical.json')
  let completed = false
  try {
    await withFinitePhysicalWait(t, { operation: 'concurrent recovery physical send', limitMs: 15000 }, async () => {
      await runVerificationToolProbe(process.execPath, [fixture, product, store, receipt], { cwd: root, env, signal: t.signal })
      const measured = JSON.parse(readFileSync(receipt, 'utf8'))
      assert.equal(measured.accepted, true)
      assert.equal(measured.closed, true)
      assert.equal(measured.sendDelta, 1)
      assert.deepEqual(measured.handled, [true, true])
      t.diagnostic(JSON.stringify({ physicalPid: measured.pid, sendDelta: measured.sendDelta, failureFacts: measured.failureFacts.length }))
    })
    completed = true
  } catch (error) {
    writeFileSync(join(root, 'stdout.log'), error.stdout ?? '')
    writeFileSync(join(root, 'stderr.log'), error.stderr ?? '')
    throw error
  } finally {
    if (completed) rmSync(root, { recursive: true, force: true })
    else t.diagnostic('CONCURRENT_RECOVERY_EVIDENCE: retained ' + root)
  }
})

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const {
  budget,
  providerFailureProjection,
  fold: foldFactsThroughOwner,
  authorityRootAccepted,
  providerFailureRecorded,
  providerRetryExhausted,
  providerSuccessRecorded,
  envelope: ownerEnvelope,
  providerFailureFactCaseNames,
} = failureOwner
const SESSION = 'ses_a'
const RUN = 'run_L'
const ROOT = 'msg_u1'
const ROOT_SELECTION_IDENTITY_SEED = {
  kind: 'RootSelection',
  participantIdentity: {
    selectedAgent: 'engineer',
    canonicalRole: 'engineer',
    persona: 'Engineer',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}
const identityFor = (run, { logical = RUN, root = ROOT } = {}) =>
  budget.attemptIdentity(SESSION, logical, root, run)
const rootFact = ({ kind = 'HumanRoot', logical = RUN, root = ROOT } = {}) =>
  authorityRootAccepted({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    authorityKind: kind,
    identitySeed: ROOT_SELECTION_IDENTITY_SEED,
  })
const failureFact = ({ run, count, logical = RUN, root = ROOT, reason = 'provider_error' }) =>
  providerFailureRecorded({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    providerRun: run,
    consecutiveFailureCount: count,
    budgetSpec: { version: 1, limit: 12 },
    reason,
  })
const exhaustedFact = ({ count }) =>
  providerRetryExhausted({
    session: SESSION,
    logicalRun: RUN,
    authorityRoot: ROOT,
    finalConsecutiveFailureCount: count,
  })
const foldFacts = (facts) =>
  foldFactsThroughOwner(facts.map((value, index) => ownerEnvelope({ seq: index + 1, session: SESSION, fact: value })))
const budgetOf = (projection) => providerFailureProjection.read(projection)

test('WHAT[provider-attempt-recovery-003] the_same_attempt_observed_twice_advances_once', () => {
  let current = providerFailureProjection.forAuthority(RUN, ROOT)
  const first = providerFailureProjection.applyFailure(identityFor('run_1'), 1, current, { version: 1, limit: 12 }, 'provider_error')
  assert.equal(first.ok, true)
  current = first.value

  assert.deepEqual(providerFailureProjection.applyFailure(identityFor('run_1'), 1, current, { version: 1, limit: 12 }, 'provider_error'), {
    ok: false,
    error: 'AlreadyObserved',
  })

  const second = providerFailureProjection.applyFailure(identityFor('run_2'), 2, current, { version: 1, limit: 12 }, 'provider_error')
  assert.equal(second.ok, true)
  assert.equal(providerFailureProjection.read(second.value).failures, 2)
})
test('WHAT[provider-attempt-recovery-003] success preserves the whole LogicalRun identity set', () => {
  let current = providerFailureProjection.forAuthority(RUN, ROOT)

  for (let attempt = 1; attempt <= 60; attempt += 1) {
    const applied = providerFailureProjection.applyFailure(identityFor(`run_${attempt}`), 1, current, { version: 1, limit: 12 }, 'provider_error')
    assert.equal(applied.ok, true, applied.ok ? '' : `advance ${attempt}: ${applied.error}`)
    current = providerFailureProjection.recordSuccess(identityFor(`success_${attempt}`), applied.value).value
  }

  const state = providerFailureProjection.read(current)
  assert.equal(state.failures, 0)
  assert.equal(state.dedupeKeys, 60)
  assert.equal(providerFailureProjection.applyFailure(identityFor('run_1'), 1, current, { version: 1, limit: 12 }, 'provider_error').error, 'AlreadyObserved')
})
test('WHAT[provider-attempt-recovery-003] a_duplicate_line_is_absorbed_because_replay_produces_it', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    failureFact({ run: 'run_1', count: 1 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.deepEqual({ failures: budgetOf(folded.value).failures }, { failures: 1 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, readFileSync, readdirSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { JournalSurface_bootWithWriterId: bootWithWriterId, JournalSurface_dispose: dispose } = await import("../../../dist/Persistence/Journal/Surface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const { budget, acceptHumanRoot: failureAcceptHumanRoot } = failureOwner
const SESSION = 'ses_ledger'
async function acceptHumanRoot(journal, userMessageId) {
  const accepted = await failureAcceptHumanRoot(journal, SESSION, userMessageId, 'engineer')
  assert.equal(accepted.ok, true, `AcceptHumanRoot failed: ${accepted.error}`)
}
async function admit(journal, providerRunName) {
  const recorded = await failureOwner.recordConfirmedFailure(
    journal,
    SESSION,
    providerRunName,
    'provider_error',
  )

  if (!recorded.ok) return recorded
  return {
    ok: true,
    value: recorded.outcome === 'RetryExhausted' ? 'RetryExhausted' : 'RetryAuthorized',
  }
}

test('WHAT[provider-attempt-recovery-003] same_failure_observed_twice_advances_once', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-dedupe-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-dedupe', 'rt_ledger_dedupe', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_dup')

    const first = await failureOwner.recordConfirmedFailure(
      journal,
      SESSION,
      'msg_asst_1',
      'provider_error',
    )
    assert.deepEqual(first, { ok: true, outcome: 'RetryAuthorized' })

    // A second observe replays the same recovery authorization. The exact
    // prompt gate dedupes its physical send; the failure count stays one.
    const second = await failureOwner.recordConfirmedFailure(
      journal,
      SESSION,
      'msg_asst_1',
      'provider_error',
    )
    assert.deepEqual(second, { ok: true, outcome: 'RetryAuthorized' })

    const state = failureOwner.snapshot(journal, SESSION)
    assert.deepEqual(
      { failures: state.failures, exhausted: state.exhausted },
      { failures: 1, exhausted: false },
    )
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
test('WHAT[provider-attempt-recovery-003] an_older_failed_run_is_absorbed_after_its_successor_advances', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-superseded-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-superseded', 'rt_ledger_superseded', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_superseded')

    assert.deepEqual(
      await failureOwner.recordConfirmedFailure(journal, SESSION, 'run-1', 'provider_error'),
      { ok: true, outcome: 'RetryAuthorized' },
    )
    assert.deepEqual(
      await failureOwner.recordConfirmedFailure(journal, SESSION, 'run-2', 'provider_error'),
      { ok: true, outcome: 'RetryAuthorized' },
    )
    assert.deepEqual(
      await failureOwner.recordConfirmedFailure(journal, SESSION, 'run-1', 'provider_error'),
      { ok: true, outcome: 'EpisodeSuperseded' },
    )

    const state = failureOwner.snapshot(journal, SESSION)
    assert.deepEqual(
      { failures: state.failures, exhausted: state.exhausted },
      { failures: 2, exhausted: false },
    )
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[provider-attempt-recovery-003] actual writer retains zero-count success and seals a success-ended failure episode', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-success-identity-'))
  const opened = await bootWithWriterId(directory, 'writer-success-identity', 'runtime-success-identity', 1, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true)
  try {
    const journal = opened.journal
    await acceptHumanRoot(journal, 'root-success-identity')
    assert.deepEqual(await failureOwner.recordConfirmedSuccess(journal, SESSION, 'root-success-identity', 'success-zero'), { ok: true })
    assert.deepEqual(await failureOwner.recordConfirmedFailure(journal, SESSION, 'failure-one', 'provider_error'), { ok: true, outcome: 'RetryAuthorized' })
    assert.deepEqual(await failureOwner.recordConfirmedSuccess(journal, SESSION, 'root-success-identity', 'success-zero'), { ok: true })
    assert.equal(failureOwner.snapshot(journal, SESSION).failures, 1)
    assert.equal((await failureOwner.recordConfirmedSuccess(journal, SESSION, 'another-authority-root', 'unseen-old-success')).ok, false)
    assert.equal(failureOwner.snapshot(journal, SESSION).failures, 1)
    assert.deepEqual(await failureOwner.recordConfirmedSuccess(journal, SESSION, 'root-success-identity', 'success-one'), { ok: true })
    assert.equal(failureOwner.snapshot(journal, SESSION).failures, 0)
    assert.deepEqual(await failureOwner.recordConfirmedFailure(journal, SESSION, 'failure-one', 'provider_error'), { ok: true, outcome: 'EpisodeSuperseded' })
    assert.equal(failureOwner.snapshot(journal, SESSION).failures, 0)
    assert.equal((await failureOwner.recordConfirmedFailure(journal, SESSION, 'success-one', 'provider_error')).ok, false)
    assert.equal((await failureOwner.recordConfirmedSuccess(journal, SESSION, 'root-success-identity', 'failure-one')).ok, false)
  } finally {
    dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[provider-attempt-recovery-003] concurrent identical ledger commands append only one failure fact', {
  todo: 'GAP-140-B: the existing CurrentState-to-Append boundary does not arbitrate concurrent commands',
}, async t => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-concurrent-'))
  const opened = await bootWithWriterId(directory, 'writer-concurrent-budget', 'runtime-concurrent-budget', 1, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true)
  let completed = false
  try {
    await acceptHumanRoot(opened.journal, 'root-concurrent-budget')
    const outcomes = await Promise.all([
      failureOwner.recordConfirmedFailure(opened.journal, SESSION, 'same-provider-run', 'same-confirmed-failure'),
      failureOwner.recordConfirmedFailure(opened.journal, SESSION, 'same-provider-run', 'same-confirmed-failure'),
    ])
    assert.ok(outcomes.every(outcome => outcome.ok))
    assert.equal(failureOwner.snapshot(opened.journal, SESSION).failures, 1)
    const eventsDirectory = join(directory, 'wanxiangshu', 'events')
    const facts = readdirSync(eventsDirectory).filter(name => name.endsWith('.ndjson')).flatMap(name =>
      readFileSync(join(eventsDirectory, name), 'utf8').trimEnd().split('\n').filter(Boolean).map(JSON.parse))
      .filter(event => event.payload?.Fact?.[1]?.[0] === 'ProviderFailure'
        && event.payload.Fact[1][1][0] === 'FailureRecorded')
    assert.equal(facts.length, 1, 'fold dedupe alone does not prevent a second physical fact')
    completed = true
  } finally {
    dispose(opened.journal)
    if (completed) rmSync(directory, { recursive: true, force: true })
    else t.diagnostic('CONCURRENT_FAILURE_BUDGET_EVIDENCE: retained ' + directory)
  }
})

test('WHAT[provider-attempt-recovery-003] a success from a legally closed old root cannot clear a new run budget', async () => {
  const store = await import('../../../dist/Persistence/EventStore/Surface.js')
  const { randomUUID } = await import('node:crypto')
  const directory = mkdtempSync(join(tmpdir(), 'wxs-budget-old-root-success-'))
  let opened = await bootWithWriterId(directory, 'old-root-writer', 'old-root-runtime', 1, '2026-01-01T00:00:00Z')
  const eventsDirectory = join(directory, 'wanxiangshu', 'events')
  const bytes = () => readdirSync(eventsDirectory).filter(name => name.endsWith('.ndjson')).sort()
    .map(name => [name, readFileSync(join(eventsDirectory, name), 'utf8')])
  try {
    assert.equal(opened.ok, true)
    await acceptHumanRoot(opened.journal, 'old-budget-root')
    const original = bytes().flatMap(([, text]) => text.trimEnd().split('\n').map(JSON.parse))
      .find(event => event.payload?.Fact?.[1]?.[1]?.[0] === 'AuthorityRootAccepted')
    assert.ok(original)
    const root = original.payload.Fact[1][1][1]
    dispose(opened.journal)
    opened = null
    const closer = store.create(directory, 'root-closure-writer')
    try {
      const id = randomUUID().replaceAll('-', '')
      const closed = await store.append(closer, [{
        id, stream: original.stream_id, type: 'JournalEnvelope', parents: [original.event_id], payloadRefs: [],
        payload: {
          ...original.payload, EventId: ['EventId', id], RuntimeId: ['RuntimeId', 'root-closure-runtime'], LocalSeq: ['LocalSeq', '1'],
          Fact: ['Agent', ['Prompt', ['AuthorityLogicalRunClosed', {
            SessionId: root.SessionId, LogicalRunId: root.LogicalRunId,
            AuthorityRootUserMessageId: root.AuthorityRootUserMessageId,
            RootKind: 'HumanRoot', ClosureWitness: 'old-root-proved-closed',
          }]]],
        },
      }])
      assert.deepEqual(closed, { ok: true, cuts: [] })
    } finally {
      store.dispose(closer)
    }
    opened = await bootWithWriterId(directory, 'new-root-writer', 'new-root-runtime', 2, '2026-01-01T00:00:01Z')
    assert.equal(opened.ok, true)
    await acceptHumanRoot(opened.journal, 'new-budget-root')
    assert.equal((await failureOwner.recordConfirmedFailure(opened.journal, SESSION, 'new-provider-failed', 'provider_error')).ok, true)
    const before = failureOwner.snapshot(opened.journal, SESSION)
    const originalBytes = bytes()
    const late = await failureOwner.recordConfirmedSuccess(opened.journal, SESSION, 'old-budget-root', 'old-provider-success')
    assert.equal(late.ok, false)
    assert.match(late.error, /different authority root/)
    assert.deepEqual(failureOwner.snapshot(opened.journal, SESSION), before)
    assert.deepEqual(bytes(), originalBytes)
    assert.deepEqual(await failureOwner.recordConfirmedSuccess(opened.journal, SESSION, 'new-budget-root', 'new-provider-success'), { ok: true })
    assert.equal(failureOwner.snapshot(opened.journal, SESSION).failures, 0)
  } finally {
    if (opened?.ok) dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { budget: providerFailureBudget, providerFailureProjection } = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const SESSION = 'ses_meta'

test('WHAT[provider-attempt-recovery-003] ProviderFailure_owner_exposes_budget_and_dedupe_state', () => {
  const initial = providerFailureProjection.forAuthority('run_L', 'msg_u1')
  const ownerIdentity = providerFailureBudget.attemptIdentity(SESSION, 'run_L', 'msg_u1', 'run_owner')
  const secondIdentity = providerFailureBudget.attemptIdentity(SESSION, 'run_L', 'msg_u1', 'run_second')
  const ownerAdvance = providerFailureProjection.applyFailure(ownerIdentity, 1, initial, { version: 1, limit: 12 }, 'provider_error')
  assert.equal(ownerAdvance.ok, true, ownerAdvance.ok ? '' : ownerAdvance.error)
  assert.deepEqual(providerFailureProjection.read(ownerAdvance.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 1,
    dedupeKeys: 1,
    budgetSpec: { version: 1, limit: 12 },
    exhausted: false,
  })

  const secondAdvance = providerFailureProjection.applyFailure(secondIdentity, 2, ownerAdvance.value, { version: 1, limit: 12 }, 'provider_error')
  assert.equal(secondAdvance.ok, true, secondAdvance.ok ? '' : secondAdvance.error)
  assert.deepEqual(providerFailureProjection.read(secondAdvance.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 2,
    dedupeKeys: 2,
    budgetSpec: { version: 1, limit: 12 },
    exhausted: false,
  })

  const duplicate = providerFailureProjection.applyFailure(ownerIdentity, 1, secondAdvance.value, { version: 1, limit: 12 }, 'provider_error')
  assert.equal(duplicate.ok, false)
  assert.equal(duplicate.error, 'AlreadyObserved')

  const successIdentity = providerFailureBudget.attemptIdentity(SESSION, 'run_L', 'msg_u1', 'run_success')
  assert.deepEqual(providerFailureProjection.read(providerFailureProjection.recordSuccess(successIdentity, ownerAdvance.value).value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 0,
    dedupeKeys: 1,
    budgetSpec: { version: 1, limit: 12 },
    exhausted: false,
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const attemptPurpose = await import("../../../dist/Participant/Provider/Attempt/PlannerSurface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");
const reconcile = await import("../../../dist/Composition/Turn/ReconcileSurface.js");

const planner = compression.attemptPlanner
const { budget, providerFailureProjection } = failureOwner
const TOOL_CAPABILITIES = [
  'BashHoneypot',
  'Edit',
  'Fetch',
  'Fission',
  'Glob',
  'Grep',
  'Move',
  'Read',
  'Remove',
  'Write',
]

test('WHAT[provider-attempt-recovery-003] each_retry_binds_a_fresh_physical_identity', () => {
  // Fresh physical identity is a ledger requirement: two distinct ProviderRun
  // identities both advance; observing one twice never advances twice.
  const first = budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'provider-1')
  const second = budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'provider-2')
  assert.notEqual(budget.dedupeKey(first), budget.dedupeKey(second))

  let current = providerFailureProjection.forAuthority('run_L', 'msg_u1')
  current = providerFailureProjection.applyFailure(first, 1, current, { version: 1, limit: 12 }, 'provider_error').value
  current = providerFailureProjection.applyFailure(second, 2, current, { version: 1, limit: 12 }, 'provider_error').value
  assert.equal(providerFailureProjection.read(current).failures, 2)

  assert.deepEqual(providerFailureProjection.applyFailure(first, 1, current, { version: 1, limit: 12 }, 'provider_error'), {
    ok: false,
    error: 'AlreadyObserved',
  })
})
}
