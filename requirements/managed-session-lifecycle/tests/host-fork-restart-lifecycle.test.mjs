// Split from tests/unit/session/host-fork-restart.test.mjs (cutover Wave 2a); owner: managed-session-lifecycle.
//
// MANAGED-SESSION-013 handle 投影恢复：durable handle projection 把 handle 四态
// （abandoned/retired/host-owned-hidden/active）与恢复 commit 失败
// （HandlesBlocked）如实验出。恢复工作流（empty/terminal re-enlist/snapshot 证明/
// link 序/legacy false abort/invalid blob/unreadable snapshot）已随 SPLIT@cutover
// 迁 requirements/crash-reconciliation/tests/host-fork-restart.test.mjs。

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
} from '../../verification-system/tests/support/domain.mjs'

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
