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


test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_blocks_retired_handle', () => {
  assert.equal(child.resolve('retired', 'missing', [], '').result, 'RecoveryBlocked')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_incomplete_when_terminal_body_is_blank', () => {
  assert.equal(child.resolve('active', 'terminal', [], '').result, 'RecoveryBlocked')
})
test('WHAT[CRASH-005] VERIFY_008_missing_ports_or_waiting_never_synthesizes_family_ready', () => {
  // Gap test 2: When child recovery is waiting or blocked, authorizeFamilyResume outcomes
  // are FamilyWaiting / FamilyBlocked, never FamilyReady or NoRecoveryRequired.
  const waiting = recovery.authorize('parent', 1, [{ session: 'child', state: 'Waiting' }])
  assert.equal(waiting.state, 'FamilyWaiting')
  assert.notEqual(waiting.state, 'FamilyReady')

  const blocked = recovery.authorize('parent', 1, [{ session: 'child', state: 'Blocked' }])
  assert.equal(blocked.state, 'FamilyBlocked')
  assert.notEqual(blocked.state, 'FamilyReady')

  // Missing handles / jobs map to Waiting/Blocked when dependency unresolved
  const handleWait = recovery.handleFamily('waiting')
  assert.equal(handleWait.state, 'Waiting')
  assert.notEqual(handleWait.state, 'NoRecoveryRequired')

  const jobWait = recovery.jobFamily('waiting')
  assert.equal(jobWait.state, 'Waiting')
  assert.notEqual(jobWait.state, 'NoRecoveryRequired')
})
test('WHAT[CRASH-005] VERIFY_008_executor_tool_empty_or_whitespace_session_id_fails_closed', async () => {
  // Gap test 4: Empty/whitespace SessionId fails closed via production executor tool surface
  const fakeSchema = {
    string: () => ({ kind: 'string', describe: () => ({}), optional: () => ({}) }),
    number: () => ({ kind: 'number', describe: () => ({}), optional: () => ({}) }),
    boolean: () => ({ kind: 'boolean', describe: () => ({}), optional: () => ({}) }),
  }
  const toolModule = { tool: { schema: fakeSchema } }
  const SPOOL_BUDGET = { command: "printf 'test'", output_budget_bytes: 4 }

  // IsNullOrWhiteSpace is reached through `args.sessionID` — the tool's own
  // argument decoder — not the context field. Empty/whitespace/undefined are
  // all refused before any physical send.
  for (const emptySession of ['', '   ', '\t', '\n', null, undefined]) {
    const res = await execTool.run(toolModule, {}, { ...SPOOL_BUDGET, sessionID: emptySession }, {}, 'ready')
    assert.match(res, /无法在此执行上下文中运行|cannot run|session.?id|empty|missing|not run/i, `whitespace sessionID ${JSON.stringify(emptySession)} must fail closed, got: ${JSON.stringify(res)}`)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const child = await import("../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const ROOT = new URL('../../../', import.meta.url).pathname

test('WHAT[CRASH-005] HFR_restart_active_with_unreadable_snapshot_waits_for_terminal_evidence', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const active = () => handles.crashScenario('active')

test('WHAT[CRASH-005] P0_RECOVERY_JOIN_001_crash_before_handle_completed_append_has_no_completion', () => {
  const state = active()
  assert.equal(state.lifecycle, 'Active')
  assert.equal(state.completion, null)
  assert.equal(state.joinable, 0)
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

test('WHAT[CRASH-005] RECOVERY_FAMILY_authorize_blocks_on_child_block', () => {
  assert.equal(recovery.authorize('parent', 1, [{ session: 'child', state: 'Blocked' }]).state, 'FamilyBlocked')
})
test('WHAT[CRASH-005] RECOVERY_FAMILY_authorize_waiting_is_family_waiting_not_blocked', () => {
  const result = recovery.authorize('parent', 1, [{ session: 'child', state: 'Waiting' }])
  assert.equal(result.state, 'FamilyWaiting')
  assert.notEqual(result.state, 'FamilyBlocked')
  assert.notEqual(result.state, 'FamilyReady')
})
test('WHAT[CRASH-005] RECOVERY_FAMILY_handle_family_waiting_maps_to_waiting_not_blocked', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  const waitingArm = src.match(/HandleFamilyRecovery\.HandlesWaiting[\s\S]*?(?=HandleFamilyRecovery\.HandlesBlocked)/)?.[0]
  assert.ok(waitingArm, 'HandleFamilyRecovery.HandlesWaiting arm body not found')
  assert.match(waitingArm, /waitingRecovery/)
  assert.doesNotMatch(waitingArm, /SessionRecovery\.Blocked/)
  const waitingMapping = src.match(/let private waitingRecovery[\s\S]*?SessionRecovery\.Waiting/)?.[0]
  assert.ok(waitingMapping, 'waitingRecovery must map to SessionRecovery.Waiting')
  assert.match(src, /\| FamilyWaiting of NonEmpty<RecoveryBlock>/)
  assert.match(src, /\| Waiting of NonEmpty<RecoveryBlock>/)
})
}
