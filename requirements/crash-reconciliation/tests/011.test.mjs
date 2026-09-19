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


test('WHAT[crash-reconciliation-011] VERIFY_008_bare_runtime_join_refusal_and_permit_validation', async () => {
  // Gap test 3: Join ops require valid FamilyRecoveryPermit.
  // Bare join on JoinSurface without permit or work fails closed as NothingToJoin.
  const probe = joinSurface.createJoinProbe()
  const interrupt = joinSurface.createJoinInterrupt()
  const bareResult = await joinSurface.joinAvailable(probe, 4, interrupt)
  assert.equal(bareResult.kind, 'Error')
  assert.equal(bareResult.error, 'NothingToJoin')

  // Validate permit root mismatch refuses
  const mismatch = joinHost.validatePermit('ses_other', 0, 'ses_parent', 0, [], [])
  assert.equal(mismatch.ok, false)
  assert.match(mismatch.error, /family recovery permit root mismatch/)

  // Valid permit passes permit validation
  const valid = joinHost.validatePermit('ses_parent', 0, 'ses_parent', 0, [], [])
  assert.equal(valid.ok, true)
  assert.equal(valid.error, 'NothingToJoin')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const join = await import("../../../dist/Execution/Delegation/Fork/Host/JoinSurface.js");

const valid = (over = {}) => ({
  permitRoot: 'ses_hfrt',
  permitSequence: 0,
  currentRoot: 'ses_hfrt',
  currentSequence: 0,
  permitMembers: [],
  currentMembers: [],
  ...over,
})

test('WHAT[crash-reconciliation-011] HFRT_join_with_permit_root_mismatch_is_not_found', () => {
  const result = join.validatePermit('ses_other', 0, 'ses_hfrt', 0, [], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /root mismatch: permit=ses_other runtime=ses_hfrt/)
})
test('WHAT[crash-reconciliation-011] HFRT_join_with_permit_stale_journal_sequence_is_not_found', () => {
  const result = join.validatePermit('ses_hfrt', 1000, 'ses_hfrt', 0, [], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /journalSequence stale: permit=1000/)
})
test('WHAT[crash-reconciliation-011] EXEC_023_permit_whose_recovered_member_is_gone_is_not_found', () => {
  const result = join.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, ['W:ses_vanished'], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /closure lost members: missing=W:ses_vanished/)
})
test('WHAT[crash-reconciliation-011] EXEC_023_permit_survives_family_growth_after_recovery_closed', () => {
  const result = join.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, ['W:ses_hfrt'], ['W:ses_hfrt', 'C:ses_child>ses_grandchild'])
  assert.equal(result.ok, true)
  assert.equal(result.error, 'NothingToJoin')
})
test('WHAT[crash-reconciliation-011] HFRT_join_with_valid_permit_passes_validation', () => {
  assert.deepEqual(join.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, [], []), { ok: true, error: 'NothingToJoin' })
})
test('WHAT[crash-reconciliation-011] HFRT_await_agent_with_permit_validation_error_maps_to_not_found', () => {
  const result = join.validatePermit('ses_other', 0, 'ses_hfrt', 0, [], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /NotFound|root mismatch/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const recovery = await import("../../../dist/Execution/Session/Recovery/Surface.js");

const root = 'ses_root'
const work = (session) => ({ kind: 'work', session })
const child = (parent, session, handle) => ({ kind: 'child', parent, child: session, handle })
const companion = (main, session) => ({ kind: 'companion', main, companion: session })
const blogger = (main, session) => ({ kind: 'blogger', main, blogger: session })
const managerJob = (job, manager) => ({ kind: 'managerJob', job, manager })

test('WHAT[crash-reconciliation-011] CRASH_CLOSURE_permit_refuses_loss_and_admits_growth', () => {
  const permit = ['W:w1', 'A:p>c:h1']
  assert.deepEqual(recovery.missingMembers(permit, ['W:w1']), ['A:p>c:h1'])
  assert.deepEqual(recovery.missingMembers(permit, ['W:w1', 'A:p>c:h1', 'C:m>c2']), [])
})
}
