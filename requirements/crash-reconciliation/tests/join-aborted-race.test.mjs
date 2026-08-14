/**
 * P0-RECOVERY-JOIN-001 Case A–E: Aborted race + resolveChild matrix.
 *
 * Case A uses handleProjection (durable lifecycle) to simulate HostForkRunLifecycle
 * observation-only Aborted (no recordCompletion) then proven terminal → complete → retire.
 * Cases B–E use pure resolveChild / tryFromProvenTerminal.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  childRecovery,
  handleId,
  handleProjection,
  roles,
  sessionId,
} from '../support/domain.mjs'
import * as LinkageProjectionModule from '../../../dist/Journal/LinkageProjection.js'
import { HandleOwnership } from '../../../dist/Kernel/Fact.js'

const CHILD = sessionId('ses_race_c')
const HANDLE = handleId.agent('h-race')
const AGENT = 'fast-coder'

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

const linkActive = () => {
  const applied = link(HANDLE, CHILD, AGENT, roles.of('Coder'), handleProjection.empty)
  assert.equal(applied.ok, true, applied.ok ? '' : `link refused: ${applied.error}`)
  return applied.value
}

const stateOf = (projection) => handleProjection.read(handleProjection.tryFind(HANDLE, projection))

const views = (projection) => ({
  joinable: handleProjection.joinable(projection).map((r) => handleProjection.read(r).handle),
  active: handleProjection.activeHandles(projection).map((r) => handleProjection.read(r).handle),
  listable: handleProjection.listable(projection).map((r) => handleProjection.read(r).handle),
})

// ── Case A: Aborted path does not record; proof then complete + consume ──────

test('P0_RECOVERY_JOIN_001_case_A_aborted_path_leaves_handle_active', () => {
  // HostForkRunLifecycle: Aborted → observation only (no recordCompletion).
  // Durable projection is unchanged after N aborts.
  let projection = linkActive()
  for (let i = 0; i < 5; i++) {
    // Pure observation: resolveChild must not mint Joinable; projection untouched.
    const resolution = childRecovery.resolveChild(
      childRecovery.durableActive(),
      childRecovery.snapshotMissing(),
      [childRecovery.abortedObserved(`abort-${i}`)],
    )
    assert.equal(caseOf(resolution), 'RecoveryIncomplete')
  }
  assert.equal(stateOf(projection).lifecycle, 'Active')
  assert.deepEqual(views(projection), {
    joinable: [],
    active: ['agent:h-race'],
    listable: ['agent:h-race'],
  })
  assert.equal(handleProjection.isRetired(HANDLE, projection), false)
  assert.equal(handleProjection.isAbandoned(HANDLE, projection), false)
})

test('P0_RECOVERY_JOIN_001_case_A_proven_terminal_completes_once_then_retire_once', () => {
  let projection = linkActive()
  // Step 2–3: aborts observed, still Active.
  assert.equal(stateOf(projection).lifecycle, 'Active')
  assert.equal(views(projection).joinable.length, 0)

  // Step 4: proven terminal → JoinableCompletion → HandleProjection.complete (recordCompletion path).
  const evidence = childRecovery.evidenceCompleted(AGENT, HANDLE, CHILD, '{"status":"ok"}')
  const proof = childRecovery.tryFromProvenTerminal(evidence)
  assert.equal(proof.ok, true, proof.ok ? '' : proof.error)

  const completed = handleProjection.complete(
    HANDLE,
    handleProjection.completionOf('Terminal'),
    projection,
  )
  assert.equal(completed.ok, true, completed.ok ? '' : completed.error)
  projection = completed.value
  assert.equal(stateOf(projection).lifecycle, 'CompletedAwaitingJoin')
  assert.deepEqual(views(projection).joinable, ['agent:h-race'])

  // Second complete refused (single-assignment).
  assert.deepEqual(
    handleProjection.complete(HANDLE, handleProjection.completionOf('Terminal'), projection),
    { ok: false, error: 'AlreadyCompleted' },
  )

  // Step 5: consume → Retired exactly once.
  const retired = handleProjection.retire(HANDLE, projection)
  assert.equal(retired.ok, true, retired.ok ? '' : retired.error)
  projection = retired.value
  assert.equal(stateOf(projection).lifecycle, 'Retired')
  assert.deepEqual(views(projection), { joinable: [], active: [], listable: [] })
  assert.deepEqual(handleProjection.retire(HANDLE, projection), {
    ok: false,
    error: 'HandleIsRetired',
  })
})

test('P0_RECOVERY_JOIN_001_case_A_tryFromProvenTerminal_then_joinable_once', () => {
  const evidence = childRecovery.evidenceCompleted(AGENT, HANDLE, CHILD, 'payload')
  const proof = childRecovery.tryFromProvenTerminal(evidence)
  assert.equal(proof.ok, true)
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotTerminal(evidence),
    [childRecovery.abortedObserved('prior')],
  )
  assert.equal(caseOf(resolution), 'RecoveredTerminal')
})

// ── Case B: Aborted + terminal snapshot → RecoveredTerminal (not abort finality)

test('P0_RECOVERY_JOIN_001_case_B_aborted_then_terminal_snapshot_is_joinable', () => {
  const evidence = childRecovery.evidenceCompleted(AGENT, HANDLE, CHILD, 'terminal-body')
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotTerminal(evidence),
    [childRecovery.abortedObserved('host abort before snapshot')],
  )
  assert.equal(caseOf(resolution), 'RecoveredTerminal')
  assert.notEqual(caseOf(resolution), 'RecoveredAbandoned')
})

// ── Case C: ParentCancelled → RecoveredAbandoned ─────────────────────────────

test('P0_RECOVERY_JOIN_001_case_C_parent_cancelled_is_abandon', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotMissing(),
    [childRecovery.parentCancelled()],
  )
  assert.equal(caseOf(resolution), 'RecoveredAbandoned')
})

test('P0_RECOVERY_JOIN_001_case_C_parent_cancelled_after_aborts_still_abandon', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotActive(),
    [
      childRecovery.abortedObserved('a1'),
      childRecovery.abortedObserved('a2'),
      childRecovery.parentCancelled(),
    ],
  )
  assert.equal(caseOf(resolution), 'RecoveredAbandoned')
})

// ── Case D: Unreadable snapshot → RecoveryIncomplete (wait, not hard block) ──

test('P0_RECOVERY_JOIN_001_case_D_unreadable_snapshot_is_incomplete_not_blocked', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotUnreadable('snapshot decode failed'),
    [childRecovery.abortedObserved('noise')],
  )
  // True GetMessages/decode failure: wait (no permit), not RecoveryBlocked hard error.
  assert.equal(caseOf(resolution), 'RecoveryIncomplete')
  assert.notEqual(caseOf(resolution), 'RecoveryBlocked')
})

// ── Case E: Aborted × N → projection / resolve unchanged ─────────────────────

test('P0_RECOVERY_JOIN_001_case_E_aborted_times_n_projection_unchanged', () => {
  const projection = linkActive()
  const before = stateOf(projection)
  for (let n = 0; n < 10; n++) {
    const resolution = childRecovery.resolveChild(
      childRecovery.durableActive(),
      childRecovery.snapshotMissing(),
      Array.from({ length: n + 1 }, (_, i) => childRecovery.abortedObserved(`e-${i}`)),
    )
    assert.equal(caseOf(resolution), 'RecoveryIncomplete')
  }
  assert.deepEqual(stateOf(projection), before)
  assert.equal(stateOf(projection).lifecycle, 'Active')
  assert.equal(views(projection).joinable.length, 0)
})
