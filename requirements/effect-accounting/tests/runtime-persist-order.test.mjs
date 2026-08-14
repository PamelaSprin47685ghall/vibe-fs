// Split from tests/unit/orchestrator/runtime.test.mjs (cutover Wave 2a); owner: effect-accounting.
//
// PERSIST-009 fact order through the live Orchestrator runtime: a fork writes
// WorktreeCreateRequested → WorktreeCreated → ManagerJobCreated, in that order
// (Runtime.fs). The NeedsReview-worktree-preservation half of the source test
// moved to requirements/change-integration/tests/runtime.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  commitHash,
  orchestratorRuntime,
  sessionId,
  targetRef,
  worktreeIdentity,
} from '../../verification-system/tests/support/domain.mjs'

// AgentFact is a single-case dispatch: caseOf(fact) === 'Orchestrator' and the
// inner Orchestrator fact case sits at fields[0].
const orchestratorFactName = (fact) => caseOf(fact.fields[0])

test('PERSIST_009_fork_appends_worktree_request_created_then_manager_job', async () => {
  const appended = []

  const runtime = orchestratorRuntime.create({
    repoPath: '/repo',
    git: {
      isDirty: async () => false,
      createWorktree: async () => orchestratorRuntime.ok(worktreeIdentity('manager/job-1')),
      freezeTargetBranch: async () => orchestratorRuntime.ok(targetRef('refs/heads/main')),
      rebase: async () => orchestratorRuntime.ok(),
      ffMerge: async () => orchestratorRuntime.ok(commitHash('published-head')),
      conflictedFiles: async () => orchestratorRuntime.ok([]),
      removeWorktree: async () => orchestratorRuntime.ok(),
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
      append: async (_streamId, fact) => {
        appended.push(orchestratorFactName(fact))
        return orchestratorRuntime.ok({})
      },
    },
  })

  const out = await orchestratorRuntime.forkManager(runtime, {
    job: 'job-1',
    managerAgent: 'fast-manager',
    prompt: 'make the requested change',
    worktree: '/tmp/wt-job-1',
  })
  assert.equal(out.ok, true, JSON.stringify(out.error))

  // PERSIST-009 order: WorktreeCreateRequested → WorktreeCreated → ManagerJobCreated.
  assert.deepEqual(appended, ['WorktreeCreateRequested', 'WorktreeCreated', 'ManagerJobCreated'])
})
