import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as todoJournal from '../../../dist/Persistence/Journal/ObligationJournalSurface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const sha256 = (value) => `sha:${value}`
const managerSession = 'ses_magic_todo_manager'
const life = 'life_magic_todo'
const call = 'call_magic_todo'
const write = todo.todoWriteId(sha256, life, call)
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const prepared = fact('TodoWritePrepared', {
  ManagerSessionId: managerSession,
  ManagerLifeId: life,
  TodoWriteId: write,
  ToolCallId: call,
  ToolPartOrdinal: 1,
  BaseTodoRef: 'blobs/base',
  BaseTodoDigest: 'digest:base',
  ProposedTodoRef: 'blobs/proposed',
  ProposedTodoDigest: 'digest:proposed',
  PlanCompleteDeclared: true,
  ProviderInputDigest: 'digest:provider-input',
  ReviewFrontier: { Sequence: 7 },
  SemanticVersion: 'magic-todo.v1',
})

const accepted = (preparedFactRef) => fact('TodoWriteAccepted', {
  ManagerLifeId: life,
  TodoWriteId: write,
  ToolCallId: call,
  PreparedFactRef: preparedFactRef,
  InputDigest: 'digest:provider-input',
  OutputDigest: 'digest:output',
  PhysicalSuccessEvidence: 'LiveAfterSuccess',
  SemanticVersion: 'magic-todo.v1',
})

const assertBoot = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.journal
}

test('WHAT[OBLIGATION-LEDGER-018] persists typed prepared identity through AgentJournal and EventStore boot', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-obligation-event-store-'))
  const startedAt = '2026-08-11T00:00:00Z'
  try {
    const booted = assertBoot(await journal.JournalSurface_bootWithWriterId(directory, 'writer-obligation', 'rt_magic_todo', 4242, startedAt))
    const preparedReceipt = await todoJournal.appendMagicTodo(booted, managerSession, 'assistant-message-id', prepared)
    assert.equal(preparedReceipt.ok, true, preparedReceipt.ok ? '' : preparedReceipt.error)
    assert.notEqual(preparedReceipt.eventId, '')

    const acceptedReceipt = await todoJournal.appendMagicTodo(
      booted,
      managerSession,
      'assistant-message-id',
      accepted(preparedReceipt.eventId),
    )
    assert.equal(acceptedReceipt.ok, true, acceptedReceipt.ok ? '' : acceptedReceipt.error)
    assert.notEqual(acceptedReceipt.eventId, '')

    const live = todoJournal.snapshotMagicTodo(booted, life)
    assert.equal(live.checkpoints.length, 1)
    assert.equal(live.checkpoints[0].providerInputDigest, 'digest:provider-input')
    assert.equal(live.checkpoints[0].planCompleteDeclared, true)
    assert.equal(live.checkpoints[0].accepted, true)
    assert.equal(live.firstPlanCommitment, write)
    journal.JournalSurface_dispose(booted)

    const resumed = assertBoot(await journal.JournalSurface_bootWithWriterId(directory, 'writer-obligation', 'rt_magic_todo_recovery', 4243, '2026-08-11T00:01:00Z'))
    const recovered = todoJournal.snapshotMagicTodo(resumed, life)
    assert.ok(recovered, 'Magic Todo prepared fact must survive EventStore boot')
    assert.equal(recovered.checkpoints[0].providerInputDigest, 'digest:provider-input')
    assert.equal(recovered.checkpoints[0].planCompleteDeclared, true)
    assert.equal(recovered.checkpoints[0].accepted, true)
    assert.equal(recovered.firstPlanCommitment, write)
    assert.equal(recovered.checkpoints[0].toolCallId, call)
    assert.equal(recovered.checkpoints[0].reviewFrontier, 7)
    journal.JournalSurface_dispose(resumed)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})
