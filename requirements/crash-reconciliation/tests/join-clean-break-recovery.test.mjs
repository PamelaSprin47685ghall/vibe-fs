/**
 * Split from tests/unit/execution/join-abort-clean-break.test.mjs (cutover Wave 2a);
 * owner: crash-reconciliation. P0 Clean Break 的恢复侧（CRASH-009/010）：aborted-only
 * 观察永不成为 terminal 证据；缺 terminal 证据 → RecoveryIncomplete（等待），
 * 真 terminal 到达前 join drain 为空且不 retire，之后只完成/retire 一次。
 * false-finality 代数（LegacyFalseAbort / 补偿事实 / wire 无 aborted）→ effect-accounting。
 */

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentCompletion,
  agentJournal,
  caseOf,
  childRecovery,
  forkRuntime,
  handleCompletionCodec,
  handleController,
  handleId,
  handleProjection,
  joinDrain,
  joinResultRenderer,
  maxJoinBatch,
  nonEmptyBatch,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'
import * as HandleControllerModule from '../../../dist/Execution/Delegation/Handle/Controller.js'
import { HandleOwnership } from '../../../dist/Composition/Durable/Fact.js'

const PARENT = sessionId('ses_parent_clean_break')
const CHILD = sessionId('ses_child_clean_break')
const AGENT_ID = 'h-false-abort'
const HANDLE = handleId.agent(AGENT_ID)
const TARGET = 'fast-coder'

/** Production HandleController.link takes Ownership (GREEN-7); the domain.mjs
 *  facade bind is stale, so tests call the dist entry directly. */
const durableLink = async (j, parentId, agentId, child, targetAgent, role) => {
  const result = await HandleControllerModule.HandleController_link(
    j,
    parentId,
    agentId,
    child,
    targetAgent,
    role,
    HandleOwnership.DurableParentHandle,
  )
  return result.tag === 0 ? { ok: true, value: result.fields[0] } : { ok: false, error: result.fields[0] }
}

// ── 3. Delayed recovery race (unit shape; full E2E later) ────────────────────

test('P0_CLEAN_BREAK_delayed_recovery_before_ready_no_aborted_join_then_true_terminal', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-join-abort-cb-race-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true)

  try {
    const j = created.journal
    const linked = await durableLink(j, PARENT, AGENT_ID, CHILD, TARGET, forkRuntime.role('Coder'))
    assert.equal(linked.ok, true, linked.ok ? '' : linked.error)

    // Host Aborted observation while recovery still incomplete → never Joinable.
    const resolution = childRecovery.resolveChild(
      childRecovery.durableActive(),
      childRecovery.snapshotMissing(),
      [childRecovery.abortedObserved('interrupted tool'), childRecovery.recoveryInFlight()],
    )
    assert.notEqual(caseOf(resolution), 'RecoveredTerminal')
    assert.equal(
      caseOf(resolution),
      'RecoveryIncomplete',
      `expected RecoveryIncomplete, got ${caseOf(resolution)}`,
    )

    // Before true terminal: drain empty, HandleRetired=0, no aborted item.
    const early = await joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(early.ok, true)
    assert.deepEqual(early.items, [])
    assert.equal(handleProjection.isRetired(HANDLE, agentJournal.handleProjection(j, PARENT)), false)

    // True terminal → completed once, retire once; never aborted.
    const sealed = agentCompletion.completedRun({
      runId: 'run-true-terminal',
      agentId: AGENT_ID,
      agentName: TARGET,
      workRecord: 'real work done',
    })
    const body = handleCompletionCodec.encodeOutcome(sealed.RunId, sealed.Outcome)
    const recorded = await handleController.recordCompletion(j, PARENT, AGENT_ID, 'Terminal', body, CHILD)
    assert.equal(recorded.ok, true, recorded.ok ? '' : recorded.error)

    const finalDrain = await joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(finalDrain.ok, true)
    assert.equal(finalDrain.items.length, 1)
    assert.equal(finalDrain.items[0].status, 'completed')
    assert.notEqual(finalDrain.items[0].status, 'aborted')
    assert.equal(handleProjection.isRetired(HANDLE, agentJournal.handleProjection(j, PARENT)), true)

    const batch = nonEmptyBatch.ofHeadTail(
      agentCompletion.completedRun({
        runId: finalDrain.items[0].runId,
        agentId: finalDrain.items[0].agentId,
        agentName: finalDrain.items[0].agentName,
        workRecord: finalDrain.items[0].workRecord,
      }),
    )
    const wire = joinResultRenderer.renderCompletedBatch(joinResultRenderer.stubRuntime(), batch)
    assert.ok(!wire.includes('status = "aborted"'))
  } finally {
    created.dispose()
  }
})
