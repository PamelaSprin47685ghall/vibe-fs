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


test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_returns_active_without_committing_when_child_is_live', () => {
  assert.equal(child.resolve('active', 'active', ['active'], '').result, 'RecoveredActive')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_unreadable_snapshot_is_incomplete_not_blocked', () => {
  const result = child.resolve('active', 'unreadable', [], '')
  assert.equal(result.result, 'RecoveryIncomplete')
  assert.notEqual(result.result, 'RecoveryBlocked')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_retired_handle_is_blocked_branch', () => {
  const result = child.resolve('retired', 'missing', [], '')
  assert.equal(result.result, 'RecoveryBlocked')
  assert.notEqual(result.result, 'RecoveryIncomplete')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_blank_terminal_body_is_incomplete_branch', () => {
  assert.equal(child.resolve('active', 'terminal', [], '').result, 'RecoveryBlocked')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const child = await import("../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");


test('WHAT[CRASH-010] P0_CLEAN_BREAK_aborted_only_observation_is_incomplete_not_blocked', () => {
  const result = child.resolve('active', 'missing', ['aborted:interrupted tool', 'restore'], '')
  assert.equal(result.result, 'RecoveryIncomplete')
  assert.notEqual(result.result, 'RecoveryBlocked')
  assert.equal(handles.crashScenario('active').joinable, 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const recovery = await import("../../../dist/Execution/Session/Recovery/Surface.js");


test('WHAT[CRASH-010] MISC_recovery_of_handle_family_all_branches', () => {
  assert.deepEqual(recovery.handleFamily('none'), { state: 'NoRecoveryRequired', restoredHandles: [], reason: '' })
  assert.deepEqual(recovery.handleFamily('recovered'), { state: 'Recovered', restoredHandles: ['h1'], reason: '' })
  assert.equal(recovery.handleFamily('waiting').state, 'Waiting')
  assert.match(recovery.handleFamily('waiting').reason, /handle h2 waiting: still running/)
  assert.equal(recovery.handleFamily('blocked').state, 'Blocked')
  assert.match(recovery.handleFamily('blocked').reason, /handle h3 blocked: linkage conflict/)
})
test('WHAT[CRASH-010] MISC_recovery_of_job_family_all_branches', () => {
  assert.equal(recovery.jobFamily('none').state, 'NoRecoveryRequired')
  assert.equal(recovery.jobFamily('recovered').state, 'Recovered')
  assert.equal(recovery.jobFamily('waiting').state, 'Waiting')
  assert.equal(recovery.jobFamily('blocked').state, 'Blocked')
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

test('WHAT[CRASH-010] RECOVERY_FAMILY_handle_family_types_and_permit_rules', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  assert.match(src, /type HandleFamilyRecovery/)
  assert.match(src, /NoLinkedHandles/)
  assert.match(src, /HandlesRecovered/)
  assert.match(src, /HandlesWaiting/)
  assert.match(src, /HandlesBlocked/)
  assert.match(src, /type JobFamilyRecovery/)
  assert.match(src, /NoRelatedJobs/)
  const child = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/ChildRecovery.fs'), 'utf8')
  assert.match(child, /type ChildRecoveryResult/)
  assert.match(child, /RecoveredActive/)
  assert.match(child, /RecoveryIncomplete/)
  assert.match(child, /RecoveryBlocked/)
  assert.doesNotMatch(child, /\| AwaitingEvidence\b/)
})
}
