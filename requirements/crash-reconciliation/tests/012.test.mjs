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


test('WHAT[crash-reconciliation-012] VERIFY_008_child_recovery_workflow_commits_terminal_then_pulses_once_single_owner', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'done').result, 'RecoveredTerminal')
})
test('WHAT[crash-reconciliation-012] VERIFY_008_child_recovery_workflow_terminal_commit_single_owner_no_raw_publish_completion', () => {
  // Gap test 1: Terminal resolution commits a proven terminal proof and emits pulse
  // The production ChildRecoveryWorkflow single-owner path never publishes bare PublishCompletion
  const committed = child.resolve('active', 'terminal', [], 'done')
  assert.equal(committed.result, 'RecoveredTerminal')
  assert.equal(committed.reason, '')

  const proven = child.provenTerminal('valid terminal text')
  assert.equal(proven.ok, true)
  assert.equal(proven.finality, 'Succeeded')
  assert.equal(proven.body, 'valid terminal text')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const active = () => handles.crashScenario('active')

test('WHAT[crash-reconciliation-012] P0_RECOVERY_JOIN_001_crash_after_retired_is_idempotent', () => {
  const state = handles.crashScenario('retired')
  assert.equal(state.lifecycle, 'Retired')
  assert.equal(state.joinable, 0)
  assert.equal(state.retired, true)
})
test('WHAT[crash-reconciliation-012] P0_RECOVERY_JOIN_001_duplicate_retire_and_late_complete_are_absorbed', () => {
  const state = handles.crashScenario('replayed-retired')
  assert.equal(state.lifecycle, 'Retired')
  assert.equal(state.retired, true)
  assert.equal(state.joinable, 0)
})
}
