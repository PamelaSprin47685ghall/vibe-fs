import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentJournal,
  childRecovery,
  clockPort,
  errorResult,
  handleController,
  handleId,
  handleProjection,
  okResult,
  roles,
  sessionId,
  toList,
} from '../../verification-system/tests/support/domain.mjs'
import { childRecoveryWorkflow } from './support/child-recovery-workflow.mjs'

const PARENT = sessionId('ses_child_recovery_parent')
const CHILD = sessionId('ses_child_recovery_child')
const AGENT_ID = 'child-recovery-agent'
const HANDLE = handleId.agent(AGENT_ID)

const withJournal = async (body) => {
  const created = await agentJournal.create({ directory: mkdtempSync(join(tmpdir(), 'wxs-child-recovery-')) })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    await body(created.journal)
  } finally {
    created.dispose()
  }
}

const linkedPorts = async (journal, messages, observations, pulse = undefined, snapshotResult = okResult(toList(messages))) => {
  const linked = await handleController.link(journal, PARENT, AGENT_ID, CHILD, 'fast-coder', roles.of('Coder'))
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
  const clock = clockPort.createVirtual()
  return childRecoveryWorkflow.ports({
    journal,
    parent: PARENT,
    snapshotResult,
    agentId: AGENT_ID,
    handle: HANDLE,
    child: CHILD,
    role: roles.of('Coder'),
    targetAgent: 'fast-coder',
    observations,
    pulse,
    clock: clock.rawPort,
  })
}

test('WHAT[CRASH-002] VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses', async () => {
  await withJournal(async (journal) => {
    let pulses = 0
    const ports = await linkedPorts(
      journal,
      [
        { Id: 'user-1', Role: 'user', Finish: undefined, Parts: [{ tag: 0, fields: ['recover'] }] },
        { Id: 'assistant-1', Role: 'assistant', Finish: 'stop', Parts: [{ tag: 0, fields: ['done'] }] },
      ],
      [],
      () => {
        pulses += 1
      },
    )

    const result = await childRecoveryWorkflow.resolve(ports)

    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveredTerminal')
    assert.equal(pulses, 1)
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'CompletedAwaitingJoin')
  })
})

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_returns_active_without_committing_when_child_is_live', async () => {
  await withJournal(async (journal) => {
    const ports = await linkedPorts(
      journal,
      [
        { Id: 'user-1', Role: 'user', Finish: undefined, Parts: [{ tag: 0, fields: ['recover'] }] },
        { Id: 'assistant-1', Role: 'assistant', Finish: 'tool-calls', Parts: [{ tag: 0, fields: ['working'] }] },
      ],
      [childRecovery.sessionActive()],
    )

    const result = await childRecoveryWorkflow.resolve(ports)

    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveredActive')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})

test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable', async () => {
  await withJournal(async (journal) => {
    const ports = await linkedPorts(journal, [], [], undefined, errorResult('snapshot unavailable'))

    const result = await childRecoveryWorkflow.resolve(ports)

    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveryIncomplete')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})

test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_blocks_retired_handle', async () => {
  await withJournal(async (journal) => {
    const ports = await linkedPorts(journal, [], [])
    // EXEC-004: retire is legal only after a completion cell exists
    // (CompletedAwaitingJoin). Retiring a bare Active handle is refused.
    const completed = await handleController.recordCompletion(journal, PARENT, AGENT_ID, 'Terminal', 'done', CHILD)
    assert.equal(completed.ok, true, completed.ok ? '' : completed.error)
    const retired = await handleController.retire(journal, PARENT, AGENT_ID)
    assert.equal(retired.ok, true, retired.ok ? '' : retired.error)

    const result = await childRecoveryWorkflow.resolve(ports)

    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveryBlocked')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Retired')
  })
})

test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_incomplete_when_terminal_body_is_blank', async () => {
  await withJournal(async (journal) => {
    const ports = await linkedPorts(
      journal,
      [
        { Id: 'user-1', Role: 'user', Finish: undefined, Parts: [{ tag: 0, fields: ['recover'] }] },
        { Id: 'assistant-1', Role: 'assistant', Finish: 'stop', Parts: [{ tag: 0, fields: ['   '] }] },
      ],
      [],
    )

    const result = await childRecoveryWorkflow.resolve(ports)

    // A whitespace-only terminal body is not a legal terminal: isTerminalCompleted
    // treats it as an active child, and no session-active observation is present,
    // so resolution is RecoveryIncomplete (no permit issued, handle stays Active).
    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveryIncomplete')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})

test('WHAT[CRASH-012] VERIFY_008_child_recovery_workflow_commits_terminal_then_pulses_once_single_owner', async () => {
  // CRASH-012 completion 单一 owner：resolveAndCommit 是生产唯一调用方，提交
  // terminal 证明后只 Pulse agent handle（唤醒），Journal 是事实源。
  await withJournal(async (journal) => {
    let pulses = 0
    const ports = await linkedPorts(
      journal,
      [
        { Id: 'user-1', Role: 'user', Finish: undefined, Parts: [{ tag: 0, fields: ['recover'] }] },
        { Id: 'assistant-1', Role: 'assistant', Finish: 'stop', Parts: [{ tag: 0, fields: ['done'] }] },
      ],
      [],
      () => {
        pulses += 1
      },
    )

    const result = await childRecoveryWorkflow.resolve(ports)

    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveredTerminal')
    assert.equal(pulses, 1, 'recordCompletion 后仅 Pulse 唤醒一次，不重复投递')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'CompletedAwaitingJoin')
  })
})

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_unreadable_snapshot_is_incomplete_not_blocked', async () => {
  await withJournal(async (journal) => {
    const ports = await linkedPorts(journal, [], [], undefined, errorResult('snapshot unavailable'))

    const result = await childRecoveryWorkflow.resolve(ports)

    // CRASH-010: Waiting ≠ Blocked —— 真读错误是 Incomplete（等待，不发 permit），
    // 不是硬 RecoveryBlocked。
    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveryIncomplete')
    assert.notEqual(result.resolution, 'RecoveryBlocked')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_retired_handle_is_blocked_branch', async () => {
  await withJournal(async (journal) => {
    const ports = await linkedPorts(journal, [], [])
    const completed = await handleController.recordCompletion(journal, PARENT, AGENT_ID, 'Terminal', 'done', CHILD)
    assert.equal(completed.ok, true, completed.ok ? '' : completed.error)
    const retired = await handleController.retire(journal, PARENT, AGENT_ID)
    assert.equal(retired.ok, true, retired.ok ? '' : retired.error)

    const result = await childRecoveryWorkflow.resolve(ports)

    // CRASH-010: 冲突 / retired 证据 → RecoveryBlocked（硬失败分支，与 Incomplete 互斥）。
    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveryBlocked')
    assert.notEqual(result.resolution, 'RecoveryIncomplete')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Retired')
  })
})

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_blank_terminal_body_is_incomplete_branch', async () => {
  await withJournal(async (journal) => {
    const ports = await linkedPorts(
      journal,
      [
        { Id: 'user-1', Role: 'user', Finish: undefined, Parts: [{ tag: 0, fields: ['recover'] }] },
        { Id: 'assistant-1', Role: 'assistant', Finish: 'stop', Parts: [{ tag: 0, fields: ['   '] }] },
      ],
      [],
    )

    const result = await childRecoveryWorkflow.resolve(ports)

    // CRASH-010: 缺 terminal 证据 → RecoveryIncomplete（必须等），不是 Blocked。
    assert.equal(result.ok, true)
    assert.equal(result.resolution, 'RecoveryIncomplete')
    assert.notEqual(result.resolution, 'RecoveryBlocked')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})
