// FINALITY-028: published/released ManagerJob never revives; an active owned
// job may receive an Orchestrator append on the same session/worktree (GLORY-068).
//
// Algebra lives in OrchestratorProjection: terminal progress leaves the job in
// the map (replay is recognised) but drops it from activeJobs, recovery is
// CleanUp (not ResumeManager), later progress cannot reopen it, and a replayed
// ManagerJobCreated cannot reset it to ManagerStarted. ContinueManager refuses
// the same terminal cases in Application/Orchestration/Runtime.fs.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  commitHash,
  idValue,
  managerJobId,
  reviewBarrierId,
  sessionId,
  targetRef,
  worktreeIdentity,
  worktreePath,
} from '../../verification-system/tests/support/domain/identity.mjs'
import { isSome } from '../../verification-system/tests/support/domain/interop.mjs'
import {
  jobProgress,
  orchestratorProjection,
} from '../../verification-system/tests/support/domain/orchestrator.mjs'

const JOB = managerJobId('job_finality_028')
const MANAGER = sessionId('ses_finality_028')

const created = () =>
  orchestratorProjection.createJob(
    {
      ManagerJobId: JOB,
      ManagerSessionId: MANAGER,
      ManagerAgent: 'fast-manager',
      Byname: 'fast-manager',
      WorktreeIdentity: worktreeIdentity('wt_finality_028'),
      WorktreePath: worktreePath('/tmp/wt-finality-028'),
      TargetRef: targetRef('refs/heads/main'),
      TargetBranchFrozen: 'refs/heads/main',
    },
    orchestratorProjection.empty,
  )

const terminal = {
  published: () =>
    jobProgress.of('Published', {
      CandidateCommit: commitHash('c1'),
      ResultingTargetHead: commitHash('r1'),
    }),
  failed: () => jobProgress.of('Failed', 'boom'),
  abandoned: () => jobProgress.of('Abandoned'),
}

// Explicit enumeration: same three terminal progress kinds as the fixture
// object above, in the same order (no dynamic export discovery).
const terminalNames = ['published', 'failed', 'abandoned']

const laterProgress = [
  jobProgress.of('CandidateReady', {
    CandidateCommit: commitHash('c9'),
    PreRebaseReviewBarrierId: reviewBarrierId('bar_late'),
  }),
  jobProgress.of('Failed', 'late'),
  jobProgress.of('Abandoned'),
]

test('WHAT[FINALITY-028] a terminal ManagerJob is not active and does not resume', () => {
  for (const name of terminalNames) {
    const projection = orchestratorProjection.recordProgress(JOB, terminal[name](), created())
    const job = orchestratorProjection.tryFind(JOB, projection)
    assert.equal(isSome(job), true, `${name}: terminal job stays in the map`)
    assert.equal(orchestratorProjection.activeJobs(projection).length, 0, `${name}: not active`)
    assert.equal(
      orchestratorProjection.recoveryAction(undefined, job),
      'CleanUp',
      `${name}: recovery is CleanUp, never ResumeManager`,
    )
  }
})

test('WHAT[FINALITY-028] later progress cannot reopen a terminal ManagerJob', () => {
  for (const name of terminalNames) {
    const progress = terminal[name]
    const sealed = orchestratorProjection.recordProgress(JOB, progress(), created())
    const sealedName = orchestratorProjection.progressOf(orchestratorProjection.tryFind(JOB, sealed))
    for (const later of laterProgress) {
      const after = orchestratorProjection.recordProgress(JOB, later, sealed)
      assert.equal(orchestratorProjection.progressOf(orchestratorProjection.tryFind(JOB, after)), sealedName)
      assert.equal(orchestratorProjection.activeJobs(after).length, 0)
    }
  }
})

test('WHAT[FINALITY-028] replaying ManagerJobCreated cannot re-enlist a terminal job', () => {
  const published = orchestratorProjection.recordProgress(JOB, terminal.published(), created())
  const replayed = orchestratorProjection.createJob(
    {
      ManagerJobId: JOB,
      ManagerSessionId: sessionId('ses_other'),
      ManagerAgent: 'deep-manager',
      Byname: 'deep-manager',
      WorktreeIdentity: worktreeIdentity('wt_other'),
      WorktreePath: worktreePath('/tmp/wt-other'),
      TargetRef: targetRef('refs/heads/other'),
      TargetBranchFrozen: 'refs/heads/other',
    },
    published,
  )
  const job = orchestratorProjection.tryFind(JOB, replayed)
  assert.equal(orchestratorProjection.progressOf(job), 'Published')
  assert.equal(idValue.session(job.ManagerSessionId), 'ses_finality_028')
  assert.equal(idValue.worktreeIdentity(job.WorktreeIdentity), 'wt_finality_028')
  assert.equal(orchestratorProjection.activeJobs(replayed).length, 0)
})

test('WHAT[FINALITY-028] an active owned job continues on the same session and worktree', () => {
  const projection = created()
  const job = orchestratorProjection.tryFind(JOB, projection)
  assert.equal(orchestratorProjection.activeJobs(projection).length, 1)
  assert.equal(orchestratorProjection.recoveryAction(undefined, job), 'ResumeManager')
  assert.equal(idValue.session(job.ManagerSessionId), 'ses_finality_028')
  assert.equal(idValue.worktreeIdentity(job.WorktreeIdentity), 'wt_finality_028')
  assert.equal(idValue.worktreePath(job.WorktreePath), '/tmp/wt-finality-028')
})
