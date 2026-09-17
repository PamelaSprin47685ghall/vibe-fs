import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const todoJournal = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const sha256 = (value) => `sha:${value}`
const managerSession = 'ses_magic_todo_manager'
const life = 'life_magic_todo'
const call = 'call_magic_todo'
const write = todo.todoWriteId(sha256, life, call)
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const prepared = fact('TodoWritePrepared', {
  ManagerSessionId: managerSession,
  IncumbencyId: life,
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
  IncumbencyId: life,
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
    assert.deepEqual(live.checkpoints[0].lifecycle, {
      kind: 'Accepted',
      inputDigest: 'digest:provider-input',
      outputDigest: 'digest:output',
    })
    assert.equal(live.firstPlanCommitment, write)
    journal.JournalSurface_dispose(booted)

    const resumed = assertBoot(await journal.JournalSurface_bootWithWriterId(directory, 'writer-obligation', 'rt_magic_todo_recovery', 4243, '2026-08-11T00:01:00Z'))
    const recovered = todoJournal.snapshotMagicTodo(resumed, life)
    assert.ok(recovered, 'Magic Todo prepared fact must survive EventStore boot')
    assert.equal(recovered.checkpoints[0].providerInputDigest, 'digest:provider-input')
    assert.equal(recovered.checkpoints[0].planCompleteDeclared, true)
    assert.deepEqual(recovered.checkpoints[0].lifecycle, {
      kind: 'Accepted',
      inputDigest: 'digest:provider-input',
      outputDigest: 'digest:output',
    })
    assert.equal(recovered.firstPlanCommitment, write)
    assert.equal(recovered.checkpoints[0].toolCallId, call)
    assert.equal(recovered.checkpoints[0].reviewFrontier, 7)
    journal.JournalSurface_dispose(resumed)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const projection = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js");
const codec = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.js");
const envelope = await import("../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const sha256 = (value) => `digest:${value}`
const life = 'manager-life'
const managerSession = 'manager-session'
const call = 'todo-call'
const write = todo.todoWriteId(sha256, life, call)
const cursor = (sequence) => ({ Sequence: sequence })
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result
}
const error = (result) => {
  assert.equal(result.ok, false, 'expected projection rejection')
  return result.error
}
let nextEvent = 0
const foldMagic = (handle, magicFact, eventId = undefined) => {
  nextEvent += 1
  return projection.MagicTodoProjectionSurface_fold(handle, eventId ?? `magic-todo-${nextEvent}`, magicFact)
}
const preparedFact = ({
  managerSessionId = managerSession,
  managerLifeId = life,
  incumbencyId = life,
  todoWriteId = write,
  toolCallId = call,
  toolPartOrdinal = 2,
  baseTodoRef = 'base-list',
  baseTodoDigest = 'base-digest',
  proposedTodoRef = 'proposal-list',
  proposedTodoDigest = 'proposal-digest',
  planCompleteDeclared = false,
  providerInputDigest = 'provider-input-digest',
  reviewFrontier = 10,
  semanticVersion = 'magic-v1',
} = {}) => fact('TodoWritePrepared', {
  ManagerSessionId: managerSessionId,
  IncumbencyId: incumbencyId,
  TodoWriteId: todoWriteId,
  ToolCallId: toolCallId,
  ToolPartOrdinal: toolPartOrdinal,
  BaseTodoRef: baseTodoRef,
  BaseTodoDigest: baseTodoDigest,
  ProposedTodoRef: proposedTodoRef,
  ProposedTodoDigest: proposedTodoDigest,
  PlanCompleteDeclared: planCompleteDeclared,
  ProviderInputDigest: providerInputDigest,
  ReviewFrontier: cursor(reviewFrontier),
  SemanticVersion: semanticVersion,
})
const acceptedFact = ({
  managerLifeId = life,
  incumbencyId = life,
  todoWriteId = write,
  toolCallId = call,
  preparedFactRef = 'prepared-fact-ref',
  inputDigest = 'provider-input-digest',
  outputDigest = 'output-digest',
  physicalSuccessEvidence = 'LiveAfterSuccess',
  semanticVersion = 'magic-v1',
} = {}) => fact('TodoWriteAccepted', {
  IncumbencyId: incumbencyId,
  TodoWriteId: todoWriteId,
  ToolCallId: toolCallId,
  PreparedFactRef: preparedFactRef,
  InputDigest: inputDigest,
  OutputDigest: outputDigest,
  PhysicalSuccessEvidence: physicalSuccessEvidence,
  SemanticVersion: semanticVersion,
})
const prepared = preparedFact()
const accepted = acceptedFact()
const acceptedState = () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  ok(foldMagic(handle, accepted))
  return handle
}

test('WHAT[OBLIGATION-LEDGER-018] checkpoint lifecycle is tagged Prepared and Accepted', () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  assert.deepEqual(projection.MagicTodoProjectionSurface_view(handle, life).checkpoints[0].lifecycle, { kind: 'Prepared' })

  ok(foldMagic(handle, accepted))
  assert.equal(projection.MagicTodoProjectionSurface_view(handle, life).checkpoints[0].lifecycle.kind, 'Accepted')
})
test('WHAT[OBLIGATION-LEDGER-018] stores typed Magic Todo bytes in the canonical Fact envelope', () => {
  const typed = ok(codec.encode(accepted)).value
  const encoded = envelope.serializeMagicTodoEnvelope(typed)
  const decoded = envelope.deserializeMagicTodoEnvelope(encoded)

  assert.equal(decoded.ok, true)
  assert.equal(decoded.case, 'MagicTodo')
  assert.equal(decoded.payload, typed)
})
test('WHAT[OBLIGATION-LEDGER-018] legacy Prepared without planComplete decodes as committed true', () => {
  const legacy = prepared.replace(/,"PlanCompleteDeclared":false/, '')
  const decoded = codec.decode(legacy)
  assert.equal(decoded.ok, true)
  assert.equal(decoded.planCompleteDeclared, true)
})
test('WHAT[OBLIGATION-LEDGER-018] rejects forward Magic Todo payloads without throwing through boot fold', () => {
  const forward = prepared.replace('TodoWritePrepared', 'FutureMagicTodoCase')
  assert.doesNotThrow(() => codec.decode(forward))
  assert.equal(codec.decode(forward).ok, false)
})
test('WHAT[OBLIGATION-LEDGER-018] folds a typed Magic Todo envelope into the one canonical projection', () => {
  const typed = ok(codec.encode(prepared)).value
  const folded = envelope.foldMagicEnvelope(managerSession, 'manager-provider-run', typed)
  assert.equal(folded.ok, true, folded.ok ? '' : folded.error)
  assert.equal(folded.lives.length, 1)
  assert.equal(folded.lives[0].checkpoints, 1)
  assert.equal(folded.lives[0].proposedDigests[0], 'proposal-digest')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const host = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");
const membrane = await import("../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js");
const projectionSurface = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js");

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

test('WHAT[OBLIGATION-LEDGER-018] business sequencing prepares and accepts checkpoints through public membrane surface', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ob-ledger-workflow-'))
  const boot = await journal.JournalSurface_boot(directory, 'rt_ob_workflow', 101, '2026-08-11T00:00:00Z')
  assert.equal(boot.ok, true)
  try {
    const handle = boot.journal
    const sessionId = 'ses-workflow-1'
    const incumbencyId = 'life-workflow-1'
    const callId = 'call-wf-1'
    const obligations = [{ name: 'task-1', horizon: 'near', work: 'Do work' }]

    const openRes = await membrane.MagicTodoMembraneSurface_openLife(handle, sessionId, incumbencyId)
    assert.equal(openRes.ok, true)

    const args = { planComplete: true, workingOn: 'task-1', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)

    const prep = await membrane.MagicTodoMembraneSurface_prepare(
      handle, sessionId, callId, canonical, digest, true, obligations, 0,
    )
    assert.equal(prep.ok, true)

    const accepted = await membrane.MagicTodoMembraneSurface_accept(
      handle, prep.value.bridge, 'LiveAfterSuccess', digest, sha256Hex('output-wf'),
    )
    assert.equal(accepted.ok, true)
  } finally {
    journal.JournalSurface_dispose(boot.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
test('WHAT[OBLIGATION-LEDGER-018] hot-path queries use incremental projection facts on IncumbencyMagicTodoState', () => {
  const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
  const prepared = fact('TodoWritePrepared', {
    ManagerSessionId: 'ses-1',
    IncumbencyId: 'test-life',
    TodoWriteId: 'tw-1',
    ToolCallId: 'call-1',
    ToolPartOrdinal: 1,
    BaseTodoRef: 'base-ref',
    BaseTodoDigest: 'base-digest',
    ProposedTodoRef: 'prop-ref',
    ProposedTodoDigest: 'prop-digest',
    PlanCompleteDeclared: true,
    ProviderInputDigest: 'in-digest',
    ReviewFrontier: { Sequence: 10 },
    SemanticVersion: 'magic-v1',
  })
  const accepted = fact('TodoWriteAccepted', {
    IncumbencyId: 'test-life',
    TodoWriteId: 'tw-1',
    ToolCallId: 'call-1',
    PreparedFactRef: 'evt-1',
    InputDigest: 'in-digest',
    OutputDigest: 'out-digest',
    PhysicalSuccessEvidence: 'LiveAfterSuccess',
    SemanticVersion: 'magic-v1',
  })
  const handle = projectionSurface.MagicTodoProjectionSurface_create()
  projectionSurface.MagicTodoProjectionSurface_fold(handle, 'evt-1', prepared)
  projectionSurface.MagicTodoProjectionSurface_fold(handle, 'evt-2', accepted)
  const view = projectionSurface.MagicTodoProjectionSurface_view(handle, 'test-life')
  assert.ok(view, 'view must exist for folded incumbency')
  assert.equal('firstAcceptedCheckpoint' in view, true)
  assert.equal('latestAcceptedCheckpoint' in view, true)
  assert.equal('firstPlanCommitment' in view, true)
  assert.equal('latestCommittedCheckpoint' in view, true)
  assert.equal('previousCommittedCheckpoint' in view, true)
  assert.equal('acceptedOrder' in view, false)
  assert.equal('acceptedIds' in view, false)
  assert.equal(view.firstAcceptedCheckpoint, 'tw-1')
  assert.equal(view.latestAcceptedCheckpoint, 'tw-1')
  assert.equal(view.firstPlanCommitment, 'tw-1')
  assert.equal(view.latestCommittedCheckpoint, 'tw-1')
})
}
