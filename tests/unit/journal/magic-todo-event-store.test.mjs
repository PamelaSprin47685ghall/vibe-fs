import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  caseOf,
  idValue,
  magicTodo,
  magicTodoJournal,
  managerLifeId,
  payloadOf,
  providerRun,
  runtimeId,
  sessionId,
  stream,
  toolCallId,
  utcOffset,
} from '../support/domain.mjs'

const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const EsWriter = await import('../../../dist/Journal/EventStoreJournalWriter.js')
const AgentJournal = await import('../../../dist/Journal/AgentJournal.js')

const resolveExport = (mod, prefix) => {
  const entry = Object.entries(mod).find(([name]) => name.startsWith(prefix))
  assert.ok(entry, `${prefix} export missing`)
  return entry[1]
}

const mustOk = (result, label) => {
  assert.equal(caseOf(result), 'Ok', `${label}: ${caseOf(result)} ${JSON.stringify(payloadOf(result))}`)
  return payloadOf(result)
}

const createWriter = (store, raw, runtime = 'rt_magic_todo') => {
  const create = resolveExport(EsWriter, 'EventStoreJournalWriter_create')
  const pair = create(runtimeId(runtime), 4242, utcOffset('2026-08-11T00:00:00Z'), store, raw)
  return { writer: pair[0], init: pair[1] }
}

const resumeWriter = (store, raw) => {
  const resume = resolveExport(EsWriter, 'EventStoreJournalWriter_resumeOrCreate')
  const result = resume(runtimeId('rt_magic_todo_recovery'), 4243, utcOffset('2026-08-11T00:01:00Z'), store, raw)
  return mustOk(result, 'resumeOrCreate')
}

test('TODO-012 persists typed prepared identity through AgentJournal and EventStore boot', () => {
  const raw = GitRaw.GitRawStore_createInMemory()
  const store = Store.EventStore_create(raw)
  const { writer, init } = createWriter(store, raw)
  const journal = mustOk(AgentJournal.AgentJournalModule_createFromEventStore(writer, init), 'createFromEventStore')
  const managerSession = sessionId('ses_magic_todo_manager')
  const life = managerLifeId('life_magic_todo')
  const call = toolCallId('call_magic_todo')
  const write = magicTodo.todoWriteId((value) => `sha:${value}`, life, call)
  const prepared = new magicTodoJournal.TodoWritePrepared(
    managerSession,
    life,
    write,
    call,
    1,
    blobRef('blobs/base'),
    blobDigest('digest:base'),
    blobRef('blobs/proposed'),
    blobDigest('digest:proposed'),
    'digest:provider-input',
    new magicTodoJournal.XTraceCursor(7n),
    'magic-todo.v1',
  )

  try {
    const appended = AgentJournal.AgentJournalModule_appendMagicTodo(
      stream.session(managerSession),
      providerRun('assistant-message-id'),
      magicTodoJournal.MagicTodoFact('TodoWritePrepared', [prepared]),
      journal,
    )
    const preparedReceipt = mustOk(appended, 'append prepared')
    assert.notEqual(idValue.event(preparedReceipt.EventId), '')

    const accepted = new magicTodoJournal.TodoWriteAccepted(
      life,
      write,
      call,
      preparedReceipt.EventId,
      'digest:provider-input',
      'digest:output',
      magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
      'magic-todo.v1',
    )
    const acceptedReceipt = mustOk(
      AgentJournal.AgentJournalModule_appendMagicTodo(
        stream.session(managerSession),
        providerRun('assistant-message-id'),
        magicTodoJournal.MagicTodoFact('TodoWriteAccepted', [accepted]),
        journal,
      ),
      'append accepted',
    )
    assert.notEqual(idValue.event(acceptedReceipt.EventId), '')

    const live = AgentJournal.AgentJournalModule_snapshot(journal).AgentProjections.MagicTodo.ByLife.get('life_magic_todo')
    assert.equal(live.Checkpoints.size, 1)
    assert.equal(live.Checkpoints.get(magicTodo.todoWriteIdValue(write)).ProviderInputDigest, 'digest:provider-input')
    assert.equal(live.Checkpoints.get(magicTodo.todoWriteIdValue(write)).Accepted, true)
  } finally {
    journal.Dispose()
  }

  const resumed = resumeWriter(store, raw)
  try {
    const recovered = resumed[2].AgentProjections.MagicTodo.ByLife.get('life_magic_todo')
    assert.ok(recovered, 'Magic Todo prepared fact must survive EventStore boot')
    const checkpoint = recovered.Checkpoints.get(magicTodo.todoWriteIdValue(write))
    assert.equal(checkpoint.ProviderInputDigest, 'digest:provider-input')
    assert.equal(checkpoint.Accepted, true)
    assert.equal(idValue.toolCall(checkpoint.ToolCallId), 'call_magic_todo')
    assert.equal(Number(checkpoint.ReviewFrontier.Sequence), 7)
  } finally {
    resumed[0].Dispose()
  }
})
