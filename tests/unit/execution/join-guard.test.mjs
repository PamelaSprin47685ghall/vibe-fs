// EXEC-016 — Background Join Guard.
//
// outstandingBackground is pure: durable listable handles / active jobs / live PTY.
// JoinGuard text is instruction-only and must stay stable for ARCH-010 surface.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  commitHash,
  continuationKind,
  handleId,
  handleProjection,
  jobProgress,
  managerJobId,
  orchestratorProjection,
  promptOrigin,
  reviewBarrierId,
  roles,
  runtimeNudge,
  sessionId,
  targetRef,
  worktreeIdentity,
  worktreePath,
} from '../support/domain.mjs'
import * as TerminalPolicyModule from '../../../dist/Infrastructure/OpenCode/Host/TerminalPolicy.js'
import * as ForkTypesModule from '../../../dist/Session/ForkTypes.js'
import * as RolesModule from '../../../dist/Kernel/Roles.js'
import * as LinkageProjectionModule from '../../../dist/Journal/LinkageProjection.js'
import { HandleOwnership } from '../../../dist/Kernel/Fact.js'

/** Production HandleProjection.link takes Ownership (GREEN-7); the domain.mjs
 *  facade bind is stale, so tests call the dist entry directly. */
const link = (handle, child, targetAgent, role, current) => {
  const result = LinkageProjectionModule.HandleProjection_link(
    handle,
    child,
    targetAgent,
    role,
    HandleOwnership.DurableParentHandle,
    current,
  )
  return result.tag === 0
    ? { ok: true, value: result.fields[0] }
    : { ok: false, error: result.fields[0].cases()[result.fields[0].tag] }
}

const outstandingBackground = (() => {
  const names = Object.keys(TerminalPolicyModule)
  const key =
    names.find((n) => n === 'TerminalPolicy_outstandingBackground') ||
    names.find((n) => n.endsWith('_outstandingBackground') || n === 'outstandingBackground')
  if (!key || typeof TerminalPolicyModule[key] !== 'function') {
    throw new Error(
      `TerminalPolicy.outstandingBackground missing. Near: ${names.filter((n) => /outstanding|Terminal/.test(n)).join(', ')}`,
    )
  }
  return TerminalPolicyModule[key]
})()

const agentRole = (name) => {
  const role = ForkTypesModule.AgentRole ?? RolesModule.Role
  const value = role?.[name]
  if (value === undefined) throw new Error(`unknown Role '${name}'`)
  return value
}

test('EXEC_016_join_guard_continuation_kind_is_parseable', () => {
  const kind = continuationKind.of('JoinGuard')
  assert.equal(caseOf(promptOrigin.continuation(kind)), 'Continuation')
})

test('EXEC_016_join_guard_text_demands_join_before_finish', () => {
  assert.deepEqual(runtimeNudge.backgroundJoinGuardInstructions, [
    'Background work remains away.',
    '',
    'Receive the consequences that have become available before you finish.',
    '',
    'If useful independent work remains, continue it instead of waiting merely because something else is still away.',
    '',
    'Use horizon when orientation would change what you should do next.',
    'Use join when receiving an arrived consequence is now useful.',
  ])
  assert.match(runtimeNudge.backgroundJoinGuard, /Background work remains away/)
  assert.match(runtimeNudge.backgroundJoinGuard, /Use join/)
})

test('EXEC_016_listable_handles_are_outstanding_for_manager', () => {
  // Durable half of outstandingBackground for Manager/DevOps: listable = Active ∪ CompletedAwaitingJoin.
  let projection = handleProjection.empty
  const handle = handleId.agent('child-1')
  const linked = link(handle, sessionId('ses_child'), 'fast-coder', roles.of('Coder'), projection)
  assert.equal(linked.ok, true)
  projection = linked.value

  assert.equal(handleProjection.listable(projection).length, 1)
  assert.equal(handleProjection.joinable(projection).length, 0)

  const completed = handleProjection.complete(handle, handleProjection.completionOf('Terminal'), projection)
  assert.equal(completed.ok, true)
  projection = completed.value
  assert.equal(handleProjection.listable(projection).length, 1)
  assert.equal(handleProjection.joinable(projection).length, 1)

  const retired = handleProjection.retire(handle, projection)
  assert.equal(retired.ok, true)
  projection = retired.value
  assert.equal(handleProjection.listable(projection).length, 0)
})

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

test('EXEC_016_outstandingBackground_false_for_roles_without_join', () => {
  // No journal, no live PTY: join-less roles must never trip the guard.
  for (const name of ['Coder', 'Reviewer', 'Inspector', 'Browser', 'Inquiry', 'Distiller', 'Blogger']) {
    assert.equal(
      outstandingBackground(undefined, () => true, agentRole(name), sessionId('ses_x')),
      false,
      `${name} has no join duty`,
    )
  }
})

test('EXEC_016_devops_live_pty_alone_is_outstanding', () => {
  assert.equal(
    outstandingBackground(undefined, () => true, agentRole('DevOps'), sessionId('ses_devops')),
    true,
  )
  assert.equal(
    outstandingBackground(undefined, () => false, agentRole('DevOps'), sessionId('ses_devops')),
    false,
  )
})

test('EXEC_016_manager_without_journal_is_not_outstanding', () => {
  assert.equal(
    outstandingBackground(undefined, () => true, agentRole('Manager'), sessionId('ses_mgr')),
    false,
  )
})
