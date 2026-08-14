// Split from tests/unit/orchestrator/runtime.test.mjs (cutover Wave 2a); owner: change-integration.
//
// ORCH_007_NeedsReview_preserves_the_active_worktree — the NeedsReview verdict
// keeps the job's worktree (CHGINT-002/006/012). The PERSIST-009 fact-order
// half of the source test moved to
// requirements/effect-accounting/tests/runtime-persist-order.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  commitHash,
  orchestratorRuntime,
  sessionId,
  targetRef,
  worktreeIdentity,
} from '../../verification-system/tests/support/domain.mjs'

test('ORCH_007_NeedsReview_preserves_the_active_worktree', async () => {
  let removeCalls = 0

  const runtime = orchestratorRuntime.create({
    repoPath: '/repo',
    git: {
      isDirty: async () => false,
      createWorktree: async () => orchestratorRuntime.ok(worktreeIdentity('manager/job-1')),
      freezeTargetBranch: async () => orchestratorRuntime.ok(targetRef('refs/heads/main')),
      rebase: async () => orchestratorRuntime.ok(),
      ffMerge: async () => orchestratorRuntime.ok(commitHash('published-head')),
      conflictedFiles: async () => orchestratorRuntime.ok([]),
      removeWorktree: async () => {
        removeCalls += 1
        return orchestratorRuntime.ok()
      },
      hasRebaseHead: async () => false,
      listWorktrees: async () => orchestratorRuntime.ok([]),
      listManagerBranches: async () => orchestratorRuntime.ok([]),
      deleteBranch: async () => orchestratorRuntime.ok(),
      readHead: async () => orchestratorRuntime.ok(commitHash('candidate-head')),
      getTargetHead: async () => orchestratorRuntime.ok(commitHash('target-head')),
    },
    manager: {
      startManager: async () => orchestratorRuntime.ok(sessionId('ses-manager-1')),
      awaitManager: async () => orchestratorRuntime.ok(),
      reverify: async () => orchestratorRuntime.error('review barrier was not confirmed'),
      resumeManager: async () => orchestratorRuntime.ok(),
    },
    journal: {
      append: async () => orchestratorRuntime.ok({}),
    },
  })

  assert.deepEqual(
    await orchestratorRuntime.forkManager(runtime, {
      job: 'job-1',
      managerAgent: 'fast-manager',
      prompt: 'make the requested change',
      worktree: '/tmp/wt-job-1',
    }),
    { ok: true, value: { jobId: 'job-1', worktreePath: '/tmp/wt-job-1' } },
  )

  assert.deepEqual(await orchestratorRuntime.join(runtime), {
    case: 'NeedsReview',
    jobId: 'job-1',
    details: 'review barrier was not confirmed',
  })
  assert.equal(removeCalls, 0, 'a NeedsReview verdict must keep the active worktree')
})
