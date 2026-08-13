// tests/unit/session/host-fork-restart.test.mjs — EXEC-009/GREEN-4 restart recovery
// coverage: HostForkRestart.restoreLinkedChildren drives the durable handle
// projection (HandleLinked facts + completion blobs in a REAL AgentJournal) and
// re-enlists children into a live ForkRuntime. Journal-only entry
// (restoreLinkedChildrenWithoutRuntime) exercises the same walk without a runtime.

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentCompletion,
  agentJournal,
  caseOf,
  handleCompletionCodec,
  handleController,
  handleId,
  handleProjection,
  listItems,
  reconcileSupervisor,
  roles,
  sessionId,
  toList,
} from '../support/domain.mjs'

const { restoreLinkedChildren, restoreLinkedChildrenWithoutRuntime } = await import(
  '../../../dist/Session/HostForkRestart.js'
)
const { ForkRuntime, ForkRuntime__List } = await import('../../../dist/Session/ForkRuntime.js')
const { HandleOwnership } = await import('../../../dist/Kernel/Fact.js')
const { HandleController_link } = await import('../../../dist/Session/HandleController.js')
const { NonEmpty_toList } = await import('../../../dist/Domain/SessionRecovery.js')

const PARENT = sessionId('ses_restart')
const CHILD = sessionId('ses_child_1')

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-restart-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    created.dispose()
  }
}

/** Production link entry with explicit ownership (the domain.mjs facade bind is stale). */
const linkDurable = async (j, agentId, child, targetAgent = 'fast-coder', ownership = HandleOwnership.DurableParentHandle) => {
  const result = await HandleController_link(
    j,
    PARENT,
    agentId,
    child,
    targetAgent,
    roles.of('Coder'),
    ownership,
  )
  assert.equal(result.tag, 0, result.tag === 1 ? result.fields[0] : '')
}

/** Durable Terminal completion with a blob (Current wire). */
const completeTerminal = async (j, agentId, child) => {
  const sealed = agentCompletion.completedRun({
    runId: `run-${agentId}`,
    agentId,
    agentName: 'fast-coder',
    workRecord: 'work-record',
  })
  const body = handleCompletionCodec.encodeOutcome(sealed.RunId, sealed.Outcome)
  const completed = await handleController.recordCompletion(j, PARENT, agentId, 'Terminal', body, child)
  assert.equal(completed.ok, true, completed.ok ? '' : completed.error)
}

const runtimeAndChildren = () => {
  const runtime = new ForkRuntime()
  const children = new Map()
  const createdDirs = []
  return { runtime, children, createdDirs }
}

const walk = (j, { runtime, children, createdDirs }, snapshot = undefined, directoryOf = () => 'dir-x') =>
  restoreLinkedChildren(runtime, snapshot, j, PARENT, children, (agentId, child, dir) => {
    createdDirs.push([agentId, child.fields?.[0] ?? child, dir])
  }, directoryOf)

const recoveredOf = (result) => {
  assert.equal(caseOf(result), 'HandlesRecovered')
  return listItems(NonEmpty_toList(result.fields[0]))
}

// ── journal-only walk (no live runtime) ──────────────────────────────────────

test('HFR_restart_empty_journal_yields_no_linked_handles', async () => {
  await withJournal(async (j) => {
    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    assert.equal(caseOf(result), 'NoLinkedHandles')
  })
})

test('HFR_restart_abandoned_handle_recovered_abandoned', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'ab1', CHILD, 'abandon-agent')
    const abandoned = await handleController.recordAbandon(j, PARENT, 'ab1', 'DeadlineExceeded')
    assert.equal(abandoned.ok, true, abandoned.ok ? '' : abandoned.error)

    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    const recovered = recoveredOf(result)
    assert.equal(recovered.length, 1)
    assert.equal(recovered[0].Kind, 'abandoned')
    assert.equal(recovered[0].Handle.fields[0], 'ab1')
    assert.equal(recovered[0].ChildSession.fields[0], CHILD.fields[0])
  })
})

test('HFR_restart_retired_handle_recovered_retired', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'rt1', CHILD, 'retire-agent')
    await completeTerminal(j, 'rt1', CHILD)
    // Retire via the production CAS consume path.
    const projection = agentJournal.handleProjection(j, PARENT)
    const record = handleProjection.tryFind(handleId.agent('rt1'), projection)
    assert.ok(record, 'handle must be joinable')
    const consumed = await handleController.consume(j, PARENT, handleId.agent('rt1'))
    assert.equal(consumed.ok, true, consumed.ok ? '' : consumed.error)

    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    const recovered = recoveredOf(result)
    assert.equal(recovered.length, 1)
    assert.equal(recovered[0].Kind, 'retired')
    assert.equal(recovered[0].Handle.fields[0], 'rt1')
  })
})

test('HFR_restart_host_owned_hidden_handle_is_filtered_out', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'hidden1', CHILD, 'fast-reviewer', HandleOwnership.HostOwnedHidden)

    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    assert.equal(caseOf(result), 'NoLinkedHandles', 'host-owned handles must not re-enter the parent runtime')
  })
})

// ── live-runtime walk: children re-enlisted, runtime restored ────────────────

test('HFR_restart_completed_terminal_re_enlists_child_into_runtime', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'term1', CHILD, 'deep-coder')
    await completeTerminal(j, 'term1', CHILD)

    const live = runtimeAndChildren()
    const result = await walk(j, live, undefined, () => 'dir-term1')
    const recovered = recoveredOf(result)

    assert.equal(recovered.length, 1)
    assert.equal(recovered[0].Kind, 'terminal')
    assert.equal(recovered[0].Handle.fields[0], 'term1')
    // Child map + created-dir callback + runtime restore all happened.
    assert.equal(live.children.get('term1').fields[0], CHILD.fields[0])
    assert.deepEqual(live.createdDirs, [['term1', CHILD.fields[0], 'dir-term1']])
    const [agents] = ForkRuntime__List(live.runtime)
    assert.equal(listItems(agents).length, 1)
    assert.equal(listItems(agents)[0].AgentId, 'term1')
    assert.equal(listItems(agents)[0].Agent, 'deep-coder')
  })
})

test('HFR_restart_active_handle_recovers_active', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'act1', CHILD, 'fast-coder')

    const live = runtimeAndChildren()
    const result = await walk(j, live)
    const recovered = recoveredOf(result)

    assert.equal(recovered.length, 1)
    assert.equal(recovered[0].Kind, 'active')
    assert.equal(live.children.get('act1').fields[0], CHILD.fields[0])
    assert.deepEqual(live.createdDirs, [['act1', CHILD.fields[0], 'dir-x']])
    const [agents] = ForkRuntime__List(live.runtime)
    assert.equal(listItems(agents)[0].AgentId, 'act1')
  })
})

test('HFR_restart_active_with_terminal_snapshot_recovered_terminal', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'snap1', CHILD, 'fast-coder')

    // Snapshot port shows a finished assistant turn followed by a user message:
    // ChildRecoveryWorkflow proves terminality from the transcript and commits
    // the completion durably (sole production caller of recordCompletion).
    const snapshot = reconcileSupervisor.createSnapshot([
      { ok: true, messages: reconcileSupervisor.terminalTranscript() },
    ])

    const live = runtimeAndChildren()
    const result = await walk(j, live, snapshot)
    const recovered = recoveredOf(result)

    assert.equal(recovered.length, 1)
    assert.equal(recovered[0].Kind, 'terminal')
    // The proof was committed: the handle is now joinable and the journal has a blob.
    const projection = agentJournal.handleProjection(j, PARENT)
    const record = handleProjection.tryFind(handleId.agent('snap1'), projection)
    assert.equal(handleProjection.joinable(projection).length, 1)
    assert.equal(record.Lifecycle.tag, 1, 'CompletedAwaitingJoin after snapshot terminal commit')
  })
})

test('HFR_restart_multiple_children_recovered_in_link_order', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'a-first', sessionId('ses_a'), 'alpha')
    await linkDurable(j, 'b-second', sessionId('ses_b'), 'beta')
    await linkDurable(j, 'c-third', sessionId('ses_c'), 'gamma')
    await completeTerminal(j, 'b-second', sessionId('ses_b'))
    await handleController.recordAbandon(j, PARENT, 'c-third', 'ParentCancelled')

    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    const recovered = recoveredOf(result)
    assert.deepEqual(
      recovered.map((r) => r.Kind),
      ['active', 'terminal', 'abandoned'],
    )
  })
})

// ── legacy abort blobs (clean-break) ─────────────────────────────────────────

const LEGACY_ABORT_BODY = (agentId) =>
  JSON.stringify({
    status: 'aborted',
    run_id: `run-${agentId}`,
    code: 'ABORTED',
    message: 'legacy abort',
    child_session_id: 'ses_child_1',
  })

const INVALID_BODY = JSON.stringify({
  schemaVersion: '2',
  status: 'completed',
  finality: '???',
  run_id: 'run-invalid',
})

const recordBlob = async (j, agentId, body) => {
  const recorded = await handleController.recordCompletion(j, PARENT, agentId, 'Terminal', body, CHILD)
  assert.equal(recorded.ok, true, recorded.ok ? '' : recorded.error)
}

test('HFR_restart_legacy_false_abort_waits_with_rejection_fact', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'legacy1', CHILD, 'fast-coder')
    await recordBlob(j, 'legacy1', LEGACY_ABORT_BODY('legacy1'))

    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    assert.equal(caseOf(result), 'HandlesWaiting')
    const waits = listItems(NonEmpty_toList(result.fields[0]))
    assert.equal(waits.length, 1)
    assert.equal(waits[0].Reason, 'legacy false abort rejected')
    assert.equal(waits[0].Handle.fields[0], 'legacy1')

    // The rejection was recorded durably: the handle is Active again, not joinable.
    const projection = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.joinable(projection).length, 0)
    assert.equal(handleProjection.activeHandles(projection).length, 1)
  })
})

test('HFR_restart_retired_legacy_false_abort_migrates_replacement_once', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'legacy2', CHILD, 'fast-coder')
    await recordBlob(j, 'legacy2', LEGACY_ABORT_BODY('legacy2'))
    const consumed = await handleController.consume(j, PARENT, handleId.agent('legacy2'))
    assert.equal(consumed.ok, true, consumed.ok ? '' : consumed.error)

    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    const recovered = recoveredOf(result)
    assert.equal(recovered.length, 1)
    assert.equal(recovered[0].Kind, 'retired')
    assert.equal(recovered[0].Handle.fields[0], 'legacy2')

    // A replacement handle was minted and linked (retired tombstone + replacement).
    const projection = agentJournal.handleProjection(j, PARENT)
    const records = handleProjection.linkedChildren(projection)
    assert.equal(records.length, 2, 'original retired + replacement handle')
    const replacement = records.find((r) => r.Handle.fields[0].fields[0] !== 'legacy2')
    assert.ok(replacement, 'replacement handle must be linked')
    assert.match(replacement.Handle.fields[0].fields[0], /^recovery:legacy2:/)
    assert.equal(replacement.TargetAgent, 'fast-coder')

    // Idempotent: a second restart does not mint a third handle.
    const again = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    assert.equal(caseOf(again), 'HandlesRecovered')
    const againProjection = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.linkedChildren(againProjection).length, 2)
  })
})

test('HFR_restart_invalid_completion_blob_waits', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'bad1', CHILD, 'fast-coder')
    await recordBlob(j, 'bad1', INVALID_BODY)

    const result = await restoreLinkedChildrenWithoutRuntime(undefined, j, PARENT)
    assert.equal(caseOf(result), 'HandlesWaiting')
    const waits = listItems(NonEmpty_toList(result.fields[0]))
    assert.equal(waits[0].Reason, 'invalid completion blob')
    // Invalid must not consume: the cell stays joinable for a later repair.
    const projection = agentJournal.handleProjection(j, PARENT)
    assert.equal(handleProjection.joinable(projection).length, 1)
  })
})

// ── ChildRecoveryWorkflow branches (recoverChild) ────────────────────────────

test('HFR_restart_active_with_unreadable_snapshot_waits_for_terminal_evidence', async () => {
  await withJournal(async (j) => {
    await linkDurable(j, 'unreadable1', CHILD, 'fast-coder')
    const snapshot = reconcileSupervisor.createSnapshot([{ ok: false, error: 'transcript vanished' }])

    const result = await restoreLinkedChildrenWithoutRuntime(snapshot, j, PARENT)
    assert.equal(caseOf(result), 'HandlesWaiting')
    const waits = listItems(NonEmpty_toList(result.fields[0]))
    assert.equal(waits.length, 1)
    assert.equal(waits[0].Reason, 'awaiting terminal evidence')
  })
})

test('HFR_restart_recovery_commit_failure_blocks', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-restart-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true)
  const j = created.journal
  try {
    await linkDurable(j, 'blocked1', CHILD, 'fast-coder')
    // A terminal snapshot proves the child finished; the commit then fails
    // against a journal whose writer is gone → hard block, not a silent wait.
    created.dispose()
    const snapshot = reconcileSupervisor.createSnapshot([
      { ok: true, messages: reconcileSupervisor.terminalTranscript() },
    ])

    const result = await restoreLinkedChildrenWithoutRuntime(snapshot, j, PARENT)
    assert.equal(caseOf(result), 'HandlesBlocked')
    const blocks = listItems(NonEmpty_toList(result.fields[0]))
    assert.equal(blocks.length, 1)
    assert.match(blocks[0].Reason, /Writer is poisoned or disposed/)
  } finally {
    try {
      created.dispose()
    } catch {}
  }
})
