import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const child = await import("../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js");

const event = (kind, extra = {}) => ({ kind, ...extra })

test('WHAT[crash-reconciliation-001] CRASH_JOIN_abort_observation_never_becomes_completion', () => {
  const result = child.resolve('active', 'missing', ['aborted:transport'], '')
  assert.equal(result.result, 'RecoveryIncomplete')
  assert.notEqual(result.result, 'RecoveredTerminal')
  assert.notEqual(result.result, 'RecoveredAbandoned')
})
test('WHAT[crash-reconciliation-001] CRASH_JOIN_durable_abandoned_is_terminal_abandonment', () => {
  const result = child.resolve('abandoned', 'missing', [], '')
  assert.equal(result.result, 'RecoveredAbandoned')
})
test('WHAT[crash-reconciliation-001] CRASH_JOIN_parent_cancelled_abandons_missing_child', () => {
  assert.equal(child.resolve('active', 'missing', ['parent-cancelled'], '').result, 'RecoveredAbandoned')
})
test('WHAT[crash-reconciliation-001] CRASH_JOIN_active_child_is_recovered_active_not_incomplete', () => {
  assert.equal(child.resolve('active', 'active', ['active'], '').result, 'RecoveredActive')
})
test('WHAT[crash-reconciliation-001] CRASH_JOIN_restore_in_flight_remains_incomplete_without_permit', () => {
  assert.equal(child.resolve('active', 'missing', ['restore'], '').result, 'RecoveryIncomplete')
})
test('WHAT[crash-reconciliation-001] CRASH_JOIN_unreadable_snapshot_remains_incomplete', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})
test('WHAT[crash-reconciliation-001] CRASH_JOIN_terminal_proof_is_joinable_only_with_body', () => {
  assert.deepEqual(child.provenTerminal('body'), { ok: true, finality: 'Succeeded', body: 'body' })
  assert.equal(child.provenTerminal('').ok, false)
})
test('WHAT[crash-reconciliation-001] CRASH_JOIN_return_requires_proof_before_commit', () => {
  assert.equal(child.trace([
    event('TerminalProofIssued', { agent: 'a1' }),
    event('HandleCompletionCommitted', { agent: 'a1' }),
    event('JoinReturned', { agent: 'a1' }),
  ]), true)
  assert.equal(child.trace([
    event('HandleCompletionCommitted', { agent: 'a1' }),
    event('JoinReturned', { agent: 'a1' }),
  ]), false)
  assert.equal(child.trace([
    event('RawAbortObserved', { session: 's1' }),
    event('HandleCompletionCommitted', { agent: 'a1' }),
  ]), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtemp, rm } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const AttachmentSurface = await import("../../../dist/Execution/Session/Attachment/AttachmentSurface.js");
const SyncDelegateSurface = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

async function scenario(mode) {
  const directory = await mkdtemp(join(tmpdir(), `wanxiangshu-managed-child-${mode}-`))

  try {
    return await SyncDelegateSurface.managedChildReconciliationScenario(directory, mode)
  } finally {
    await rm(directory, { recursive: true, force: true })
  }
}

test('WHAT[managed-session-lifecycle-001] managed_child_effect_reconciliation_classifies_missing_matching_and_conflicting_evidence', () => {
  assert.deepEqual(AttachmentSurface.classifyObservation('missing'), {
    observation: 'missing',
    decision: 'Create',
    children: [],
  })
  assert.deepEqual(AttachmentSurface.classifyObservation('matching'), {
    observation: 'matching',
    decision: 'Adopt',
    children: ['host-child-existing'],
  })
  assert.deepEqual(AttachmentSurface.classifyObservation('conflicting'), {
    observation: 'conflicting',
    decision: 'RejectConflict',
    children: ['host-child-existing', 'host-child-conflict'],
  })
})
test('WHAT[managed-session-lifecycle-001] SyncDelegate adapter reconciles managed child effects through the Host boundary', async () => {
  const adopted = await scenario('matching')
  assert.deepEqual(adopted.listedFamilies, ['host-family-root'])
  assert.equal(adopted.createCount, 0)
  assert.equal(adopted.child, 'host-child-existing')
  assert.equal(adopted.error, '')

  const created = await scenario('missing')
  assert.deepEqual(created.listedFamilies, ['host-family-root'])
  assert.equal(created.createCount, 1)
  assert.equal(
    created.createTitle,
    `wanxiangshu:sync-delegate:v1:scope=${encodeURIComponent(created.ownerScope)}:role=engineer:agent=engineer`,
  )
  assert.equal(created.createAgent, 'engineer')
  assert.equal(created.child, 'host-child-created')
  assert.equal(created.error, '')

  const conflict = await scenario('conflicting')
  assert.deepEqual(conflict.listedFamilies, ['host-family-root'])
  assert.equal(conflict.createCount, 0)
  assert.equal(conflict.child, '')
  assert.equal(
    conflict.error,
    'sync delegate child observation conflicted: host-child-existing-a, host-child-existing-b',
  )

  const queryFailure = await scenario('query-error')
  assert.deepEqual(queryFailure.listedFamilies, ['host-family-root'])
  assert.equal(queryFailure.createCount, 0)
  assert.equal(queryFailure.child, '')
  assert.equal(
    queryFailure.error,
    'sync delegate child observation failed for host-family-root: controlled ListChildren rejection',
  )
})
test('WHAT[managed-session-lifecycle-001] same-family delegates are adopted only for the exact reuse scope', async () => {
  const result = await scenario('other-scope')

  assert.deepEqual(result.listedFamilies, ['host-family-root'])
  assert.equal(result.createCount, 1)
  assert.equal(
    result.createTitle,
    `wanxiangshu:sync-delegate:v1:scope=${encodeURIComponent(result.ownerScope)}:role=engineer:agent=engineer`,
  )
  assert.equal(result.child, 'host-child-created-exact-scope')
  assert.equal(result.error, '')
})
test('WHAT[managed-session-lifecycle-001] concurrent GetOrCreate serializes reconciliation and shares one child', async () => {
  const result = await SyncDelegateSurface.concurrentAttachedGetOrCreateScenario()

  assert.equal(result.observeCount, 1)
  assert.equal(result.createCount, 1)
  assert.deepEqual(result.children, ['concurrent-child', 'concurrent-child'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertOpaque } = await import("../../verification-system/tests/support/js-contract.mjs");

const quiescence = await import('../../../dist/OpenCode/Host/QuiescenceSurface.js')
const S = 'ses-q'
const accepted = { accepted: true, failure: null }
const rejected = (failure) => ({ accepted: false, failure })

test('WHAT[crash-reconciliation-001] Q07_restart_gate_holds_no_permit', () => {
  const before = quiescence.create()
  quiescence.beginAttempt(before, S)
  const oldPermit = quiescence.observeIdle(before, S)

  // New process incarnation: the gate is empty, so the old permit is unknown
  // to it and no idle-derived continuation can pass.
  const after = quiescence.create()
  assert.deepEqual(quiescence.tryConsume(after, oldPermit), rejected('WrongOwner'), 'restart must not inherit idle truth')
})
test('WHAT[crash-reconciliation-001] Q08_restart_or_unknown_idle_cannot_mint_new_send_authority', () => {
  const restarted = quiescence.create()
  const historicalIdle = quiescence.observeIdle(restarted, S)
  assert.deepEqual(
    quiescence.tryConsume(restarted, historicalIdle),
    rejected('NoFreshIdle'),
    'SessionIdle without a current-process BeginProviderAttempt is historical observation, not continuation authority',
  )

  quiescence.beginAttempt(restarted, S)
  const freshIdle = quiescence.observeIdle(restarted, S)
  assert.deepEqual(quiescence.tryConsume(restarted, freshIdle), accepted, 'a real current-process provider attempt restores idle authority')
})
}
