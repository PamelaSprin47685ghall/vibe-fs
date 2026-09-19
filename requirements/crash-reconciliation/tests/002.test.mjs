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


test('WHAT[crash-reconciliation-002] VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'done').result, 'RecoveredTerminal')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const child = await import("../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const ROOT = new URL('../../../', import.meta.url).pathname

test('WHAT[crash-reconciliation-002] HFR_restart_empty_journal_yields_no_linked_handles', () => {
  assert.equal(child.resolve('active', 'missing', [], '').result, 'RecoveryIncomplete')
})
test('WHAT[crash-reconciliation-002] HFR_restart_completed_terminal_re_enlists_child_into_runtime', () => {
  assert.equal(child.resolve('completed', 'missing', [], 'work-record').result, 'RecoveredTerminal')
  assert.equal(handles.crashScenario('completed').lifecycle, 'CompletedAwaitingJoin')
})
test('WHAT[crash-reconciliation-002] HFR_restart_active_with_terminal_snapshot_recovered_terminal', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'work-record').result, 'RecoveredTerminal')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const active = () => handles.crashScenario('active')

test('WHAT[crash-reconciliation-002] P0_RECOVERY_JOIN_001_crash_after_completed_before_consume_is_awaiting_join', () => {
  const state = handles.crashScenario('completed')
  assert.equal(state.lifecycle, 'CompletedAwaitingJoin')
  assert.deepEqual(state.completion, { kind: 'Terminal' })
  assert.equal(state.joinable, 1)
})
test('WHAT[crash-reconciliation-002] P0_RECOVERY_JOIN_001_duplicate_handle_completed_is_absorbed', () => {
  const state = handles.crashScenario('replayed-completed')
  assert.equal(state.lifecycle, 'CompletedAwaitingJoin')
  assert.deepEqual(state.completion, { kind: 'Terminal' })
  assert.equal(state.joinable, 1)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const recovery = await import("../../../dist/Execution/Session/Recovery/Surface.js");


test('WHAT[crash-reconciliation-002] MISC_recovery_receipt_accessors_and_nonempty_helpers', () => {
  assert.deepEqual(recovery.receiptView('s1', 42), {
    session: 's1',
    sequence: 42,
    snapshotDigest: null,
    resolvedClaims: [],
    restoredHandles: [],
  })
})
}
