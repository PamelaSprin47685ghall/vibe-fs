// Split from tests/unit/execution/join-guard.test.mjs (cutover Wave 2a);
// owner: change-integration. EXEC-016 outstandingBackground 的 orchestrator 半边：
// 活跃 ManagerJob 对 Orchestrator 是 outstanding（ORCH-007 job 投影；
// handle/PTY 判定 → managed-session-lifecycle / process-execution）。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  commitHash,
  jobProgress,
  managerJobId,
  orchestratorProjection,
  sessionId,
  targetRef,
  worktreeIdentity,
  worktreePath,
} from '../../verification-system/tests/support/domain.mjs'

test('EXEC_016_active_manager_jobs_are_outstanding_for_orchestrator', () => {
  let jobs = orchestratorProjection.empty
  jobs = orchestratorProjection.createJob(
    {
      ManagerJobId: managerJobId('job_1'),
      ManagerSessionId: sessionId('ses_mgr'),
      ManagerAgent: 'fast-manager',
      WorktreeIdentity: worktreeIdentity('manager/job_1'),
      WorktreePath: worktreePath('/tmp/wt'),
      TargetRef: targetRef('refs/heads/main'),
      TargetBranchFrozen: 'main',
    },
    jobs,
  )
  assert.equal(orchestratorProjection.activeJobs(jobs).length, 1)

  jobs = orchestratorProjection.recordProgress(
    managerJobId('job_1'),
    jobProgress.of('Published', {
      CandidateCommit: commitHash('c1'),
      ResultingTargetHead: commitHash('r1'),
    }),
    jobs,
  )
  assert.equal(orchestratorProjection.activeJobs(jobs).length, 0)
})
