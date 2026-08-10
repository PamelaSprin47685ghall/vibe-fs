import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentJournal,
  caseOf,
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
} from '../support/domain.mjs'
import { Ports, resolveAndCommit } from '../../../dist/Application/Reconciliation/ChildRecoveryWorkflow.js'

const PARENT = sessionId('ses_child_recovery_parent')
const CHILD = sessionId('ses_child_recovery_child')
const AGENT_ID = 'child-recovery-agent'
const HANDLE = handleId.agent(AGENT_ID)

const withJournal = async (body) => {
  const created = agentJournal.create({ directory: mkdtempSync(join(tmpdir(), 'wxs-child-recovery-')) })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    await body(created.journal)
  } finally {
    created.dispose()
  }
}

const linkedPorts = (journal, messages, observations, pulse = undefined, snapshotResult = okResult(toList(messages))) => {
  const linked = handleController.link(journal, PARENT, AGENT_ID, CHILD, 'fast-coder', roles.of('Coder'))
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
  const clock = clockPort.createVirtual()
  return new Ports(
    journal,
    PARENT,
    { GetMessages: () => Promise.resolve(snapshotResult) },
    AGENT_ID,
    HANDLE,
    CHILD,
    roles.of('Coder'),
    'fast-coder',
    toList(observations),
    pulse,
    clock.rawPort,
  )
}

test('VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses', async () => {
  await withJournal(async (journal) => {
    let pulses = 0
    const ports = linkedPorts(
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

    const result = await resolveAndCommit(ports)

    assert.equal(caseOf(result), 'Ok')
    assert.equal(caseOf(result.fields[0]), 'RecoveredTerminal')
    assert.equal(pulses, 1)
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'CompletedAwaitingJoin')
  })
})

test('VERIFY_008_child_recovery_workflow_returns_active_without_committing_when_child_is_live', async () => {
  await withJournal(async (journal) => {
    const ports = linkedPorts(
      journal,
      [
        { Id: 'user-1', Role: 'user', Finish: undefined, Parts: [{ tag: 0, fields: ['recover'] }] },
        { Id: 'assistant-1', Role: 'assistant', Finish: 'tool-calls', Parts: [{ tag: 0, fields: ['working'] }] },
      ],
      [childRecovery.sessionActive()],
    )

    const result = await resolveAndCommit(ports)

    assert.equal(caseOf(result), 'Ok')
    assert.equal(caseOf(result.fields[0]), 'RecoveredActive')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})

test('VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable', async () => {
  await withJournal(async (journal) => {
    const ports = linkedPorts(journal, [], [], undefined, errorResult('snapshot unavailable'))

    const result = await resolveAndCommit(ports)

    assert.equal(caseOf(result), 'Ok')
    assert.equal(caseOf(result.fields[0]), 'RecoveryIncomplete')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})

test('VERIFY_008_child_recovery_workflow_blocks_retired_handle', async () => {
  await withJournal(async (journal) => {
    const ports = linkedPorts(journal, [], [])
    // EXEC-004: retire is legal only after a completion cell exists
    // (CompletedAwaitingJoin). Retiring a bare Active handle is refused.
    const completed = handleController.recordCompletion(journal, PARENT, AGENT_ID, 'Terminal', 'done', CHILD)
    assert.equal(completed.ok, true, completed.ok ? '' : completed.error)
    const retired = handleController.retire(journal, PARENT, AGENT_ID)
    assert.equal(retired.ok, true, retired.ok ? '' : retired.error)

    const result = await resolveAndCommit(ports)

    assert.equal(caseOf(result), 'Ok')
    assert.equal(caseOf(result.fields[0]), 'RecoveryBlocked')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Retired')
  })
})

test('VERIFY_008_child_recovery_workflow_incomplete_when_terminal_body_is_blank', async () => {
  await withJournal(async (journal) => {
    const ports = linkedPorts(
      journal,
      [
        { Id: 'user-1', Role: 'user', Finish: undefined, Parts: [{ tag: 0, fields: ['recover'] }] },
        { Id: 'assistant-1', Role: 'assistant', Finish: 'stop', Parts: [{ tag: 0, fields: ['   '] }] },
      ],
      [],
    )

    const result = await resolveAndCommit(ports)

    // A whitespace-only terminal body is not a legal terminal: isTerminalCompleted
    // treats it as an active child, and no session-active observation is present,
    // so resolution is RecoveryIncomplete (no permit issued, handle stays Active).
    assert.equal(caseOf(result), 'Ok')
    assert.equal(caseOf(result.fields[0]), 'RecoveryIncomplete')
    assert.equal(handleProjection.read(agentJournal.handleProjection(journal, PARENT).Handles.get(HANDLE)).lifecycle, 'Active')
  })
})
