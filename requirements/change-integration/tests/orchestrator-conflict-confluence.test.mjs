// Moved from tests/unit/temporal/orchestrator-conflict-confluence.test.mjs (cutover Wave 2a); owner: change-integration
// tests/unit/temporal/orchestrator-conflict-confluence.test.mjs — G4R-2 race extraction.
//
// Proves orchestrator conflict / restart-publish algebra on production Fold +
// OrchestratorProjection.recoveryAction. No Host. No invented business rules.
//
// Feedstock (Host-level Long Stroke / temporal algebra; TOML canaries retired in G4R-4):
//   tests/e2e/entry.test.mjs + scenarios/long-stroke.toml (publish-conflict stroke)
//   Former multi-canary TOMLs orchestrator-restart-publish{,-conflict}.toml deleted.
//
// Production symbols:
//   Fold.foldEnvelope / Fold.apply          — Composition/Durable/Fold.fs
//   OrchestratorProjection.recoveryAction   — Change/Orchestration/OrchestratorProjection.fs (ORCH-007)
//   OrchestratorFactCases                   — Kernel/Fact.fs
//   agentJournal + dropEphemeral            — durable restart (G4R §12)
//
// Race model:
//   Independent jobs commute (fold(A;B) == fold(B;A)).
//   Contended head inputs yield unique ORCH-007 actions (PublishClaimed order;
//   stale target → RebaseAndReviewAgain; GetTargetHead fail → FailClosed).
//   Latest durable progress wins; replayed create does not resurrect Published.
//   dropEphemeral preserves ConflictPending / PublishClaimed recovery.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentFact,
  agentJournal,
  commitHash,
  envelope,
  fact,
  fold,
  idValue,
  listItems,
  managerJobId,
  orchestratorProjection,
  reviewBarrierId,
  sessionId,
  stream,
  targetRef,
  toList,
  worktreeIdentity,
  worktreePath,
} from '../../verification-system/tests/support/domain.mjs'
import {
  DeterministicEventQueue,
  createVirtualClock,
  dropEphemeral,
  foldEnvelopes,
} from '../../verification-system/tests/support/temporal-harness.mjs'

const JOB_A = managerJobId('job_a')
const JOB_B = managerJobId('job_b')
const SES_A = 'ses_orch_a'
const SES_B = 'ses_orch_b'
const MANAGER_A = sessionId(SES_A)
const MANAGER_B = sessionId(SES_B)

const createFact = (job, manager, wt = 'wt_a') =>
  fact('ManagerJobCreated', {
    ManagerJobId: job,
    ManagerSessionId: manager,
    ManagerAgent: 'fast-manager',
    Byname: 'Road',
    WorktreeIdentity: worktreeIdentity(wt),
    WorktreePath: worktreePath(`/tmp/${wt}`),
    TargetRef: targetRef('refs/heads/main'),
    TargetBranchFrozen: 'refs/heads/main',
  })

const candidateFact = (job, commit = 'c1', barrier = 'bar_1') =>
  fact('CandidateReady', {
    ManagerJobId: job,
    CandidateCommit: commitHash(commit),
    PreRebaseReviewBarrierId: reviewBarrierId(barrier),
  })

const conflictFact = (job, { commit = 'c1', head = 'h1', files = ['publish_proof.txt'] } = {}) =>
  fact('ConflictDetected', {
    ManagerJobId: job,
    CandidateCommit: commitHash(commit),
    TargetHeadSnapshot: commitHash(head),
    ConflictFiles: toList(files),
    DiagnosticsDigest: 'conflict-digest',
  })

const rebasedFact = (job, { rebased = 'r1', head = 'h1', barrier = 'bar_2' } = {}) =>
  fact('RebasedCandidateReady', {
    ManagerJobId: job,
    RebasedCommit: commitHash(rebased),
    TargetHeadSnapshot: commitHash(head),
    PostRebaseReviewBarrierId: reviewBarrierId(barrier),
  })

const publishClaimedFact = (job, { expected = 'h1' } = {}) =>
  fact('PublishClaimed', {
    ManagerJobId: job,
    TargetRef: targetRef('refs/heads/main'),
    ExpectedHead: commitHash(expected),
  })

const publishedFact = (job, { candidate = 'c1', head = 'r1' } = {}) =>
  fact('Published', {
    ManagerJobId: job,
    CandidateCommit: commitHash(candidate),
    ResultingTargetHead: commitHash(head),
  })

const env = (seq, manager, value) =>
  envelope({ seq, stream: stream.session(manager), fact: value })

const foldFacts = (facts, manager = MANAGER_A) =>
  foldEnvelopes(facts.map((value, index) => env(index + 1, manager, value)))

const jobOf = (projection, jobId) =>
  orchestratorProjection.tryFind(jobId, fold.orchestrator(projection))

const progressOf = (projection, jobId) =>
  orchestratorProjection.progressOf(jobOf(projection, jobId))

const actionOf = (projection, jobId, head) =>
  orchestratorProjection.recoveryAction(
    head === undefined ? undefined : commitHash(head),
    jobOf(projection, jobId),
  )

const payloadOf = (projection, jobId, head) =>
  orchestratorProjection.recoveryActionPayload(
    head === undefined ? undefined : commitHash(head),
    jobOf(projection, jobId),
  )

const createAgentFact = (job, manager, wt = 'wt_a') =>
  agentFact('ManagerJobCreated', {
    ManagerJobId: job,
    ManagerSessionId: manager,
    ManagerAgent: 'fast-manager',
    Byname: 'Road',
    WorktreeIdentity: worktreeIdentity(wt),
    WorktreePath: worktreePath(`/tmp/${wt}`),
    TargetRef: targetRef('refs/heads/main'),
    TargetBranchFrozen: 'refs/heads/main',
  })

const conflictAgentFact = (job, { commit = 'c1', head = 'h1', files = ['publish_proof.txt'] } = {}) =>
  agentFact('ConflictDetected', {
    ManagerJobId: job,
    CandidateCommit: commitHash(commit),
    TargetHeadSnapshot: commitHash(head),
    ConflictFiles: toList(files),
    DiagnosticsDigest: 'conflict-digest',
  })

const rebasedAgentFact = (job, { rebased = 'r1', head = 'h1', barrier = 'bar_2' } = {}) =>
  agentFact('RebasedCandidateReady', {
    ManagerJobId: job,
    RebasedCommit: commitHash(rebased),
    TargetHeadSnapshot: commitHash(head),
    PostRebaseReviewBarrierId: reviewBarrierId(barrier),
  })

const publishClaimedAgentFact = (job, { expected = 'h1' } = {}) =>
  agentFact('PublishClaimed', {
    ManagerJobId: job,
    TargetRef: targetRef('refs/heads/main'),
    ExpectedHead: commitHash(expected),
  })

// ── Theorem 1: independent jobs commute ─────────────────────────────────────
//
// Two Manager jobs (distinct ManagerJobId / ManagerSessionId) are independent.
// Any interleaving of their create+candidate sequences must fold to the same
// per-job progress. Algebraic shape of concurrent fork-manager races without Host.

test('WHAT[CHGINT-004] THEOREM_orchestrator_independent_jobs_confluent_across_interleavings', () => {
  const seqA = [
    env(10, MANAGER_A, createFact(JOB_A, MANAGER_A, 'wt_a')),
    env(11, MANAGER_A, candidateFact(JOB_A, 'ca', 'bar_a')),
  ]
  const seqB = [
    env(20, MANAGER_B, createFact(JOB_B, MANAGER_B, 'wt_b')),
    env(21, MANAGER_B, candidateFact(JOB_B, 'cb', 'bar_b')),
  ]

  const foldAB = foldEnvelopes([...seqA, ...seqB])
  const foldBA = foldEnvelopes([...seqB, ...seqA])
  assert.equal(foldAB.ok, true, foldAB.ok ? '' : JSON.stringify(foldAB.error))
  assert.equal(foldBA.ok, true, foldBA.ok ? '' : JSON.stringify(foldBA.error))

  assert.equal(progressOf(foldAB.value, JOB_A), 'CandidateReady')
  assert.equal(progressOf(foldAB.value, JOB_B), 'CandidateReady')
  assert.equal(progressOf(foldBA.value, JOB_A), 'CandidateReady')
  assert.equal(progressOf(foldBA.value, JOB_B), 'CandidateReady')
  assert.equal(actionOf(foldAB.value, JOB_A, 'h1'), 'RebaseReviewPublish')
  assert.equal(actionOf(foldBA.value, JOB_B, 'h1'), 'RebaseReviewPublish')

  for (const interleaving of DeterministicEventQueue.interleavings(seqA, seqB)) {
    const folded = foldEnvelopes(interleaving)
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    assert.equal(progressOf(folded.value, JOB_A), progressOf(foldAB.value, JOB_A))
    assert.equal(progressOf(folded.value, JOB_B), progressOf(foldAB.value, JOB_B))
    assert.equal(actionOf(folded.value, JOB_A, 'h1'), 'RebaseReviewPublish')
    assert.equal(actionOf(folded.value, JOB_B, 'h1'), 'RebaseReviewPublish')
  }
})

// ── Theorem 2: ConflictDetected → ResumeConflictResolution ──────────────────
//
// E2E conflict feedstock: crash at first conflict-resume; recovery must resume
// the SAME Manager with conflicted files — not re-fork (ORCH-003 / ORCH-007).

test('WHAT[CHGINT-005] THEOREM_conflict_detected_folds_to_resume_conflict_resolution', () => {
  const folded = foldFacts([
    createFact(JOB_A, MANAGER_A),
    candidateFact(JOB_A),
    conflictFact(JOB_A, { files: ['publish_proof.txt', 'src/a.fs'] }),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  assert.equal(progressOf(folded.value, JOB_A), 'ConflictPending')
  // Head is not an input for ConflictPending — same action with or without it.
  assert.equal(actionOf(folded.value, JOB_A, undefined), 'ResumeConflictResolution')
  assert.equal(actionOf(folded.value, JOB_A, 'h9'), 'ResumeConflictResolution')

  const payload = payloadOf(folded.value, JOB_A, 'h1')
  assert.deepEqual(
    {
      commit: idValue.commit(payload.CandidateCommit),
      files: listItems(payload.ConflictFiles),
    },
    { commit: 'c1', files: ['publish_proof.txt', 'src/a.fs'] },
  )
})

// ── Theorem 3: PublishClaimed three-branch order (ORCH-007) ─────────────────
//
 // Fixed order: already-published → unchanged expected → else rebase-again.
// Order matters: checking "unchanged" first would re-attempt a succeeded ff.

test('WHAT[CHGINT-007] THEOREM_publish_claimed_three_branch_order_is_fixed', () => {
  const folded = foldFacts([
    createFact(JOB_A, MANAGER_A),
    candidateFact(JOB_A),
    rebasedFact(JOB_A, { rebased: 'r1', head: 'h1' }),
    publishClaimedFact(JOB_A, { expected: 'h1' }),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(progressOf(folded.value, JOB_A), 'PublishClaimed')

  // Branch 1: currentHead == rebasedCommit → BackfillPublished.
  assert.equal(actionOf(folded.value, JOB_A, 'r1'), 'BackfillPublished')
  assert.deepEqual(
    {
      rebased: idValue.commit(payloadOf(folded.value, JOB_A, 'r1').RebasedCommit),
      head: idValue.commit(payloadOf(folded.value, JOB_A, 'r1').ResultingTargetHead),
    },
    { rebased: 'r1', head: 'r1' },
  )

  // Branch 2: currentHead == ExpectedHead → AttemptPublish.
  assert.equal(actionOf(folded.value, JOB_A, 'h1'), 'AttemptPublish')
  assert.deepEqual(
    {
      rebased: idValue.commit(payloadOf(folded.value, JOB_A, 'h1').RebasedCommit),
      expected: idValue.commit(payloadOf(folded.value, JOB_A, 'h1').ExpectedHead),
    },
    { rebased: 'r1', expected: 'h1' },
  )

  // Branch 3: stale / moved target → RebaseAndReviewAgain (discard post-rebase witness).
  assert.equal(actionOf(folded.value, JOB_A, 'h9'), 'RebaseAndReviewAgain')
})

// ── Theorem 4: stale head on RebasedCandidateReady ──────────────────────────
//
// ORCH-005 allows target to move while unlocked; REVIEW-008 forbids reusing the
// post-rebase witness against the wrong base.

test('WHAT[CHGINT-013] THEOREM_stale_target_on_rebased_candidate_discards_witness', () => {
  const folded = foldFacts([
    createFact(JOB_A, MANAGER_A),
    candidateFact(JOB_A),
    rebasedFact(JOB_A, { rebased: 'r1', head: 'h1' }),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(progressOf(folded.value, JOB_A), 'RebasedCandidateReady')

  assert.equal(actionOf(folded.value, JOB_A, 'h1'), 'AttemptPublish')
  assert.equal(actionOf(folded.value, JOB_A, 'h2'), 'RebaseAndReviewAgain')
})

// ── Theorem 5: GetTargetHead failure fails closed (ORCH-008) ────────────────

test('WHAT[CHGINT-008] THEOREM_unreadable_target_head_fails_closed', () => {
  for (const facts of [
    [createFact(JOB_A, MANAGER_A), candidateFact(JOB_A), rebasedFact(JOB_A)],
    [
      createFact(JOB_A, MANAGER_A),
      candidateFact(JOB_A),
      rebasedFact(JOB_A),
      publishClaimedFact(JOB_A),
    ],
  ]) {
    const folded = foldFacts(facts)
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    assert.equal(actionOf(folded.value, JOB_A, undefined), 'FailClosed')
    assert.equal(
      payloadOf(folded.value, JOB_A, undefined),
      'GetTargetHead failed; ORCH-008 forbids falling back to HEAD',
    )
  }
})

// ── Theorem 6: latest progress only; published is terminal exactly-once ─────

test('WHAT[CHGINT-006] THEOREM_latest_progress_wins_and_published_is_terminal', () => {
  const folded = foldFacts([
    createFact(JOB_A, MANAGER_A),
    candidateFact(JOB_A),
    conflictFact(JOB_A),
    rebasedFact(JOB_A),
    publishClaimedFact(JOB_A),
    publishedFact(JOB_A),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(progressOf(folded.value, JOB_A), 'Published')
  assert.equal(orchestratorProjection.activeJobs(fold.orchestrator(folded.value)).length, 0)
  assert.equal(actionOf(folded.value, JOB_A, 'r1'), 'CleanUp')

  // Replayed create after Published must not reopen the job (restart feedstock).
  const replayCreate = fold.apply(folded.value, [
    env(99, MANAGER_A, createFact(JOB_A, MANAGER_A)),
  ])
  assert.equal(replayCreate.ok, true, replayCreate.ok ? '' : JSON.stringify(replayCreate.error))
  assert.equal(progressOf(replayCreate.value, JOB_A), 'Published')
  assert.equal(orchestratorProjection.activeJobs(fold.orchestrator(replayCreate.value)).length, 0)
  assert.equal(actionOf(replayCreate.value, JOB_A, 'r1'), 'CleanUp')
})

// ── Theorem 7: PublishClaimed without prior rebase is rejected ──────────────
//
 // Fold refuses PublishClaimed when no RebasedCandidateReady established the
// rebased commit (ORCH-004). Race that claims without rebase must fail closed.

test('WHAT[CHGINT-003] THEOREM_publish_claimed_without_rebased_candidate_is_rejected', () => {
  const folded = foldFacts([
    createFact(JOB_A, MANAGER_A),
    candidateFact(JOB_A),
    publishClaimedFact(JOB_A),
  ])
  assert.equal(folded.ok, false)
})

// ── Theorem 8: dropEphemeral preserves conflict recovery ────────────────────
//
// G4R §12 + E2E conflict restart: durable ConflictDetected survives crash;
// recovered projection still yields ResumeConflictResolution (not ManagerStarted).

test('WHAT[CHGINT-005] THEOREM_drop_ephemeral_preserves_conflict_pending_recovery', async () => {
  const dir = `temporal-orch-conflict-${Date.now()}-${Math.random().toString(16).slice(2)}`
  const vt1 = createVirtualClock()
  const created1 = await agentJournal.create({
    directory: dir,
    runtime: 'rt_orch_1',
    pid: 4242,
    startedAt: '2026-01-01T00:00:00Z',
  })
  assert.equal(created1.ok, true, created1.ok ? '' : String(created1.error))
  const world1 = {
    vt: vt1,
    journal: created1.journal,
    raw: created1.raw,
    directory: dir,
    dispose: created1.dispose,
  }

  const streamA = stream.session(MANAGER_A)
  const a1 = await agentJournal.appendAgent(streamA, undefined, createAgentFact(JOB_A, MANAGER_A), world1.journal)
  assert.equal(a1.ok, true, a1.ok ? '' : JSON.stringify(a1.error))
  const a2 = await agentJournal.appendAgent(streamA, undefined, conflictAgentFact(JOB_A), world1.journal)
  assert.equal(a2.ok, true, a2.ok ? '' : JSON.stringify(a2.error))

  const before = agentJournal.snapshot(world1.journal)
  assert.equal(progressOf(before, JOB_A), 'ConflictPending')
  assert.equal(actionOf(before, JOB_A, 'h1'), 'ResumeConflictResolution')

  const world2 = await dropEphemeral(world1, { runtime: 'rt_orch_recovered', pid: 4243 })
  const after = agentJournal.snapshot(world2.journal)
  assert.equal(progressOf(after, JOB_A), 'ConflictPending')
  assert.equal(actionOf(after, JOB_A, 'h1'), 'ResumeConflictResolution')
  assert.equal(actionOf(after, JOB_A, undefined), 'ResumeConflictResolution')

  const payload = payloadOf(after, JOB_A, 'h1')
  assert.deepEqual(
    {
      commit: idValue.commit(payload.CandidateCommit),
      files: listItems(payload.ConflictFiles),
    },
    { commit: 'c1', files: ['publish_proof.txt'] },
  )

  world2.dispose()
})

// ── Theorem 9: dropEphemeral preserves PublishClaimed three-branch algebra ──
//
// Crash inside the CAS window (ORCH-005 restart-publish feedstock): durable
// PublishClaimed survives; recovered recoveryAction still respects branch order.

test('WHAT[CHGINT-007] THEOREM_drop_ephemeral_preserves_publish_claimed_branch_algebra', async () => {
  const dir = `temporal-orch-claim-${Date.now()}-${Math.random().toString(16).slice(2)}`
  const vt1 = createVirtualClock()
  const created1 = await agentJournal.create({
    directory: dir,
    runtime: 'rt_orch_claim_1',
    pid: 4242,
    startedAt: '2026-01-01T00:00:00Z',
  })
  assert.equal(created1.ok, true, created1.ok ? '' : String(created1.error))
  const world1 = {
    vt: vt1,
    journal: created1.journal,
    raw: created1.raw,
    directory: dir,
    dispose: created1.dispose,
  }

  const streamA = stream.session(MANAGER_A)
  assert.equal(
    (await agentJournal.appendAgent(streamA, undefined, createAgentFact(JOB_A, MANAGER_A), world1.journal)).ok,
    true,
  )
  assert.equal(
    (await agentJournal.appendAgent(streamA, undefined, rebasedAgentFact(JOB_A), world1.journal)).ok,
    true,
  )
  assert.equal(
    (await agentJournal.appendAgent(streamA, undefined, publishClaimedAgentFact(JOB_A), world1.journal)).ok,
    true,
  )

  const before = agentJournal.snapshot(world1.journal)
  assert.equal(progressOf(before, JOB_A), 'PublishClaimed')
  assert.equal(actionOf(before, JOB_A, 'r1'), 'BackfillPublished')
  assert.equal(actionOf(before, JOB_A, 'h1'), 'AttemptPublish')
  assert.equal(actionOf(before, JOB_A, 'h9'), 'RebaseAndReviewAgain')

  const world2 = await dropEphemeral(world1, { runtime: 'rt_orch_claim_recovered', pid: 4243 })
  const after = agentJournal.snapshot(world2.journal)
  assert.equal(progressOf(after, JOB_A), 'PublishClaimed')
  assert.equal(actionOf(after, JOB_A, 'r1'), 'BackfillPublished')
  assert.equal(actionOf(after, JOB_A, 'h1'), 'AttemptPublish')
  assert.equal(actionOf(after, JOB_A, 'h9'), 'RebaseAndReviewAgain')
  assert.equal(actionOf(after, JOB_A, undefined), 'FailClosed')

  world2.dispose()
})
