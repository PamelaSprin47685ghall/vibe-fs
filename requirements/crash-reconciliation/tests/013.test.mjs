import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const recovery = await import("../../../dist/Execution/Session/Recovery/Surface.js");


test('WHAT[CRASH-013] RECOVERY_COMBINE_export_exists', () => {
  assert.equal(typeof recovery.combine, 'function')
})
test('WHAT[CRASH-013] RECOVERY_COMBINE_blocked_dominates', () => {
  assert.equal(recovery.combine(['NoRecoveryRequired', 'Waiting', 'Blocked', 'Recovered']), 'Blocked')
})
test('WHAT[CRASH-013] RECOVERY_COMBINE_waiting_dominates_ready', () => {
  assert.equal(recovery.combine(['NoRecoveryRequired', 'Recovered', 'Waiting']), 'Waiting')
})
test('WHAT[CRASH-013] RECOVERY_COMBINE_recovered_over_ready', () => {
  assert.equal(recovery.combine(['NoRecoveryRequired', 'Recovered']), 'Recovered')
})
test('WHAT[CRASH-013] RECOVERY_COMBINE_empty_is_no_recovery_required', () => {
  assert.equal(recovery.combine([]), 'NoRecoveryRequired')
})
test('WHAT[CRASH-013] RECOVERY_COMBINE_order_independent_for_tier', () => {
  assert.equal(recovery.combine(['Blocked', 'Waiting', 'Recovered']), 'Blocked')
  assert.equal(recovery.combine(['Recovered', 'Blocked', 'Waiting']), 'Blocked')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const recovery = await import("../../../dist/Execution/Session/Recovery/Surface.js");


test('WHAT[CRASH-013] MISC_recovery_authorize_aggregates_blocks_waits_ready', () => {
  assert.equal(recovery.authorize('root1', 9, [{ session: 'child1', state: 'Blocked' }]).state, 'FamilyBlocked')
  assert.equal(
    recovery.authorize('root1', 9, [
      { session: 'child1', state: 'Waiting' },
      { session: 'other', state: 'NoRecoveryRequired' },
    ]).state,
    'FamilyWaiting',
  )
  const ready = recovery.authorize('root1', 9, [{ session: 'child1', state: 'Recovered' }])
  assert.equal(ready.state, 'FamilyReady')
  assert.equal(ready.root, 'root1')
  assert.equal(ready.sequence, 9)
  assert.deepEqual(ready.members, [])
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

test('WHAT[CRASH-013] RECOVERY_FAMILY_combine_and_coordinator_ownership_moved', () => {
  const domain = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  const coordinator = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Coordinator.fs'), 'utf8')
  // W5 cutover: ownership is declared on the owning shard, not the wrapper
  // aggregate. Recovery stays under dispatch-protocol's session-recovery
  // shard.
  const shard = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Wanxiangshu.Owner.dispatch.session-recovery-coordinator.fsproj'),
    'utf8',
  )
  assert.match(domain, /let combine \(outcomes: SessionRecovery list\)/)
  assert.match(coordinator, /module FamilyRecoveryCoordinator/)
  assert.match(coordinator, /let runOnce/)
  assert.doesNotMatch(coordinator, /recoverFamilyDirect|SessionRecoveryPorts|authorizeFamilyResume/)
  assert.match(shard, /Execution\/Session\/Recovery\/Coordinator\.fs/)
})
}
