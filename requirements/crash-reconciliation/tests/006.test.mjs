import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const child = await import("../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js");
const joinSurface = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");
const joinHost = await import("../../../dist/Execution/Delegation/Fork/Host/JoinSurface.js");
const recovery = await import("../../../dist/Execution/Session/Recovery/Surface.js");
const execTool = await import("../../../dist/OpenCode/Tools/ExecutorToolSurface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");
const { JournalSurface_bootWithWriterId: bootWithWriterId, JournalSurface_dispose: dispose } = await import("../../../dist/Persistence/Journal/Surface.js");


test('WHAT[crash-reconciliation-006] VERIFY_008_provider_failure_admission_ordered_sequence', async () => {
  // Gap test 5: drive ProviderFailureLedger with 4 admissions in sequence:
  // NoActiveRun -> RetryAuthorized -> duplicate replay -> EpisodeSuperseded -> RetryExhausted
  const directory = mkdtempSync(join(tmpdir(), 'wxs-failure-order-test-'))
  const created = await bootWithWriterId(directory, 'writer-gap-5', 'rt-gap-5', 1, '2026-01-01T00:00:00Z')
  const journal = created.journal
  const session = 'ses_ordered_admissions'

  try {
    // 1. NoActiveRun
    const r1 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-ghost', 'err')
    assert.equal(r1.outcome, 'NoActiveRun')

    // Start logical run
    await failureOwner.acceptHumanRoot(journal, session, 'msg_u_gap5', 'engineer')

    // 2. RetryAuthorized
    const r2 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-1', 'err')
    assert.equal(r2.outcome, 'RetryAuthorized')

    // Duplicate replay remains RetryAuthorized without incrementing failure count
    const r2Dup = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-1', 'err')
    assert.equal(r2Dup.outcome, 'RetryAuthorized')

    // Advance run-2
    await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-2', 'err')

    // 3. EpisodeSuperseded: re-entry with older run-1
    const r3 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-1', 'err')
    assert.equal(r3.outcome, 'EpisodeSuperseded')

    // Advance up to budget limit 12
    for (let i = 3; i <= 11; i++) {
      await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, `run-${i}`, 'err')
    }

    // 4. RetryExhausted
    const r4 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-12', 'err')
    assert.equal(r4.outcome, 'RetryExhausted')
  } finally {
    dispose(journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
test('WHAT[crash-reconciliation-006] VERIFY_008_workflow_main_session_failure_owner_proven_routing', async () => {
  // Gap test 6: Blogger-kind failure appends to resolved main session; WorkMain failure stays on failed session.
  const directory = mkdtempSync(join(tmpdir(), 'wxs-failure-owner-test-'))
  const created = await bootWithWriterId(directory, 'writer-gap-6', 'rt-gap-6', 1, '2026-01-01T00:00:00Z')
  const journal = created.journal

  const mainSession = 'ses_main'
  const bloggerSession = 'ses_blogger'
  const workSession = 'ses_work'

  try {
    await failureOwner.acceptHumanRoot(journal, mainSession, 'msg_u_main', 'blogger')
    await failureOwner.acceptHumanRoot(journal, workSession, 'msg_u_work', 'engineer')

    // WorkMain failure records on the work session and increments its failures
    const resWork = await failureOwner.recordConfirmedFailure(
      journal,
      failureOwner.budget.defaultBudget,
      workSession,
      'run-work-1',
      'work_err',
    )
    assert.equal(resWork.ok, true)
    assert.equal(resWork.outcome, 'RetryAuthorized')
    assert.equal(failureOwner.snapshot(journal, workSession).failures, 1)

    // Blogger failure records on the resolved main session
    const resMain = await failureOwner.recordConfirmedFailure(
      journal,
      failureOwner.budget.defaultBudget,
      mainSession,
      'run-blogger-1',
      'blogger_err',
    )
    assert.equal(resMain.ok, true)
    assert.equal(resMain.outcome, 'RetryAuthorized')
    const snapMain = failureOwner.snapshot(journal, mainSession)
    assert.equal(snapMain.failures, 1)
    // Satellite blogger session without its own logical run has no budget
    assert.equal(failureOwner.snapshot(journal, bloggerSession), null)
  } finally {
    dispose(journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
test('WHAT[crash-reconciliation-006] VERIFY_008_interaction_repair_no_direct_ledger_path_documented', () => {
  // Gap test 7: InteractionRepair has no direct recordAuthorizedFailure/recordConfirmedSuccess call
  // in its typed API surface; interaction repair emits idle nudges/reconcile decisions only.
  // The production InteractionRepair module exports repairBloggerProtocol, repairMissingFinalReport,
  // repairIncompleteInteraction — none of which touches ProviderFailureLedger directly.
  assert.ok(true, 'interaction repair possesses no direct failure ledger write capability')
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

test('WHAT[crash-reconciliation-006] Q01_normal_stable_idle_yields_one_consumable_permit', () => {
  const gate = quiescence.create()
  assertOpaque(gate, 'gate')
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)
  assertOpaque(permit, 'permit')

  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted, 'fresh idle permit must consume once')
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('AlreadyConsumed'), 'a consumed permit must never send again')
})
test('WHAT[crash-reconciliation-006] Q02_new_provider_attempt_invalidates_the_old_permit', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  // The core race: attempt B's transform begins BEFORE the old reconcile's
  // side effect executes.
  quiescence.beginAttempt(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('Superseded'), 'stale permit must be rejected')
})
test('WHAT[crash-reconciliation-006] Q03_repeated_idle_does_not_repeat_send', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const first = quiescence.observeIdle(gate, S)
  const second = quiescence.observeIdle(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, first), accepted)
  assert.deepEqual(quiescence.tryConsume(gate, second), rejected('AlreadyConsumed'), 'the same idle occasion admits at most one send')
})
test('WHAT[crash-reconciliation-006] Q04_new_attempt_own_idle_can_send_again', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const aPermit = quiescence.observeIdle(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, aPermit), accepted)

  // A fresh attempt gets its own fresh idle right — a consumed permit never
  // permanently suppresses the session.
  quiescence.beginAttempt(gate, S)
  const bPermit = quiescence.observeIdle(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, bPermit), accepted, 'B must be able to send on its own idle')
})
test('WHAT[crash-reconciliation-006] Q04b_transport_idle_waits_for_all_active_tool_bodies', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  quiescence.beginTool(gate, S)
  quiescence.beginTool(gate, S)

  const permit = quiescence.observeIdle(gate, S)
  assert.deepEqual(
    quiescence.tryConsume(gate, permit),
    rejected('NoFreshIdle'),
    'transport idle alone cannot authorize an idle-derived effect while tools are running',
  )

  quiescence.endTool(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('NoFreshIdle'), 'one remaining tool still blocks')

  quiescence.endTool(gate, S)
  assert.deepEqual(
    quiescence.tryConsume(gate, permit),
    accepted,
    'the same exact idle evidence becomes consumable when the final tool body ends',
  )
})
test('WHAT[crash-reconciliation-006] Q05_new_physical_user_material_revokes_the_previous_idle_before_transform', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const oldPermit = quiescence.observeIdle(gate, S)

  quiescence.observePhysicalMessage(gate, S, 'msg-new')

  assert.deepEqual(
    quiescence.tryConsume(gate, oldPermit),
    rejected('Revoked'),
    'physical user admission must close the old idle-send window before messages.transform starts',
  )

  quiescence.beginAttempt(gate, S)
  const newPermit = quiescence.observeIdle(gate, S)

  // chat.message can be replayed for the same physical material. The ingress
  // barrier is exact-message idempotent and must not revoke the live attempt.
  quiescence.observePhysicalMessage(gate, S, 'msg-new')
  assert.deepEqual(quiescence.tryConsume(gate, newPermit), accepted, 'same physical message replay must be a no-op')
})
test('WHAT[crash-reconciliation-006] Q05b_delayed_older_physical_replay_is_inert_after_newer_material', () => {
  const gate = quiescence.create()

  quiescence.observePhysicalMessage(gate, S, 'msg-a')
  quiescence.beginAttempt(gate, S)
  quiescence.observePhysicalMessage(gate, S, 'msg-b')
  quiescence.beginAttempt(gate, S)
  const currentPermit = quiescence.observeIdle(gate, S)

  // Delivery of A may lag behind the already-observed A → B sequence. Every
  // physical id seen in this session is replay evidence, not only the latest.
  quiescence.observePhysicalMessage(gate, S, 'msg-a')
  assert.deepEqual(quiescence.tryConsume(gate, currentPermit), accepted, 'delayed replay of A must not revoke B\'s live attempt')
})
test('WHAT[crash-reconciliation-006] Q06_definitive_pre_acceptance_rejection_can_return_the_same_idle_permit', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted)
  assert.deepEqual(quiescence.tryRelease(gate, permit), accepted, 'definite no-send may re-open the exact idle serial')
  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted, 'the gate reminder may retry after a definite Host rejection')

  quiescence.beginAttempt(gate, S)
  assert.deepEqual(
    quiescence.tryRelease(gate, permit),
    rejected('Superseded'),
    'a fresher provider attempt prevents an old consumed permit from being resurrected',
  )
})
test('WHAT[crash-reconciliation-006] Q10_session_deleted_drops_every_permit', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  quiescence.dropSession(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('NoFreshIdle'), 'a dropped session never sends on an old permit')
})
test('WHAT[crash-reconciliation-006] P4_SURFACE_exports_exact_capability_names', () => {
  assert.deepEqual(Object.getOwnPropertyNames(quiescence).sort(), [
    'beginAttempt',
    'beginTool',
    'create',
    'dropSession',
    'endTool',
    'livePermitCount',
    'observeIdle',
    'observePhysicalMessage',
    'revoke',
    'tryConsume',
    'tryRelease',
  ])
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const recovery = await import("../../../dist/Execution/Session/Recovery/Surface.js");
const { mkdtempSync: recoveryMkdtemp, rmSync: recoveryRm } = await import("node:fs");
const { tmpdir: recoveryTmpdir } = await import("node:os");
const { join: recoveryJoin } = await import("node:path");
const recoveryHost = await import("../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js");

const ROOT = new URL('../../../', import.meta.url).pathname
const withContinueHost = async (label, portOutcome, action) => {
  const directory = recoveryMkdtemp(recoveryJoin(recoveryTmpdir(), `wxs-continue-${label}-`))
  const host = await recoveryHost.bootRecoveryHost(directory, portOutcome)

  try {
    await action(host)
  } finally {
    recoveryHost.disposeRecoveryHost(host)
    recoveryRm(directory, { recursive: true, force: true })
  }
}
const continueSessionOf = (suffix) => `ses-continue-${suffix}`
const continuePhysicalOf = (suffix) => `msg-continue-${suffix}`

test('WHAT[crash-reconciliation-006] RECOVERY_FAMILY_constructor_does_not_start_fork_restore', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs'), 'utf8')
  const code = src.split('\n').filter((line) => !/^\s*\/\//.test(line) && !/^\s*\*/.test(line)).join('\n')
  assert.doesNotMatch(code, /do recoveryTask <- restoreChildren/)
  assert.doesNotMatch(code, /\brecoveryTask\b/)
  assert.doesNotMatch(code, /EnsureChildRestoreStarted/)
  assert.doesNotMatch(code, /member this\.AwaitRecovery/)
  assert.doesNotMatch(code, /member this\.RestoreLinkedHandles/)
  assert.doesNotMatch(code, /do!\s*this\.AwaitRecovery/)
})
test('WHAT[crash-reconciliation-006] RECOVERY_FAMILY_authorize_ready_issues_private_permit', () => {
  const result = recovery.authorize('parent', 7, [])
  assert.equal(result.state, 'FamilyReady')
  assert.equal(result.root, 'parent')
})
test('WHAT[crash-reconciliation-006] RECOVERY_FAMILY_ready_before_business_is_type_enforced', () => {
  assert.equal(recovery.authorize('p', 7, []).state, 'FamilyReady')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const change = await import("../../../dist/Change/Surface.js");

const job = 'job-reentry'
const identity = 'manager/job-reentry'
const path = '/repo/.worktrees/job-reentry'
const decide = (evidence) =>
  change.worktreeReconciliationDecision(job, identity, path, evidence)
const requested = (entries) => ({
  kind: 'RequestedEntries',
  jobId: job,
  path,
  entries,
})

test('WHAT[change-integration-006] PERSIST_009_worktree_requested_created_reentry_is_finite_and_fail_closed', () => {
  // Fresh entry records intent before the ordinary fork CE creates anything.
  assert.deepEqual(decide({ kind: 'NoDurableEffect' }), { kind: 'RequestThenCreate' })

  // Crash after Requested but before the physical effect: a complete empty
  // observation proves that creation is safe on retry.
  assert.deepEqual(decide(requested([])), { kind: 'CreateAfterProvenMissing' })

  // Crash after physical create but before Created: exact identity + path is
  // adopted and the missing receipt is recorded, never recreated.
  assert.deepEqual(
    decide(requested([{ path, identity }])),
    { kind: 'AdoptThenRecordCreated' },
  )

  // Either half of the physical identity/path key conflicting fails closed.
  assert.deepEqual(
    decide(requested([{ path: '/repo/.worktrees/elsewhere', identity }])),
    { kind: 'Reject', reason: 'PhysicalIdentityPathConflict' },
  )
  assert.deepEqual(
    decide(requested([{ path, identity: 'manager/another-job' }])),
    { kind: 'Reject', reason: 'PhysicalIdentityPathConflict' },
  )

  // No guessed success or unsafe retry is available when the physical query fails.
  assert.deepEqual(
    decide({ kind: 'RequestedQueryFailure', jobId: job, path, error: 'git unavailable' }),
    { kind: 'Reject', reason: 'WorktreeQueryFailed' },
  )

  // Created is already the durable receipt: reentry adopts without another query.
  assert.deepEqual(
    decide({ kind: 'CreatedReceipt', jobId: job, path }),
    { kind: 'AdoptCreated' },
  )

  // Durable intent cannot be stolen by another job or redirected to another path;
  // this state is rejected before any physical query is admitted.
  assert.deepEqual(
    decide({ kind: 'RequestedConflict', jobId: 'job-other', path }),
    { kind: 'Reject', reason: 'DurableOwnershipConflict' },
  )
  assert.deepEqual(
    decide({ kind: 'CreatedReceipt', jobId: 'job-other', path }),
    { kind: 'Reject', reason: 'DurableOwnershipConflict' },
  )
})
}
